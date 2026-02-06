A `403 Forbidden` on **the PUT /content upload** almost always means “the token is valid, but this app is **not allowed to write here** (or not allowed to write *to that specific folder/library*)”.

Since your earlier steps are succeeding (resolve site, resolve drive, and *sometimes* ensure folder), you’re past the “bad tenant/client/secret” class of problems. This is almost certainly **authorization scope** (app permission) *or* **SharePoint/library/folder-level permissions**.

## 1) First: make the error body visible (it will usually tell you exactly why)

Right now you throw a `GraphRequestException` with `ResponseBody`, but you’re not surfacing it in logs. The Graph error payload often includes:

* `error.code` like `accessDenied`
* a message like “Either scp or roles claim need to be present”
* SharePoint-specific hints (“The app does not have permission to access this resource”)

Add this in your `catch (GraphRequestException gre)` block in `ConvertFileToPdfAsync`:

```csharp
catch (GraphRequestException gre)
{
    result.AddLog(LogLevel.Error, gre.Stage, $"Status={(int)gre.StatusCode} RequestId={gre.RequestId} ClientRequestId={gre.ClientRequestId}");
    if (!string.IsNullOrWhiteSpace(gre.ResponseBody))
        result.AddLog(LogLevel.Error, gre.Stage, gre.ResponseBody);

    var msg = $"Graph request failed ({(int)gre.StatusCode}) during '{gre.Stage}'.";
    return await FailAsync(result, gre.Stage, msg, sw, gre, ct).ConfigureAwait(false);
}
```

That one change usually turns “Forbidden” into “Forbidden because **X**”.

## 2) What a 403 at **UploadToFolderAsync** usually means (ranked)

### A) **Sites.Selected** is configured but the app only has *Read* (or no site grant at all)

With **Sites.Selected**, the app registration having the permission is **not enough** — you must also grant the app access to the *specific site* (and the grant must include **write**).

Typical symptom:

* You can `GET` site/drive metadata (sometimes)
* You can read files / list
* Upload fails with 403

What to check:

* In Entra ID (Azure AD) App Registration → API permissions:

  * **Microsoft Graph → Application → Sites.Selected** (or Sites.ReadWrite.All)
  * Admin consent granted
* Then confirm the **site-level grant** exists and includes `write`.

How to grant (Graph API):
`POST /sites/{siteId}/permissions`

Body example (app-only grant):

```json
{
  "roles": ["write"],
  "grantedToIdentities": [
    {
      "application": {
        "id": "<YOUR-APP-CLIENT-ID>",
        "displayName": "<YOUR-APP-NAME>"
      }
    }
  ]
}
```

If your org uses PnP PowerShell, this is even easier:

* `Grant-PnPAzureADAppSitePermission -AppId <clientId> -Site <siteUrl> -Permissions Write`

### B) **Unique permissions on the library or folder**

Even if the app has write to the *site*, the **document library** or your `_graphlib-temp` folder might have broken inheritance and removed “Add Items”.

Typical symptom:

* Resolve site/drive works
* EnsureFolder may *skip creation* because the folder exists
* Upload fails 403 when targeting that folder

What to check (SharePoint side):

* Library permissions for the app principal (or “Everyone except external users” etc.)
* Folder `_graphlib-temp` permissions (inheritance broken?)
* “Add Items” / “Edit Items” allowed?

Easy test:

* Change `TempFolder` to a brand new folder name (e.g., `_graphlib-temp-test-<guid>`) so `EnsureFolderAsync` is forced to **create** it.

  * If folder creation succeeds but upload still fails → permission is specifically about file upload (rare).
  * If folder creation fails too → definitely missing write permission.

### C) The app has **Sites.Read.All** or equivalent, but not write

Sometimes the app was granted read to satisfy “resolve site/drive”, but not write.

You want one of these models:

* **Least privilege**: `Sites.Selected` + site grant `write`
* **Broad** (usually not acceptable): `Sites.ReadWrite.All`

### D) You’re uploading into the wrong drive/library

Your drive lookup is name-based:

```csharp
sites/{siteId}/drives?$select=id,name
```

If the library is actually named differently (common: “Documents” vs “Shared Documents”), you’d normally fail earlier with “No drive found…”. But if there are multiple drives and the JSON helper picks the wrong “id” (because it scans a window), you *could* resolve the wrong drive and then be forbidden on upload.

**Fix** if you suspect this: do a more deterministic parse for drive selection (or temporarily log the JSON returned by `/drives` so you can see exactly what name/id you’re selecting).

## 3) Quick “is it permissions?” sanity checks you can do without server monkeys

### Check the token has **roles** (app permissions)

An app-only token should have a `roles` claim (not `scp`). If it’s missing or wrong, Graph often returns 403.

Fast way:

* Copy the access token (temporarily) and decode it in a JWT viewer (internal tools, not public if that violates policy).
* Look for `roles`: should include something like `Sites.Selected` or `Sites.ReadWrite.All`.

### Try a write that doesn’t involve upload

For example, create a folder under root (you already do in `EnsureFolderAsync`).

* If `EnsureFolderAsync` successfully creates a new folder, then **write** is working at least at that location.
* If ensure only checks existence and never creates, you still don’t know.

So: set `TempFolder` to something guaranteed new to force a POST create.

## 4) What I’d send to the Server Monkeys (tight checklist)

1. Confirm App Registration has **Graph Application** permission:

   * `Sites.Selected` **(preferred)** or `Sites.ReadWrite.All`
   * Admin consent granted
2. If using `Sites.Selected`, confirm the app has a **site permission grant** with **Write** for the target site.
3. Confirm SharePoint library/folder permissions:

   * `_graphlib-temp` folder inherits permissions (or explicitly allows Add/Edit)
4. Provide the exact Graph failure details from `gre.ResponseBody`, plus `request-id` / `client-request-id` for traceability.

---

If you paste **just the `gre.ResponseBody`** from the thrown exception (the JSON string), I can tell you which of the above it is in one pass and what exact permission/grant is missing.
