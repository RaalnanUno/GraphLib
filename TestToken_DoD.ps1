# -------------------------
# TestToken_DoD.ps1
# -------------------------

$ErrorActionPreference = "Stop"

# ==== CONFIG ====
$tenantId     = "YOUR-TENANT-ID"
$clientId     = "YOUR-CLIENT-ID"
$clientSecret = "YOUR-CLIENT-SECRET"

$spoHost  = "fdicgov.sharepoint.com"
$sitePath = "/sites/apdb/EVAuto"

# ==== ENDPOINTS ====
$tokenUrl = "https://login.microsoftonline.us/$tenantId/oauth2/v2.0/token"
$scope    = "https://dod-graph.microsoft.us/.default"
$graph    = "https://dod-graph.microsoft.us/v1.0"

# ==== TOKEN ====
Write-Host "Requesting token (DoD)..." -ForegroundColor Cyan
$token = Invoke-RestMethod `
  -Method Post `
  -Uri $tokenUrl `
  -ContentType "application/x-www-form-urlencoded" `
  -Body @{
    client_id     = $clientId
    client_secret = $clientSecret
    grant_type    = "client_credentials"
    scope         = $scope
  }

$headers = @{
  Authorization = "Bearer $($token.access_token)"
}

# ==== SITE RESOLUTION ====
$siteUrl = "$graph/sites/${spoHost}:$sitePath?`$select=id,displayName,webUrl"
Write-Host "Calling $siteUrl" -ForegroundColor Yellow

Invoke-RestMethod -Headers $headers -Uri $siteUrl -Method Get

Write-Host "SUCCESS (DoD)" -ForegroundColor Green
