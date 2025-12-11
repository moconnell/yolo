using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer.Crypto;
using Nethereum.Util;
using Org.BouncyCastle.Math;
using Shouldly;
using Xunit.Abstractions;

namespace YoloBroker.AzureVault.Test;

public class AzureKeyVaultSignerTest(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GivenVaultUriAndKeyName_SignerRecoversAddress()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.local.json", optional: true)
            .Build();
        var azureVaultConfig = configuration.GetRequiredSection("AzureVault").Get<AzureVaultConfig>()!;

        var vaultUri = new Uri(azureVaultConfig.VaultUri);
        var keyName = azureVaultConfig.KeyName;
        var expectedAddress = azureVaultConfig.ExpectedAddress;

        var signer = new AzureKeyVaultSigner(vaultUri, keyName, new DefaultAzureCredential());

        // 1. Show ETH address
        var addr = await signer.GetAddressAsync();
        output.WriteLine("Agent address  : " + addr);
        addr.ShouldBe(expectedAddress);

        // 2. Sign a known digest
        var digest = Enumerable.Repeat((byte)0x42, 32).ToArray(); // fake 32-byte hash
        var sig = await signer.SignDigestAsync(digest);

        // 3. Recover address using Nethereum and compare
        var r = sig[..32];
        var s = sig[32..64];
        var v = sig[64];

        var ecSig = new ECDSASignature(new BigInteger(1, r), new BigInteger(1, s));
        var recId = v == 27 || v == 28 ? v - 27 : v; // adapt for your encoding
        var ecKey = ECKey.RecoverFromSignature(recId, ecSig, digest, false);
        var recPub = ecKey.GetPubKey(false);          // 0x04 || X || Y

        var keccak = new Sha3Keccack();
        var hash = keccak.CalculateHash(recPub.AsSpan(1).ToArray()); // drop 0x04
        var recAddrBytes = hash[^20..];
        var recAddr = "0x" + recAddrBytes.ToHex();
        recAddr = AddressUtil.Current.ConvertToChecksumAddress(recAddr);

        output.WriteLine("Recovered addr : " + recAddr);

        output.WriteLine(recAddr == addr
            ? "✅ Signer + recovery OK"
            : "❌ Address mismatch – something is off");

        recAddr.ShouldBe(expectedAddress);
    }
}
