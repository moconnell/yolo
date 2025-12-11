namespace YoloBroker.AzureVault;

using System;
using System.Formats.Asn1;
using System.Numerics;
using System.Threading.Tasks;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Nethereum.Signer.Crypto;
using Nethereum.Util;

/// <summary>
/// Signs 32-byte message digests with an Azure Key Vault EC-SECP256K1 key,
/// and computes Ethereum-compatible (r,s,v) signature and address.
/// </summary>
public sealed class AzureKeyVaultSigner
{
    private readonly CryptographyClient _crypto;
    private readonly KeyClient _keyClient;
    private readonly string _keyName;

    /// <param name="vaultUri">e.g. https://myTradingVault.vault.azure.net/</param>
    /// <param name="keyName">The name of your EC-SECP256K1 key in Key Vault</param>
    /// <param name="credential">Usually new DefaultAzureCredential()</param>
    public AzureKeyVaultSigner(Uri vaultUri, string keyName, Azure.Core.TokenCredential credential)
    {
        _keyClient = new KeyClient(vaultUri, credential);
        var key = _keyClient.GetKey(keyName).Value;
        _crypto = new CryptographyClient(key.Id, credential);
        _keyName = keyName;

        if (key.KeyType != KeyType.Ec && key.KeyType != KeyType.EcHsm)
            throw new InvalidOperationException($"Key '{keyName}' is not EC.");
        // if (key.KeyOperations != KeyCurveName.P256K)
        //     throw new InvalidOperationException($"Key '{keyName}' must be created with curve SECP256K1.");
    }

    /// <summary>
    /// Returns the Ethereum address (EIP-55) for the Key Vault public key.
    /// </summary>
    public async Task<string> GetAddressAsync()
    {
        var jwk = (await _keyClient.GetKeyAsync(_keyName)).Value.Key;
        var pubUncompressed = BuildUncompressedPublicKey(jwk); // 0x04 || X || Y
                                                               // Ethereum address is keccak256(X||Y) last 20 bytes
        var keccak = new Sha3Keccack();
        var hash = keccak.CalculateHash(pubUncompressed.AsSpan(1).ToArray()); // drop 0x04
        var addrBytes = hash[^20..];
        var addr = "0x" + addrBytes.ToHex();
        return AddressUtil.Current.ConvertToChecksumAddress(addr);
    }

    /// <summary>
    /// Signs a 32-byte digest (already keccak256/EIP-712 hash) and returns 65-byte r||s||v.
    /// </summary>
    public async Task<byte[]> SignDigestAsync(byte[] digest32, VEncoding vEncoding = VEncoding.Ethereum27)
    {
        if (digest32.Length != 32) throw new ArgumentException("Digest must be 32 bytes.", nameof(digest32));

        // 1) Ask Key Vault to sign the digest using ES256K (ECDSA over secp256k1, SHA-256).
        // We provide a digest, so MessageType=DIGEST.
        var signResult = await _crypto.SignAsync(SignatureAlgorithm.ES256K, digest32);
        // 2) Azure returns DER-encoded signature; decode to (r,s).
        (BigInteger r, BigInteger s) = DecodeSignature(signResult.Signature);

        // Ensure r,s are 32-byte big-endian
        var r32 = ToFixed32(r);
        var s32 = ToFixed32(s);

        // 3) Compute correct recovery id 'v' by actually recovering pubkey and matching.
        //    First, get our expected public key from KV (to compare).
        var jwk = (await _keyClient.GetKeyAsync(_keyName)).Value.Key;
        var pubUncompressed = BuildUncompressedPublicKey(jwk); // 65 bytes

        // Try with given (r,s)
        int? recId = ComputeRecoveryId(digest32, r32, s32, pubUncompressed);
        if (recId is null)
        {
            // If signature had high-s (unlikely; KV should canonicalize), try low-s transform: s' = N - s
            var n = Secp256k1N();
            var sBig = new BigInteger(s32, isUnsigned: true, isBigEndian: true);
            var sLow = n - sBig;
            var sLow32 = ToFixed32(sLow);
            recId = ComputeRecoveryId(digest32, r32, sLow32, pubUncompressed);
            if (recId is null)
                throw new InvalidOperationException("Could not compute recovery id for the signature.");
            s32 = sLow32; // use low-s if that matched
        }

        byte v = vEncoding == VEncoding.Ethereum27 ? (byte)(recId.Value + 27) : (byte)(recId.Value);

        // 4) Return 65-byte signature r||s||v
        var sig = new byte[65];
        r32.CopyTo(sig, 0);
        s32.CopyTo(sig, 32);
        sig[64] = v;
        return sig;
    }

    // ---- Helpers ----

    private static byte[] BuildUncompressedPublicKey(JsonWebKey jwk)
    {
        if (jwk.CurveName != KeyCurveName.P256K || jwk.X is null || jwk.Y is null)
            throw new InvalidOperationException("KeyVault JWK missing X/Y or wrong curve.");
        // X and Y are big-endian 32 bytes
        var x = LeftPad32(jwk.X);
        var y = LeftPad32(jwk.Y);
        var pub = new byte[65];
        pub[0] = 0x04;
        Buffer.BlockCopy(x, 0, pub, 1, 32);
        Buffer.BlockCopy(y, 0, pub, 33, 32);
        return pub;
    }

    private static (BigInteger r, BigInteger s) DecodeSignature(byte[] sig)
    {
        // Case 1 — Raw 64-byte signature (R||S)
        if (sig.Length == 64)
        {
            var r = new BigInteger(sig.AsSpan(0, 32).ToArray(), true, true);
            var s = new BigInteger(sig.AsSpan(32, 32).ToArray(), true, true);
            return (r, s);
        }

        // Case 2 — DER-encoded signature
        if (sig.Length > 64 && sig[0] == 0x30)
        {
            var reader = new AsnReader(sig, AsnEncodingRules.DER);
            var seq = reader.ReadSequence();
            var r = new BigInteger(seq.ReadIntegerBytes().ToArray(), true, true);
            var s = new BigInteger(seq.ReadIntegerBytes().ToArray(), true, true);
            return (r, s);
        }

        throw new InvalidOperationException($"Unknown ES256K signature format (len={sig.Length}, firstByte=0x{sig[0]:X2}).");
    }

    private static byte[] ToFixed32(BigInteger x)
    {
        // BigInteger.ToByteArray can include a leading 0x00; force 32 big-endian
        var tmp = x.ToByteArray(isUnsigned: true, isBigEndian: true);
        if (tmp.Length > 32) throw new InvalidOperationException("Integer too large for 32 bytes.");
        if (tmp.Length == 32) return tmp;
        var out32 = new byte[32];
        Buffer.BlockCopy(tmp, 0, out32, 32 - tmp.Length, tmp.Length);
        return out32;
    }

    private static byte[] LeftPad32(byte[] src)
    {
        if (src.Length == 32) return src;
        if (src.Length > 32) throw new InvalidOperationException("Unexpected length for coordinate.");
        var out32 = new byte[32];
        Buffer.BlockCopy(src, 0, out32, 32 - src.Length, src.Length);
        return out32;
    }

    private static BigInteger Secp256k1N()
    {
        // order n of secp256k1
        var nHex = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEBAAEDCE6AF48A03BBFD25E8CD0364141";
        return BigInteger.Parse("00" + nHex, System.Globalization.NumberStyles.HexNumber);
    }

    private static int? ComputeRecoveryId(byte[] digest32, byte[] r32, byte[] s32, byte[] expectedUncompressedPub)
    {
        // Try recId 0 and 1; recover pubkey and compare
        var sig = new ECDSASignature(new Org.BouncyCastle.Math.BigInteger(1, r32), new Org.BouncyCastle.Math.BigInteger(1, s32));
        for (int recId = 0; recId <= 1; recId++)
        {
            try
            {
                var ecKey = ECKey.RecoverFromSignature(recId, sig, digest32, false);
                if (ecKey == null) continue;
                var recovered = ecKey.GetPubKey(false); // uncompressed, 65 bytes (0x04 + X + Y)
                if (BytesEqual(recovered, expectedUncompressedPub))
                    return recId;
            }
            catch { /* ignore and try next */ }
        }
        return null;
    }

    private static bool BytesEqual(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        => a.SequenceEqual(b);
}

// Small hex helper
internal static class BytesExt
{
    public static string ToHex(this ReadOnlySpan<byte> data)
    {
        char[] c = new char[data.Length * 2];
        int b;
        for (int i = 0; i < data.Length; i++)
        {
            b = data[i] >> 4;
            c[i * 2] = (char)(55 + b + (((b - 10) >> 31) & -7));
            b = data[i] & 0xF;
            c[i * 2 + 1] = (char)(55 + b + (((b - 10) >> 31) & -7));
        }
        return new string(c).ToLowerInvariant();
    }
}
