using System;
using System.Collections.Generic;
using Nethereum.Signer;

namespace YoloBroker.Hyperliquid.CustomSigning;

public static class NethereumSigningExtensions
{
    public static Dictionary<string, object> SignMessage(this string message, string privateKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(message, nameof(message));
        ArgumentException.ThrowIfNullOrEmpty(privateKey, nameof(privateKey));

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