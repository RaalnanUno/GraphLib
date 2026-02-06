# -------------------------------------------------------
# TestToken_ByHostnameAndRelativePath.ps1
# Uses: GET /sites/{hostname}:/sites/...
# -------------------------------------------------------

$ErrorActionPreference = "Stop"

# ==== CONFIG ====
$tenantId     = "YOUR-TENANT-ID"
$clientId     = "YOUR-CLIENT-ID"
$clientSecret = "YOUR-CLIENT-SECRET"

$spoHost  = "fdicgov.sharepoint.com"
$sitePath = "/sites/apdb/EVAuto"

# ==== ENDPOINTS ====
$tokenUrl  = "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token"
$scope     = "https://graph.microsoft.com/.default"
$graphBase = "https://graph.microsoft.com/v1.0"

# ==== TOKEN ====
Write-Host "Requesting token..." -ForegroundColor Cyan
$token = Invoke-RestMethod -Method Post -Uri $tokenUrl -ContentType "application/x-www-form-urlencoded" -Body @{
  client_id     = $clientId
  client_secret = $clientSecret
  grant_type    = "client_credentials"
  scope         = $scope
}

$headers = @{ Authorization = "Bearer $($token.access_token)" }

# IMPORTANT: ${spoHost} avoids the ":" parsing issue in PowerShell
$siteUrl = "$graphBase/sites/${spoHost}:$sitePath?`$select=id,displayName,webUrl"

Write-Host "Calling URL:" -ForegroundColor Yellow
Write-Host $siteUrl

Invoke-RestMethod -Headers $headers -Uri $siteUrl -Method Get
