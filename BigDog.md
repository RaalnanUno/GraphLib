Hi [BigDog Name],

I’m ready to start porting **GraphLib** over for our use, but we’re currently blocked on the **SharePoint + Microsoft Graph (app-only)** setup required for the PDF conversion pipeline.

**TL;DR**

* We need a **SharePoint site + document library** that GraphLib can access, and an **Azure App Registration** for Microsoft Graph (client credentials).
* Once those pieces are in place, I can point the runner at the site/library and begin validating the conversion flow immediately.
* This email includes a simple task list so the server/admin team has a clear “do-this, then-that” checklist.

---

**Why this is needed (high level)**
GraphLib’s PDF conversion runner uses Microsoft Graph to:

1. Resolve the SharePoint site ID from a Site URL
2. Resolve the document library (Drive) ID
3. Ensure a temp folder exists (ex: “_graphlib-temp”)
4. Upload a file to the temp folder
5. Download the converted PDF using Graph “?format=pdf”

It’s all app-only (no user login) using MSAL client credentials (TenantId / ClientId / ClientSecret). We are *not* asking for tenant-wide access. We can and should scope access to a specific site/library.

---

**What we need from the server/admin team (technical task list)**

**A) SharePoint target**

1. Confirm/create a SharePoint site for GraphLib (or confirm an existing one).
2. Confirm/create a document library (default name “Documents” is fine).
3. Confirm/create a temp folder name to be used by the pipeline: *“_graphlib-temp”* (GraphLib can create this automatically, but it helps if they know it will appear).
4. Provide me:

   * **Site URL** (example format: [https://tenant.sharepoint.com/sites/GraphLib](https://tenant.sharepoint.com/sites/GraphLib))
   * **Library name** (example: Documents)

**B) Azure App Registration (Microsoft Graph, app-only)**

1. Create/identify an Azure AD App Registration for GraphLib.
2. Create a **Client Secret** (or preferred credential method per policy).
3. Provide me:

   * **Tenant ID**
   * **Client ID**
   * **Client Secret value** (delivered via approved secure channel)

**C) Permissions (least privilege)**

1. Grant GraphLib app access only to the approved SharePoint site/library (least privilege).
2. Ensure the app has the ability to:

   * Read site and drives metadata
   * Create a folder (if missing)
   * Upload a file into the temp folder
   * Download the converted PDF content

**D) Validation (quick smoke test)**
Once A–C are done, I can run a smoke test that:

* uploads a sample .docx
* downloads the PDF bytes
* confirms the pipeline end-to-end

If the server/admin team wants to verify independently, I can provide a minimal command/runbook, but I don’t want them to have to reverse-engineer the flow—this email is meant to be the checklist.

---

**What I’ll do once this is ready**

* Plug the provided values into the GraphLib settings (SiteUrl / LibraryName / TenantId / ClientId / ClientSecret).
* Run the conversion runner against a small set of test documents.
* Report results and log details (including Graph request correlation IDs if anything fails).

---

**Notes**

* This is not something the team does every day, so the biggest blocker is simply having a clear checklist. That’s what the task list above is for.
* Once access is granted, we can iterate quickly and stabilize the port.

Thanks,
Rahsaan
