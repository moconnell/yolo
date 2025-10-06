# Create secrets directory
$secretsPath = Join-Path $PWD "secrets"
$env:YOLO_SECRETS_PATH = $secretsPath
New-Item -Path $secretsPath -ItemType Directory -Force

# Prompt for secrets securely
$address = Read-Host -Prompt "Enter Hyperliquid wallet address"
$privateKey = Read-Host -MaskInput "Enter Hyperliquid wallet private key"

# Write secrets to files
$address | Out-File -FilePath "$secretsPath\Hyperliquid__Address" -Encoding utf8 -NoNewline
$privateKey | Out-File -FilePath "$secretsPath\Hyperliquid__PrivateKey" -Encoding utf8 -NoNewline

# Set restrictive permissions (only current user can read)
$acl = Get-Acl $secretsPath
$acl.SetAccessRuleProtection($true, $false)  # Disable inheritance
$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    $env:USERNAME, "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow"
)
$acl.SetAccessRule($accessRule)
Set-Acl -Path $secretsPath -AclObject $acl

# Apply same permissions to all secret files
Get-ChildItem $secretsPath | ForEach-Object {
    Set-Acl -Path $_.FullName -AclObject $acl
}

Write-Host "Secrets configured successfully in $secretsPath" -ForegroundColor Green
Write-Host "Files created:" -ForegroundColor Yellow
Get-ChildItem $secretsPath | ForEach-Object { Write-Host "  - $($_.Name)" }

# Clear variables containing secrets
Clear-Variable address, privateKey, apiKey -ErrorAction SilentlyContinue
