# ================================
# Graph App Permission Check Script
# ================================

$tenantId     = "YOUR_TENANT_GUID"
$clientId     = "YOUR_APP_GUID"
$clientSecret = "YOUR_CLIENT_SECRET"

$siteUrl = "https://contoso.sharepoint.com/sites/MySite"
$libraryName = "Documents"
$tempFolder  = "_graphlib-perm-test"

Write-Host "Getting app-only token..." -ForegroundColor Cyan

$tokenResp = Invoke-RestMethod -Method Post `
  -Uri "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token" `
  -ContentType "application/x-www-form-urlencoded" `
  -Body @{
    client_id     = $clientId
    client_secret = $clientSecret
    scope         = "https://graph.microsoft.com/.default"
    grant_type    = "client_credentials"
  }

$token = $tokenResp.access_token
$headers = @{ Authorization = "Bearer $token" }

function Test-Step($name, $scriptBlock) {
  Write-Host "`n[$name]" -ForegroundColor Yellow
  try {
    & $scriptBlock
    Write-Host "OK" -ForegroundColor Green
  }
  catch {
    Write-Host "FAIL" -ForegroundColor Red
    Write-Host $_.Exception.Message
    if ($_.ErrorDetails.Message) {
      Write-Host $_.ErrorDetails.Message
    }
  }
}

# ----------------
# 1) Basic Graph call
# ----------------
Test-Step "Graph access (GET /organization)" {
  Invoke-RestMethod -Headers $headers `
    -Uri "https://graph.microsoft.com/v1.0/organization?`$select=id"
}

# ----------------
# 2) Resolve site
# ----------------
$u = [uri]$siteUrl
$hostName = $u.Host
$serverRel = $u.AbsolutePath

$siteId = $null

Test-Step "Resolve SharePoint site" {
  $site = Invoke-RestMethod -Headers $headers `
    -Uri "https://graph.microsoft.com/v1.0/sites/$hostName:$serverRel?`$select=id"
  $script:siteId = $site.id
  Write-Host "siteId=$siteId"
}

# ----------------
# 3) List drives (libraries)
# ----------------
$driveId = $null

Test-Step "List document libraries" {
  $drives = Invoke-RestMethod -Headers $headers `
    -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/drives?`$select=id,name"

  $drives.value | Format-Table name,id

  $script:driveId = ($drives.value | Where-Object name -eq $libraryName).id
  if (-not $driveId) {
    throw "Library '$libraryName' not found"
  }
}

# ----------------
# 4) Read root of library
# ----------------
Test-Step "Read library root" {
  Invoke-RestMethod -Headers $headers `
    -Uri "https://graph.microsoft.com/v1.0/drives/$driveId/root?`$select=id,name"
}

# ----------------
# 5) Create folder (write test)
# ----------------
Test-Step "Create folder in library" {
  $body = @{
    name  = $tempFolder
    folder = @{}
    "@microsoft.graph.conflictBehavior" = "rename"
  } | ConvertTo-Json

  Invoke-RestMethod -Method Post -Headers $headers `
    -Uri "https://graph.microsoft.com/v1.0/drives/$driveId/root/children" `
    -Body $body -ContentType "application/json"
}

# ----------------
# 6) Upload file (write test)
# ----------------
Test-Step "Upload file" {
  $bytes = [Text.Encoding]::UTF8.GetBytes("permission test " + (Get-Date))
  $uploadUrl = "https://graph.microsoft.com/v1.0/drives/$driveId/root:/$tempFolder/perm-test.txt:/content"

  Invoke-RestMethod -Method Put -Headers $headers `
    -Uri $uploadUrl `
    -Body $bytes `
    -ContentType "application/octet-stream"
}

Write-Host "`nPermission check complete." -ForegroundColor Cyan
