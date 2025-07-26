using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Signer;

namespace YoloBroker.Hyperliquid.CustomSigning;

public static class NethereumSigningExtensions
{
    public static Dictionary<string, object> SignMessage(this string message, string privateKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(message, nameof(message));
        ArgumentException.ThrowIfNullOrEmpty(privateKey, nameof(privateKey));
        if (!message[2..].All(char.IsAsciiHexDigit))
        {
            throw new ArgumentException("Message must be a valid hex string", nameof(message));
        }
        if (privateKey.Length != 66 || !privateKey[2..].All(char.IsAsciiHexDigit))
        {
            throw new ArgumentException("Private key must be a valid hex string of length 64", nameof(privateKey));
        }

        var messageBytes = Convert.FromHexString(message);
        var sign = new MessageSigner().SignAndCalculateV(messageBytes, new EthECKey(privateKey));


        return new Dictionary<string, object>
            {
                { "r", "0x" + Convert.ToHexString(sign.R).ToLowerInvariant() },
                { "s", "0x" + Convert.ToHexString(sign.S).ToLowerInvariant() },
                { "v", (int)sign.V[0] }
            };
    }
}