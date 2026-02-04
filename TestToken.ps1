# TestToken.ps1
# Purpose: Acquire app-only token and test Graph access to a specific SharePoint site.
# Notes:
# - Do NOT use $host as a variable name (PowerShell reserves $Host).
# - For Sites.Selected, you must ALSO grant the app access to the target site.

$ErrorActionPreference = "Stop"

# -------------------------
# CONFIG (fill these in)
# -------------------------
$tenantId     = "YOUR-TENANT-ID-GUID"
$clientId     = "YOUR-CLIENT-ID-GUID"
$clientSecret = "YOUR-CLIENT-SECRET"

# SharePoint host should be ONLY the hostname, no https:// and no path
$spoHost  = "contoso.sharepoint.com"

# Site path, starting with /sites/... (or /teams/...)
$sitePath = "/sites/apdb/EVAuto"

# -------------------------
# 1) Get access token
# -------------------------
$tokenUrl = "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token"

$tokenBody = @{
  client_id     = $clientId
  client_secret = $clientSecret
  grant_type    = "client_credentials"
  scope         = "https://graph.microsoft.com/.default"
}

Write-Host "Requesting token..." -ForegroundColor Cyan
$tokenRes = Invoke-RestMethod -Method Post -Uri $tokenUrl -Body $tokenBody -ContentType "application/x-www-form-urlencoded"

$accessToken = $tokenRes.access_token
Write-Host "Token acquired. Expires in (sec): $($tokenRes.expires_in)" -ForegroundColor Green

# Optional: quick peek at token payload (no validation, just decode)
function Decode-JwtPayload([string]$jwt) {
  $parts = $jwt.Split(".")
  if ($parts.Length -lt 2) { return $null }
  $p = $parts[1].Replace('-', '+').Replace('_', '/')
  switch ($p.Length % 4) { 2 { $p += '==' } 3 { $p += '=' } }
  $json = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($p))
  return $json | ConvertFrom-Json
}

$payload = Decode-JwtPayload $accessToken
if ($payload) {
  Write-Host "Token aud: $($payload.aud)"
  if ($payload.roles) { Write-Host "Token roles: $($payload.roles -join ', ')" }
}

$headers = @{
  Authorization = "Bearer $accessToken"
}

# -------------------------
# 2) Resolve Site by path (recommended)
# -------------------------
# Correct format:
#   /sites/{hostname}:/sites/...   (hostname ONLY)
$siteResolveUrl = "https://graph.microsoft.com/v1.0/sites/$spoHost:$sitePath?`$select=id,displayName,webUrl"

Write-Host "Resolving site..." -ForegroundColor Cyan
Write-Host "Graph URL: $siteResolveUrl"

$site = Invoke-RestMethod -Headers $headers -Uri $siteResolveUrl -Method Get
Write-Host "Site resolved:" -ForegroundColor Green
Write-Host "  id: $($site.id)"
Write-Host "  displayName: $($site.displayName)"
Write-Host "  webUrl: $($site.webUrl)"

# -------------------------
# 3) Simple follow-up call (example)
# -------------------------
$drivesUrl = "https://graph.microsoft.com/v1.0/sites/$($site.id)/drives?`$select=id,name"
Write-Host "Listing drives..." -ForegroundColor Cyan
$drives = Invoke-RestMethod -Headers $headers -Uri $drivesUrl -Method Get
$drives.value | ForEach-Object { Write-Host "  Drive: $($_.name)  id=$($_.id)" }

Write-Host "DONE" -ForegroundColor Green
