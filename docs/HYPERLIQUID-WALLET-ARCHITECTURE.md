# Hyperliquid Wallet Architecture

Hyperliquid uses two distinct identities:

1. The **agent wallet** signs API transactions and should hold no funds.
2. The **vault address** identifies the funded account whose positions are managed.

Production should use a hardware-backed owner wallet and separate funded vaults/subaccounts for YoloDaily and UnravelDaily. The agent can be rotated without moving funds; the owner-wallet private key must never be exposed to the application.

## Environment isolation

Development and production have separate Azure Key Vaults. Development contains only Hyperliquid testnet credentials; production contains only mainnet credentials. Both vaults use the same names:

- `hyperliquid-agent-address`
- `hyperliquid-agent-privatekey`
- `hyperliquid-vault-yolodaily`
- `hyperliquid-vault-unraveldaily`

See [Azure Key Vault Secrets Setup](AZURE-KEY-VAULT-SECRETS-SETUP.md) for commands and access verification.

At runtime, Bicep maps those secrets to each strategy's `HyperliquidConfig`:

```csharp
public class HyperliquidConfig
{
    public string Address { get; set; }        // Agent address
    public string PrivateKey { get; set; }     // Agent signing key
    public string? VaultAddress { get; set; }  // Funded strategy vault
    public bool UseTestnet { get; set; }
}
```

The development deployment forces `UseTestnet=true`; production forces `UseTestnet=false`. Branch and environment validation in the deployment workflow prevents selecting mainnet from `develop` or testnet from `master`.

## Setup

1. Generate a different agent wallet for each environment with `scripts/generate-eth-keypair.py`.
2. Authorize the development agent on Hyperliquid testnet and the production agent on mainnet.
3. Create separate vaults/subaccounts for each strategy.
4. Store agent and vault values in the appropriate environment's Key Vault.
5. Confirm the agent wallet holds no funds and each Function App identity can read only its own Key Vault.

Never reuse the production agent private key, vault address, or owner-wallet credentials in development.
