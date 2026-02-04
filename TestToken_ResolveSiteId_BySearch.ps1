# -----------------------------------------
# TestToken_ResolveSiteId_BySearch.ps1
# Resolve Site ID using Graph search, then read site details.
# -----------------------------------------

$ErrorActionPreference = "Stop"

# ==== CONFIG ====
$tenantId     = "YOUR-TENANT-ID"
$clientId     = "YOUR-CLIENT-ID"
$clientSecret = "YOUR-CLIENT-SECRET"

# What to search for (keep it short & unique)
$siteSearch = "EVAuto"

# Optional: help disambiguate if your tenant has multiple results
$expectedWebUrlContains = "fdicgov.sharepoint.com/sites/apdb/EVAuto"

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

# 1) Search sites
$searchUrl = "$graph/sites?search=$([Uri]::EscapeDataString($siteSearch))&`$select=id,displayName,webUrl"
Write-Host "Searching sites: $searchUrl" -ForegroundColor Yellow
$searchRes = Invoke-RestMethod -Headers $headers -Uri $searchUrl -Method Get

if (-not $searchRes.value -or $searchRes.value.Count -eq 0) {
  throw "No sites returned for search '$siteSearch'. Try a different search term."
}

# 2) Pick best match
$match = $null
foreach ($s in $searchRes.value) {
  if ($s.webUrl -and $s.webUrl.ToLower().Contains($expectedWebUrlContains.ToLower())) {
    $match = $s
    break
  }
}
if (-not $match) { $match = $searchRes.value[0] }

Write-Host "Selected site:" -ForegroundColor Green
Write-Host "  displayName: $($match.displayName)"
Write-Host "  webUrl     : $($match.webUrl)"
Write-Host "  id         : $($match.id)"

# 3) Confirm by fetching site details by ID
$siteId = $match.id
$siteByIdUrl = "$graph/sites/$siteId?`$select=id,displayName,webUrl"
Write-Host "Confirming site by id: $siteByIdUrl" -ForegroundColor Yellow
$site = Invoke-RestMethod -Headers $headers -Uri $siteByIdUrl -Method Get

Write-Host "OK - Site resolved by ID" -ForegroundColor Green
Write-Host "  id         : $($site.id)"
Write-Host "  displayName: $($site.displayName)"
Write-Host "  webUrl     : $($site.webUrl)"
