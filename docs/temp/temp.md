![[Pasted image 20260203153424.png]]

Perfect 👍 — here’s the **tight, no-nonsense checklist** you can drop straight into a ticket or email to the server team. This is written so they can follow it without interpretation.

---

## **Action Required: Fix Microsoft Graph App Permissions for SharePoint Access**

### **Purpose**

Enable app-only Microsoft Graph access to SharePoint sites for a service application that uploads documents and downloads PDF conversions.

---

## **Step-by-Step Fix (Entra ID / Azure AD)**

### **1. Open the App Registration**

* Azure Portal → **Entra ID**
* **App registrations**
* Select the application used by the PDF conversion service

---

### **2. Verify API Permission Type**

Go to **API permissions**

* Confirm permissions are under:

  * ✅ **Microsoft Graph → Application permissions**
  * ❌ NOT “Delegated permissions”

---

### **3. Add Required Permission**

Under **Microsoft Graph → Application permissions**, add **one** of the following:

* **Sites.ReadWrite.All** *(recommended for upload + conversion)*

  * OR **Sites.Read.All** *(read-only scenarios)*
  * OR **Sites.Selected** *(only if site-scoped access is required)*

---

### **4. Grant Admin Consent**

* Click **“Grant admin consent for <Tenant>”**
* Confirm status shows **Granted for <Tenant>**

> ⚠️ Without admin consent, the permission does not apply to app-only tokens.

---

### **5. (Only if using Sites.Selected) Grant Site Access**

If **Sites.Selected** is used:

* Explicitly grant the app access to the target SharePoint site
* This can be done via:

  * Microsoft Graph
  * PowerShell
  * SharePoint Admin tooling

> Without this step, Graph will return 401/403 even with a valid token.

---

## **Validation**

After completing the above:

* Acquire a new access token (client credentials flow)
* Call:

  ```
  GET https://graph.microsoft.com/v1.0/sites/{tenant}.sharepoint.com:/{sitePath}?$select=id
  ```
* Expected result:

  * ✅ JSON response with a `site id`
  * ❌ 401/403 indicates permission or consent is still missing

---

## **Notes**

* Token acquisition succeeding **does not** imply authorization is correct.
* The failure is authorization-based, not a code defect.
* No application changes are required once permissions are corrected.

---

If you want, I can also provide:

* a **PowerShell script** the server team can run to verify permissions,
* or a **one-paragraph executive summary** suitable for leadership review.

You’ve done the hard part already — this checklist should let them fix it cleanly.
