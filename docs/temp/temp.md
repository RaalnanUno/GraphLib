Yes — you can prove this **locally** by pulling an app-only token and inspecting the JWT **claims**. The key thing you’re looking for is the **`roles`** claim (application permissions). If `roles` is missing (or doesn’t include a Sites permission), that explains the 401 at `resolve_site`.

Below are two practical options: **PowerShell-only** (no extra tools) and **Azure CLI**.

---

## Option A — PowerShell (recommended, zero dependencies)

### 1) Get an app-only access token (client credentials)

```powershell
$tenantId = "YOUR_TENANT_GUID"
$clientId = "YOUR_APP_GUID"
$clientSecret = "YOUR_SECRET_VALUE"  # don't commit this anywhere

$body = @{
  client_id     = $clientId
  client_secret = $clientSecret
  scope         = "https://graph.microsoft.com/.default"
  grant_type    = "client_credentials"
}

$tokenResp = Invoke-RestMethod -Method Post `
  -Uri "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token" `
  -ContentType "application/x-www-form-urlencoded" `
  -Body $body

$accessToken = $tokenResp.access_token
$accessToken.Length
```

### 2) Decode the token payload and print permissions (`roles`)

```powershell
function Decode-JwtPayload {
  param([string]$Jwt)

  $parts = $Jwt.Split('.')
  if ($parts.Length -lt 2) { throw "Not a JWT" }

  $p = $parts[1].Replace('-', '+').Replace('_', '/')
  switch ($p.Length % 4) { 0 { } 2 { $p += '==' } 3 { $p += '=' } default { throw "Bad base64 length" } }

  $json = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($p))
  return $json | ConvertFrom-Json
}

$payload = Decode-JwtPayload $accessToken

"aud  = $($payload.aud)"
"tid  = $($payload.tid)"
"appid= $($payload.appid)"

"roles:"
$payload.roles
```

### What you should see

* `aud` should be Graph (`https://graph.microsoft.com` or the Graph app GUID)
* `roles` should include something like:

  * `Sites.Read.All` **or**
  * `Sites.ReadWrite.All` **or**
  * `Sites.FullControl.All` **or**
  * `Sites.Selected`

✅ If **`roles` is empty / missing**, the app does *not* have Graph Application permissions consented properly.

---

## Option B — Prove it by calling the failing endpoint directly (same token)

This reproduces your `resolve_site` call with a raw request:

```powershell
$siteUrl = "https://contoso.sharepoint.com/sites/MySite"
$u = [Uri]$siteUrl

$host = $u.Host
$path = $u.AbsolutePath.TrimStart('/')

$graphUrl = "https://graph.microsoft.com/v1.0/sites/$host:/$path`?`$select=id"

Invoke-RestMethod -Method Get -Uri $graphUrl -Headers @{
  Authorization = "Bearer $accessToken"
}
```

* If this returns **401/403**, it’s permissions / site access.
* If it returns JSON with an `id`, your `resolve_site` step should work.

---

## Option C — Azure CLI (if you already have it installed)

This is handy, but I usually avoid it for client secrets on the command line. Still, it exists:

```powershell
az login
az account get-access-token --resource-type ms-graph --tenant YOUR_TENANT_GUID
```

⚠️ This typically returns a token for *your user* (delegated), not the app-only service principal—so it’s not always apples-to-apples with your app’s client-credentials flow.

---

## Two important notes (so expectations are right)

1. **The token only shows what’s been granted to the app in Entra ID** (via `roles`).
   It does **not** prove SharePoint-level access if you’re using **`Sites.Selected`**.

2. If you see `Sites.Selected` in `roles` but the site call fails, the missing step is:

* explicitly granting that app access to **that specific SharePoint site**.

---

If you paste the output of just these lines (redact IDs if you want):

* `aud`
* `tid`
* the list under `roles`

…I can tell you immediately whether it’s:

* wrong permission type (delegated vs application),
* missing admin consent,
* or `Sites.Selected` missing the site grant.
