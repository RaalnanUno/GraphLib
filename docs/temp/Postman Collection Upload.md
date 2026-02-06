Absolutely. Below is a **second Postman collection JSON** for **uploading** to your SharePoint document library (Drive) using Graph app-only.

It includes:

* Token
* Resolve siteId
* Resolve driveId (library)
* Ensure folder (creates `_graphlib-temp` if missing)
* Upload a file **from Postman** (binary upload)
* Optional: list folder contents

> **How Postman uploads the file:** you’ll pick a local file in Postman using **Body → binary**. Graph will upload those bytes into SharePoint.

---

```json
{
  "info": {
    "_postman_id": "f1b4f6b2-6a77-4e1e-9d41-8b8d9b9d6d2a",
    "name": "GraphLib — SharePoint Upload (App-only)",
    "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json",
    "description": "App-only (client credentials) Postman collection to resolve SharePoint site + library (drive), ensure a folder exists, and upload a file to that folder using Microsoft Graph."
  },
  "item": [
    {
      "name": "01 - Get app-only token (client_credentials)",
      "request": {
        "method": "POST",
        "header": [
          { "key": "Content-Type", "value": "application/x-www-form-urlencoded" }
        ],
        "body": {
          "mode": "urlencoded",
          "urlencoded": [
            { "key": "client_id", "value": "{{clientId}}", "type": "text" },
            { "key": "client_secret", "value": "{{clientSecret}}", "type": "text" },
            { "key": "scope", "value": "https://graph.microsoft.com/.default", "type": "text" },
            { "key": "grant_type", "value": "client_credentials", "type": "text" }
          ]
        },
        "url": {
          "raw": "https://login.microsoftonline.com/{{tenantId}}/oauth2/v2.0/token",
          "protocol": "https",
          "host": ["login", "microsoftonline", "com"],
          "path": ["{{tenantId}}", "oauth2", "v2.0", "token"]
        }
      },
      "event": [
        {
          "listen": "test",
          "script": {
            "type": "text/javascript",
            "exec": [
              "const j = pm.response.json();",
              "pm.collectionVariables.set('token', j.access_token);",
              "pm.test('Token acquired', function(){",
              "  pm.expect(pm.collectionVariables.get('token')).to.be.ok;",
              "});"
            ]
          }
        }
      ]
    },
    {
      "name": "02 - Resolve siteId from host + path",
      "request": {
        "method": "GET",
        "header": [
          { "key": "Authorization", "value": "Bearer {{token}}" }
        ],
        "url": {
          "raw": "https://graph.microsoft.com/v1.0/sites/{{spHost}}:{{spPath}}?$select=id,displayName,webUrl",
          "protocol": "https",
          "host": ["graph", "microsoft", "com"],
          "path": ["v1.0", "sites", "{{spHost}}:{{spPath}}"],
          "query": [
            { "key": "$select", "value": "id,displayName,webUrl" }
          ]
        }
      },
      "event": [
        {
          "listen": "test",
          "script": {
            "type": "text/javascript",
            "exec": [
              "const j = pm.response.json();",
              "pm.collectionVariables.set('siteId', j.id);",
              "pm.collectionVariables.set('siteWebUrl', j.webUrl || '');",
              "pm.test('siteId resolved', function(){",
              "  pm.expect(pm.collectionVariables.get('siteId')).to.be.ok;",
              "});"
            ]
          }
        }
      ]
    },
    {
      "name": "03 - List drives (libraries) and pick driveId by libraryName",
      "request": {
        "method": "GET",
        "header": [
          { "key": "Authorization", "value": "Bearer {{token}}" }
        ],
        "url": {
          "raw": "https://graph.microsoft.com/v1.0/sites/{{siteId}}/drives?$select=id,name,driveType,webUrl",
          "protocol": "https",
          "host": ["graph", "microsoft", "com"],
          "path": ["v1.0", "sites", "{{siteId}}", "drives"],
          "query": [
            { "key": "$select", "value": "id,name,driveType,webUrl" }
          ]
        }
      },
      "event": [
        {
          "listen": "test",
          "script": {
            "type": "text/javascript",
            "exec": [
              "const lib = (pm.collectionVariables.get('libraryName') || '').toLowerCase();",
              "const j = pm.response.json();",
              "const drives = j.value || [];",
              "const match = drives.find(d => (d.name || '').toLowerCase() === lib);",
              "if (!match) {",
              "  throw new Error(`Library not found. libraryName='${pm.collectionVariables.get('libraryName')}'. Available: ` + drives.map(d => d.name).join(', '));",
              "}",
              "pm.collectionVariables.set('driveId', match.id);",
              "pm.collectionVariables.set('driveWebUrl', match.webUrl || '');",
              "pm.test('driveId resolved', function(){",
              "  pm.expect(pm.collectionVariables.get('driveId')).to.be.ok;",
              "});"
            ]
          }
        }
      ]
    },
    {
      "name": "04 - Ensure folder exists (create if missing)",
      "request": {
        "method": "POST",
        "header": [
          { "key": "Authorization", "value": "Bearer {{token}}" },
          { "key": "Content-Type", "value": "application/json" }
        ],
        "body": {
          "mode": "raw",
          "raw": "{\n  \"name\": \"{{uploadFolder}}\",\n  \"folder\": {},\n  \"@microsoft.graph.conflictBehavior\": \"fail\"\n}"
        },
        "url": {
          "raw": "https://graph.microsoft.com/v1.0/drives/{{driveId}}/root/children",
          "protocol": "https",
          "host": ["graph", "microsoft", "com"],
          "path": ["v1.0", "drives", "{{driveId}}", "root", "children"]
        },
        "description": "Creates the folder at the library root if it doesn't already exist. If it already exists, this may return 409 (conflict) — that's OK."
      }
    },
    {
      "name": "05 - Upload file (simple upload to folder) — Body is binary",
      "request": {
        "method": "PUT",
        "header": [
          { "key": "Authorization", "value": "Bearer {{token}}" },
          { "key": "Content-Type", "value": "application/octet-stream" }
        ],
        "body": {
          "mode": "file",
          "file": {}
        },
        "url": {
          "raw": "https://graph.microsoft.com/v1.0/drives/{{driveId}}/root:/{{uploadFolder}}/{{uploadFileName}}:/content?@microsoft.graph.conflictBehavior={{conflictBehavior}}",
          "protocol": "https",
          "host": ["graph", "microsoft", "com"],
          "path": [
            "v1.0",
            "drives",
            "{{driveId}}",
            "root:/{{uploadFolder}}/{{uploadFileName}}:",
            "content"
          ],
          "query": [
            { "key": "@microsoft.graph.conflictBehavior", "value": "{{conflictBehavior}}" }
          ]
        },
        "description": "In Postman: Body → binary → Select a local file. The filename in SharePoint will be {{uploadFileName}} (not necessarily the local file name)."
      },
      "event": [
        {
          "listen": "test",
          "script": {
            "type": "text/javascript",
            "exec": [
              "const j = pm.response.json();",
              "pm.collectionVariables.set('uploadedItemId', j.id || '');",
              "pm.collectionVariables.set('uploadedWebUrl', j.webUrl || '');",
              "pm.test('Upload returned an item id', function(){",
              "  pm.expect(pm.collectionVariables.get('uploadedItemId')).to.be.ok;",
              "});"
            ]
          }
        }
      ]
    },
    {
      "name": "06 - List uploaded folder contents",
      "request": {
        "method": "GET",
        "header": [
          { "key": "Authorization", "value": "Bearer {{token}}" }
        ],
        "url": {
          "raw": "https://graph.microsoft.com/v1.0/drives/{{driveId}}/root:/{{uploadFolder}}:/children?$select=name,id,size,webUrl",
          "protocol": "https",
          "host": ["graph", "microsoft", "com"],
          "path": ["v1.0", "drives", "{{driveId}}", "root:/{{uploadFolder}}:", "children"],
          "query": [
            { "key": "$select", "value": "name,id,size,webUrl" }
          ]
        }
      }
    }
  ],
  "variable": [
    { "key": "tenantId", "value": "YOUR_TENANT_GUID" },
    { "key": "clientId", "value": "YOUR_APP_GUID" },
    { "key": "clientSecret", "value": "YOUR_CLIENT_SECRET" },

    { "key": "spHost", "value": "FDICDev.sharepoint.com" },
    { "key": "spPath", "value": "/sites/EVAuto" },

    { "key": "libraryName", "value": "GraphLib" },

    { "key": "uploadFolder", "value": "_graphlib-temp" },
    { "key": "uploadFileName", "value": "Alpha.docx" },
    { "key": "conflictBehavior", "value": "replace" },

    { "key": "token", "value": "" },

    { "key": "siteId", "value": "" },
    { "key": "siteWebUrl", "value": "" },

    { "key": "driveId", "value": "" },
    { "key": "driveWebUrl", "value": "" },

    { "key": "uploadedItemId", "value": "" },
    { "key": "uploadedWebUrl", "value": "" }
  ]
}
```

### How to use it

1. Import JSON into Postman
2. Update collection variables:

* `tenantId`, `clientId`, `clientSecret`
* `spHost` (likely `fdicdev.sharepoint.com` is correct)
* `spPath` (you said site is `/sites/EVAuto`)
* `libraryName` = `GraphLib`
* `uploadFolder` (ex `_graphlib-temp`)
* `uploadFileName` (what you want it named in SharePoint)

3. Run **01 → 03**
4. Run **04** (if it errors with **409 Conflict**, that’s fine — folder already exists)
5. Open **05**, set **Body → binary**, choose a local file, then **Send**.

If you want, I can also add an **upload + convert + download PDF** request at the end, so the collection does the full GraphLib pipeline end-to-end.
