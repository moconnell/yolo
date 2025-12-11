
using Azure.Identity;
using YoloBroker.Interface;

namespace YoloBroker.AzureVault;

public static class AzureKeyVaultSignerExtensions
{
    public static void ConfigureHAzureKeyVaultSigner(this IYoloBroker broker, AzureVaultConfig config)
    {
        var signer = new AzureKeyVaultSigner(
            new Uri(config.VaultUri),
            config.KeyName,
            new DefaultAzureCredential());

        broker.ConfigureSigning((req, _) =>
        {
            var digest = Convert.FromHexString(req);
            var sig = signer.SignDigestAsync(digest).GetAwaiter().GetResult();
            var r = sig[..32];
            var s = sig[32..64];
            var v = sig[64];

            return new Dictionary<string, object>
            {
                { "r", "0x" + Convert.ToHexString(r).ToLowerInvariant() },
                { "s", "0x" + Convert.ToHexString(s).ToLowerInvariant() },
                { "v", (int)v }
            };
        });
    }
}