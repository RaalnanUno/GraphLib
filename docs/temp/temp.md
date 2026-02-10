Got it — this should be a **clean, non-controversial implementation item**, not a research spike and not a feature pitch.

This fits best as a **Task** (or **Implementation Task**, depending on how your board is configured), because:

* Scope is **known**
* APIs are **validated**
* Output is **deterministic**
* No product decision required

Below is a **board-ready work item** you can paste straight in.

---

## 🧾 Work Item

**Type:** Task
**Area/Path:** GraphLib → PDF Conversion
**Iteration:** (current sprint)
**Priority:** Medium
**Risk:** Low
**Blocked:** No

---

## Title

Implement Microsoft Graph reliable document-to-PDF conversions

---

## Description

Implement Microsoft Graph–based document conversion support for **known reliable formats** as part of the GraphLib PDF pipeline.

This task formalizes and implements the subset of Microsoft Graph conversions that are **production-safe and deterministic**, and explicitly documents unsupported or unreliable formats to prevent misuse.

The implementation will use **app-only authentication** and SharePoint/OneDrive–hosted files, with conversions executed server-side by Microsoft Graph.

---

## In Scope

Implement conversion helpers for the following **reliable conversions**:

* DOCX → PDF
* XLSX → PDF
* PPTX → PDF
* DOCX → HTML
* DOCX → TXT
* XLSX → CSV (first worksheet only)

Conversions will be executed via:

```
GET /drives/{drive-id}/items/{item-id}/content?format={target}
```

---

## Out of Scope

* PDF → anything (Graph does not support reverse PDF conversions)
* OCR
* Image extraction from PDFs
* LibreOffice / OpenOffice processing
* Cross-Office conversions (e.g., Excel → Word)
* Multi-worksheet CSV exports

These are intentionally excluded and will remain fallback-only.

---

## Technical Notes

* Authentication: **App-only (client credentials)**
* Permissions: `Sites.Selected` (preferred) or equivalent scoped access
* Files must exist in **SharePoint or OneDrive**
* Output is streamed; persistence handled by GraphLib
* Conversion failures should surface as actionable errors (403 = permissions, 415 = unsupported format, etc.)

---

## Acceptance Criteria

* [ ] GraphLib exposes a helper capable of converting DOCX/XLSX/PPTX → PDF
* [ ] Non-PDF conversions (HTML/TXT/CSV) are supported where applicable
* [ ] Unsupported conversions are explicitly rejected with clear messaging
* [ ] Conversion logic uses DriveItem ID–based access (path optional)
* [ ] Code is app-only compatible (no delegated user context)
* [ ] Implementation documented for future maintainers

---

## Validation / Test Plan

* Upload known DOCX, XLSX, PPTX files to a test SharePoint library
* Convert each to PDF and verify:

  * File opens
  * Page count matches source
  * No blank or truncated output
* Validate CSV export uses only the first worksheet
* Confirm PDF inputs are rejected with a clear error

---

## Definition of Done

* Code merged
* Tested against SharePoint library
* Board item updated with supported conversion matrix
* No reliance on local Office, OpenOffice, or LibreOffice

---

If you want, next we can:

* Split this into **Task + Documentation Task**
* Add a **fallback decision matrix** as a follow-up item
* Create a **security review companion task** explaining why this is safe under least-privilege

Just say the word and I’ll line it up cleanly.

