#!/bin/bash

# Alternative: Generate Ethereum keypair using openssl (no Python dependencies)
# This uses pure cryptographic primitives to generate a valid Ethereum key pair

set -e

echo "🔑 Generating Ethereum-compatible key pair..."
echo "============================================================"
echo ""

# Generate 32-byte private key
PRIVATE_KEY_HEX=$(openssl rand -hex 32)

# Add 0x prefix for Ethereum format
PRIVATE_KEY="0x${PRIVATE_KEY_HEX}"

echo "✅ Private Key Generated"
echo ""

# To derive the address, we need to use a tool that can do secp256k1 + keccak256
# This is complex without libraries, so we'll just show the private key

echo "📋 Wallet Details:"
echo "------------------------------------------------------------"
echo "Private Key: ${PRIVATE_KEY}"
echo ""
echo "⚠️  To get the public address, use one of these options:"
echo ""
echo "Option 1: Use Python script (recommended)"
echo "  python3 generate-eth-keypair.py"
echo ""
echo "Option 2: Use cast (Foundry) if installed:"
echo "  cast wallet address ${PRIVATE_KEY}"
echo ""
echo "Option 3: Import into MetaMask and copy address"
echo "  Settings → Security → Show Secret Recovery Phrase"
echo ""
echo "⚠️  SECURITY WARNINGS:"
echo "------------------------------------------------------------"
echo "1. NEVER share your private key with anyone"
echo "2. NEVER commit your private key to source control"  
echo "3. Store securely in Azure Key Vault immediately"
echo "4. This wallet has NO FUNDS - fund it before use"
echo ""
echo "============================================================"
