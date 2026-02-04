# TestToken_Commercial_Force.ps1
$ErrorActionPreference = "Stop"

# ==== CONFIG ====
$tenantId     = "YOUR-TENANT-ID"
$clientId     = "YOUR-CLIENT-ID"
$clientSecret = "YOUR-CLIENT-SECRET"

$spoHost  = "fdicgov.sharepoint.com"
$sitePath = "/sites/apdb/EVAuto"

# ==== FORCE COMMERCIAL CLOUD ====
$tokenUrl = "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token"
$scope    = "https://graph.microsoft.com/.default"
$graph    = "https://graph.microsoft.com/v1.0"

Write-Host "Token URL : $tokenUrl"
Write-Host "Scope     : $scope"
Write-Host "Graph     : $graph"
Write-Host "SPO Host  : $spoHost"
Write-Host "Site Path : $sitePath"
Write-Host ""

# ==== TOKEN ====
$token = Invoke-RestMethod -Method Post -Uri $tokenUrl -ContentType "application/x-www-form-urlencoded" -Body @{
  client_id     = $clientId
  client_secret = $clientSecret
  grant_type    = "client_credentials"
  scope         = $scope
}

$headers = @{ Authorization = "Bearer $($token.access_token)" }

# ==== SITE CALL ====
# NOTE: ${spoHost}: is REQUIRED because of the colon.
$siteUrl = "$graph/sites/${spoHost}:$sitePath?`$select=id,displayName,webUrl"
Write-Host "Calling: $siteUrl" -ForegroundColor Yellow

Invoke-RestMethod -Headers $headers -Uri $siteUrl -Method Get
