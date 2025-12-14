#!/usr/bin/env python3
"""
Generate a new Ethereum-compatible key pair for Hyperliquid testnet/mainnet wallet.

Usage:
    python3 generate-eth-keypair.py

Requirements:
    pip install eth-account
"""

from eth_account import Account
import secrets


def generate_keypair():
    """Generate a new Ethereum key pair."""
    # Generate a random private key (32 bytes)
    private_key = "0x" + secrets.token_hex(32)

    # Create account from private key
    account = Account.from_key(private_key)

    return {
        'address': account.address,
        'private_key': private_key
    }


def main():
    print("🔑 Generating new Ethereum-compatible key pair...")
    print("=" * 60)
    print()

    keypair = generate_keypair()

    print("✅ Key pair generated successfully!")
    print()
    print("📋 Wallet Details:")
    print("-" * 60)
    print(f"Address:     {keypair['address']}")
    print(f"Private Key: {keypair['private_key']}")
    print()
    print("⚠️  SECURITY WARNINGS:")
    print("-" * 60)
    print("1. NEVER share your private key with anyone")
    print("2. NEVER commit your private key to source control")
    print("3. Store the private key securely in Azure Key Vault")
    print("4. This wallet has NO FUNDS - you must fund it before use")
    print()
    print("📝 Next Steps:")
    print("-" * 60)
    print("For TESTNET:")
    print("  1. Fund this address with testnet tokens")
    print("  2. Store credentials in Azure Key Vault:")
    print()
    print(f"     az keyvault secret set \\")
    print(f"       --vault-name YOLO \\")
    print(f"       --name hyperliquid-dev-address \\")
    print(f"       --value '{keypair['address']}'")
    print()
    print(f"     az keyvault secret set \\")
    print(f"       --vault-name YOLO \\")
    print(f"       --name hyperliquid-dev-privatekey \\")
    print(f"       --value '{keypair['private_key']}'")
    print()
    print("For MAINNET:")
    print("  ⚠️  Use a hardware wallet or secure key management!")
    print("  1. Fund this address with REAL tokens (be careful!)")
    print("  2. Store credentials in Azure Key Vault:")
    print()
    print(f"     az keyvault secret set \\")
    print(f"       --vault-name YOLO \\")
    print(f"       --name hyperliquid-prod-address \\")
    print(f"       --value '{keypair['address']}'")
    print()
    print(f"     az keyvault secret set \\")
    print(f"       --vault-name YOLO \\")
    print(f"       --name hyperliquid-prod-privatekey \\")
    print(f"       --value '{keypair['private_key']}'")
    print()
    print("=" * 60)


if __name__ == "__main__":
    try:
        main()
    except ImportError:
        print("❌ Error: eth-account module not found")
        print()
        print("Install it with:")
        print("  pip install eth-account")
        print()
        print("Or use pipx for isolated installation:")
        print("  pipx run --spec eth-account generate-eth-keypair.py")
        exit(1)
