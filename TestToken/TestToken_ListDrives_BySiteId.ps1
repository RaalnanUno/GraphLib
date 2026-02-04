$ErrorActionPreference = "Stop"

$tenantId     = "YOUR-TENANT-ID"
$clientId     = "YOUR-CLIENT-ID"
$clientSecret = "YOUR-CLIENT-SECRET"

# Paste the exact id returned by search:
$siteId = "fdicdev.sharepoint.com,GUID1,GUID2"

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

$drivesUrl = "$graphBase/sites/$siteId/drives?`$select=id,name,webUrl"
Write-Host "Calling URL:" -ForegroundColor Yellow
Write-Host $drivesUrl

$drives = Invoke-RestMethod -Headers $headers -Uri $drivesUrl -Method Get

$drives.value | ForEach-Object {
  Write-Host "$($_.name) :: $($_.webUrl) :: $($_.id)"
}
