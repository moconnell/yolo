# Set environment variables
$env:YOLO_SECRETS_PATH = "C:\secrets\yolo"

# Verify secrets exist
if (-not (Test-Path $env:YOLO_SECRETS_PATH)) {
    Write-Error "Secrets directory not found: $env:YOLO_SECRETS_PATH"
    Write-Host "Run setup-secrets.ps1 first to configure secrets"
    exit 1
}

$requiredSecrets = @("Hyperliquid__Address", "Hyperliquid__PrivateKey", "Yolo__ApiKey")
foreach ($secret in $requiredSecrets) {
    $secretFile = Join-Path $env:YOLO_SECRETS_PATH $secret
    if (-not (Test-Path $secretFile)) {
        Write-Error "Missing secret file: $secretFile"
        exit 1
    }
}

Write-Host "Starting YOLO Console..." -ForegroundColor Green
Write-Host "Secrets path: $env:YOLO_SECRETS_PATH" -ForegroundColor Yellow

# Run the application
.\YoloKonsole.exe
