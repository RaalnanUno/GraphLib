```json
{
  "info": {
    "_postman_id": "6c3d8e7b-6f8a-4a18-8b60-7f4d7b7d1a11",
    "name": "GraphLib — SharePoint PDF Download (App-only)",
    "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json",
    "description": "App-only (client credentials) Postman collection to resolve SharePoint site + library (drive) and download an existing Office file as PDF using Microsoft Graph. Variables are wired end-to-end."
  },
  "item": [
    {
      "name": "01 - Get app-only token (client_credentials)",
      "request": {
        "method": "POST",
        "header": [
          {
            "key": "Content-Type",
            "value": "application/x-www-form-urlencoded"
          }
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
        },
        "description": "Gets an app-only access token for Microsoft Graph (.default scope). Stores {{token}}."
      },
      "event": [
        {
          "listen": "test",
          "script": {
            "exec": [
              "const j = pm.response.json();",
              "pm.collectionVariables.set('token', j.access_token);",
              "pm.collectionVariables.set('token_type', j.token_type || 'Bearer');",
              "if (j.expires_in) pm.collectionVariables.set('token_expires_in', String(j.expires_in));",
              "pm.test('Token acquired', function () {",
              "  pm.expect(pm.collectionVariables.get('token')).to.be.ok;",
              "});"
            ],
            "type": "text/javascript"
          }
        }
      ]
    },
    {
      "name": "02 - Resolve siteId from siteUrl",
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
        },
        "description": "Resolves the SharePoint site ID using host + server-relative path. Stores {{siteId}}."
      },
      "event": [
        {
          "listen": "prerequest",
          "script": {
            "exec": [
              "if (!pm.collectionVariables.get('token')) {",
              "  console.warn('No token set. Run 01 - Get app-only token first.');",
              "}"
            ],
            "type": "text/javascript"
          }
        },
        {
          "listen": "test",
          "script": {
            "exec": [
              "const j = pm.response.json();",
              "pm.collectionVariables.set('siteId', j.id);",
              "pm.collectionVariables.set('siteDisplayName', j.displayName || '');",
              "pm.collectionVariables.set('siteWebUrl', j.webUrl || '');",
              "pm.test('siteId resolved', function () {",
              "  pm.expect(pm.collectionVariables.get('siteId')).to.be.ok;",
              "});"
            ],
            "type": "text/javascript"
          }
        }
      ]
    },
    {
      "name": "03 - List drives (document libraries) for site",
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
        },
        "description": "Lists document libraries (drives) on the site. Picks the drive matching {{libraryName}} and stores {{driveId}}."
      },
      "event": [
        {
          "listen": "test",
          "script": {
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
              "pm.test('driveId resolved', function () {",
              "  pm.expect(pm.collectionVariables.get('driveId')).to.be.ok;",
              "});"
            ],
            "type": "text/javascript"
          }
        }
      ]
    },
    {
      "name": "04 - Get itemId by filePath (inside library)",
      "request": {
        "method": "GET",
        "header": [
          { "key": "Authorization", "value": "Bearer {{token}}" }
        ],
        "url": {
          "raw": "https://graph.microsoft.com/v1.0/drives/{{driveId}}/root:{{filePath}}?$select=id,name,webUrl,size,file,folder",
          "protocol": "https",
          "host": ["graph", "microsoft", "com"],
          "path": ["v1.0", "drives", "{{driveId}}", "root:{{filePath}}"],
          "query": [
            { "key": "$select", "value": "id,name,webUrl,size,file,folder" }
          ]
        },
        "description": "Resolves an item's metadata (and itemId) by path inside the document library. Stores {{itemId}} and {{itemName}}."
      },
      "event": [
        {
          "listen": "test",
          "script": {
            "exec": [
              "const j = pm.response.json();",
              "pm.collectionVariables.set('itemId', j.id);",
              "pm.collectionVariables.set('itemName', j.name || '');",
              "pm.collectionVariables.set('itemWebUrl', j.webUrl || '');",
              "pm.test('itemId resolved', function () {",
              "  pm.expect(pm.collectionVariables.get('itemId')).to.be.ok;",
              "});"
            ],
            "type": "text/javascript"
          }
        }
      ]
    },
    {
      "name": "05 - Download PDF by filePath (recommended)",
      "request": {
        "method": "GET",
        "header": [
          { "key": "Authorization", "value": "Bearer {{token}}" }
        ],
        "url": {
          "raw": "https://graph.microsoft.com/v1.0/drives/{{driveId}}/root:{{filePath}}:/content?format=pdf",
          "protocol": "https",
          "host": ["graph", "microsoft", "com"],
          "path": ["v1.0", "drives", "{{driveId}}", "root:{{filePath}}:", "content"],
          "query": [
            { "key": "format", "value": "pdf" }
          ]
        },
        "description": "Downloads the file rendered as PDF by path. Use Postman 'Send and Download' to save the PDF.",
        "protocolProfileBehavior": {
          "followRedirects": true
        }
      }
    },
    {
      "name": "06 - Download PDF by itemId",
      "request": {
        "method": "GET",
        "header": [
          { "key": "Authorization", "value": "Bearer {{token}}" }
        ],
        "url": {
          "raw": "https://graph.microsoft.com/v1.0/drives/{{driveId}}/items/{{itemId}}/content?format=pdf",
          "protocol": "https",
          "host": ["graph", "microsoft", "com"],
          "path": ["v1.0", "drives", "{{driveId}}", "items", "{{itemId}}", "content"],
          "query": [
            { "key": "format", "value": "pdf" }
          ]
        },
        "description": "Downloads the file rendered as PDF by itemId. Use Postman 'Send and Download' to save the PDF.",
        "protocolProfileBehavior": {
          "followRedirects": true
        }
      }
    }
  ],
  "variable": [
    { "key": "tenantId", "value": "YOUR_TENANT_GUID" },
    { "key": "clientId", "value": "YOUR_APP_GUID" },
    { "key": "clientSecret", "value": "YOUR_CLIENT_SECRET" },

    { "key": "spHost", "value": "contoso.sharepoint.com" },
    { "key": "spPath", "value": "/sites/MySite" },
    { "key": "libraryName", "value": "Documents" },

    { "key": "filePath", "value": "/Shared Documents/YourFolder/YourFile.docx" },

    { "key": "token", "value": "" },
    { "key": "token_type", "value": "Bearer" },
    { "key": "token_expires_in", "value": "" },

    { "key": "siteId", "value": "" },
    { "key": "siteDisplayName", "value": "" },
    { "key": "siteWebUrl", "value": "" },

    { "key": "driveId", "value": "" },
    { "key": "driveWebUrl", "value": "" },

    { "key": "itemId", "value": "" },
    { "key": "itemName", "value": "" },
    { "key": "itemWebUrl", "value": "" }
  ]
}
```

### How to use it (quick)

1. Import this JSON as a Postman collection.
2. Edit the collection variables:

* `tenantId`, `clientId`, `clientSecret`
* `spHost`, `spPath`
* `libraryName`
* `filePath` *(path **inside the library root**)*

3. Run requests **01 → 05** in order.
4. On **05**, use **Send and Download** to save the PDF.

### Important note about `filePath`

In Graph, the `root:{{filePath}}` path is **relative to the document library root**, not the full SharePoint URL.

If your library is *Documents* and the file is in `_graphlib-temp/example.docx`, set:

* `filePath` = `/_graphlib-temp/example.docx`

If you paste what you see in the SharePoint UI (library + folders + file name), I’ll translate it into the exact `filePath` string that will work on the first try.
