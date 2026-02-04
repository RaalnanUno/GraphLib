$ErrorActionPreference = "Stop"

$tenantId     = "YOUR-TENANT-ID"
$clientId     = "YOUR-CLIENT-ID"
$clientSecret = "YOUR-CLIENT-SECRET"

# Paste the id returned from the Search script:
$siteId = "PASTE-SITE-ID-HERE"

$tokenUrl  = "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token"
$scope     = "https://graph.microsoft.com/.default"
$graphBase = "https://graph.microsoft.com/v1.0"

$token = Invoke-RestMethod -Method Post -Uri $tokenUrl -ContentType "application/x-www-form-urlencoded" -Body @{
  client_id     = $clientId
  client_secret = $clientSecret
  grant_type    = "client_credentials"
  scope         = $scope
}

$headers = @{ Authorization = "Bearer $($token.access_token)" }

$siteUrl = "$graphBase/sites/$siteId?`$select=id,displayName,webUrl"
Write-Host "Calling URL:" -ForegroundColor Yellow
Write-Host $siteUrl

$site = Invoke-RestMethod -Headers $headers -Uri $siteUrl -Method Get
$site | Format-List
