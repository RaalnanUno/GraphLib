Yes — you can, and it fits your current flow nicely.

Right now GraphLib does:

1. Upload original file to `TempFolder`
2. Download PDF bytes (`?format=pdf`)
3. Return bytes to the caller

To **save the PDF “next to the file” in SharePoint**, you add one more step:

4. **Upload the PDF bytes back into the same folder**, using the same `driveId` and a sibling filename like `Alpha.pdf`.

---

## What “next to the file” means in Graph terms

If the source file was uploaded to:

`/{TempFolder}/{fi.Name}`

Then “next to it” means:

`/{TempFolder}/{baseName}.pdf`

Example:

* Source: `_graphlib-temp/Alpha.docx`
* Output: `_graphlib-temp/Alpha.pdf`

(If you later support converting files that already exist elsewhere, “next to” would mean the **same parent folder as that item**, not necessarily the temp folder.)

---

## Minimal code change: add an optional “savePdfToSharePoint” setting

### 1) Add settings

```cs
public sealed class GraphLibSettings
{
    // ...existing...
    public bool SavePdfToSharePoint { get; set; } = false;

    // Optional: suffix/behavior
    public string PdfFileNameSuffix { get; set; } = ".pdf";
}
```

### 2) Add an upload helper for the PDF bytes

You can reuse `UploadToFolderAsync`—just call it again with a `.pdf` name.

```cs
private static string ChangeExtensionToPdf(string fileName)
{
    var baseName = Path.GetFileNameWithoutExtension(fileName);
    return baseName + ".pdf";
}
```

### 3) In `ConvertFileToPdfAsync`, after you download `pdfBytes`

Right after:

```cs
var pdfBytes = await DownloadPdfAsync(driveId, itemId, clientRequestId, ct).ConfigureAwait(false);
```

Add:

```cs
string? pdfItemId = null;

if (_settings.SavePdfToSharePoint)
{
    var pdfName = ChangeExtensionToPdf(fi.Name);

    result.AddLog(LogLevel.Info, GraphStage.Upload, $"Uploading PDF '{pdfName}' next to source.");
    pdfItemId = await UploadToFolderAsync(
        driveId,
        tempFolderPath,
        pdfName,
        pdfBytes,
        _settings.ConflictBehavior,
        clientRequestId,
        ct).ConfigureAwait(false);

    result.AddLog(LogLevel.Info, GraphStage.Upload, $"Uploaded PDF. pdfItemId='{pdfItemId}'.");
}
```

If you want, store `pdfItemId` on the result object so callers can reference it.

---

## One important behavior decision

### Do you want the PDF to be saved…

1. **Next to the uploaded temp copy** (what you’re doing today) ✅ easiest
   or
2. **Next to the original file in its original library folder** (harder, but “truer” next-to)

To do (2), you’d need either:

* the original `driveId + itemId`, or
* the original folder path, or
* `parentReference` for the original item

Your current runner starts from a local file path, so it *doesn’t know* a “real original SharePoint location” unless you add that concept.

---

## Also: Graph convert limits

Graph’s `?format=pdf` conversion is intended for Office files. If you upload `.txt`, you might still get bytes back, but it won’t be a meaningful “converted PDF” in the same way a `.docx` is.

---

If you tell me which interpretation you mean (PDF next to the **temp upload** vs next to the **original SharePoint file**), I’ll give you the exact shape of the method and result fields to support it cleanly.
