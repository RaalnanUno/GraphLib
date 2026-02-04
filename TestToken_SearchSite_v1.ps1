# -------------------------------------------------------
# TestToken_SearchSite_v1.ps1
# Uses: GET https://graph.microsoft.com/v1.0/sites?search="term"
# -------------------------------------------------------

$ErrorActionPreference = "Stop"

# ==== CONFIG ====
$tenantId     = "YOUR-TENANT-ID"
$clientId     = "YOUR-CLIENT-ID"
$clientSecret = "YOUR-CLIENT-SECRET"

$siteSearch = "EVAuto" # keep this short and unique

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

# ==== SEARCH SITES ====
# Graph expects quotes around the search term for best compatibility
$searchParam = [Uri]::EscapeDataString('"' + $siteSearch + '"')
$siteUrl = "$graphBase/sites?search=$searchParam&`$select=id,displayName,webUrl"

Write-Host "Calling URL:" -ForegroundColor Yellow
Write-Host $siteUrl

$resp = Invoke-RestMethod -Headers $headers -Uri $siteUrl -Method Get

Write-Host "Returned sites:" -ForegroundColor Green
$resp.value | ForEach-Object {
  Write-Host " - $($_.displayName) :: $($_.webUrl) :: $($_.id)"
}
