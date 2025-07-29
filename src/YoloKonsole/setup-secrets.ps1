# Create secrets directory
$secretsPath = "C:\secrets\yolo"
New-Item -Path $secretsPath -ItemType Directory -Force

# Prompt for secrets securely
$address = Read-Host -Prompt "Enter Hyperliquid Address"
$privateKey = Read-Host -Prompt "Enter Hyperliquid Private Key" -AsSecureString
$apiKey = Read-Host -Prompt "Enter Yolo API Key" -AsSecureString

# Convert secure strings to plain text for file writing
$privateKeyPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($privateKey))
$apiKeyPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($apiKey))

# Write secrets to files
$address | Out-File -FilePath "$secretsPath\Hyperliquid__Address" -Encoding utf8 -NoNewline
$privateKeyPlain | Out-File -FilePath "$secretsPath\Hyperliquid__PrivateKey" -Encoding utf8 -NoNewline
$apiKeyPlain | Out-File -FilePath "$secretsPath\Yolo__ApiKey" -Encoding utf8 -NoNewline

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
Clear-Variable privateKeyPlain, apiKeyPlain -ErrorAction SilentlyContinue
