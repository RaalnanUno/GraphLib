# -----------------------------------------
# TestToken_ResolveSiteId_ByPath.ps1
# Resolve Site using /sites/{host}:{path} form
# -----------------------------------------

$ErrorActionPreference = "Stop"

# ==== CONFIG ====
$tenantId     = "YOUR-TENANT-ID"
$clientId     = "YOUR-CLIENT-ID"
$clientSecret = "YOUR-CLIENT-SECRET"

$spoHost  = "fdicgov.sharepoint.com"
$sitePath = "/sites/apdb/EVAuto"

# ==== ENDPOINTS (Commercial) ====
$tokenUrl = "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token"
$scope    = "https://graph.microsoft.com/.default"
$graph    = "https://graph.microsoft.com/v1.0"

Write-Host "Requesting token..." -ForegroundColor Cyan
$token = Invoke-RestMethod -Method Post -Uri $tokenUrl -ContentType "application/x-www-form-urlencoded" -Body @{
  client_id     = $clientId
  client_secret = $clientSecret
  grant_type    = "client_credentials"
  scope         = $scope
}

$headers = @{ Authorization = "Bearer $($token.access_token)" }

# IMPORTANT: ${spoHost}: avoids PowerShell parsing issues with :
$siteUrl = "$graph/sites/${spoHost}:$sitePath?`$select=id,displayName,webUrl"
Write-Host "Calling: $siteUrl" -ForegroundColor Yellow

Invoke-RestMethod -Headers $headers -Uri $siteUrl -Method Get
