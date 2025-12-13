
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using YoloBroker.AzureVault.Config;
using YoloBroker.Interface;

namespace YoloBroker.AzureVault.Extensions;

public static class AzureKeyVaultSignerExtensions
{
    public static void ConfigureAzureKeyVaultSigner(this IYoloBroker broker, IConfiguration config)
    {
        var azureVaultConfig = config.GetSection("AzureVault").Get<AzureVaultConfig>();
        broker.ConfigureAzureKeyVaultSigner(azureVaultConfig);
    }

    public static void ConfigureAzureKeyVaultSigner(this IYoloBroker broker, AzureVaultConfig? config)
    {
        if (config == null)
        {
            return;
        }

        var signer = new AzureKeyVaultSigner(
            new Uri(config.VaultUri),
            config.KeyName,
            new DefaultAzureCredential());

        if (config.ExpectedAddress != null)
        {
            var address = signer.GetAddressAsync().GetAwaiter().GetResult();
            if (!string.Equals(address, config.ExpectedAddress, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Azure Key Vault signer address mismatch. Expected: {config.ExpectedAddress}, Actual: {address}");
            }
        }

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