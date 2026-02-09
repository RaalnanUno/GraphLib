Below is a **single Markdown “drop-in” file** that contains the entire minimal project (a tiny **WinForms** app) that:

- accepts a **SQL Server connection string**
    
- has a **file picker**
    
- uploads the selected file into the **EV** database table you point it at
    
- writes the file bytes into **Docs_Blob_MO (image)** and sets **GetDate()** timestamps on insert
    

> ✅ I’m assuming the table is something like `dbo.Docs` (or similar). Since you didn’t give the table name, this file makes it **one constant** you can change in one place.

---

````markdown
# EV Doc Uploader (Minimal WinForms)

This is a minimal Windows desktop app that:
- accepts a SQL Server connection string
- lets you pick a file
- inserts a record into an EV database table
- stores file binary in `Docs_Blob_MO` (SQL `image`)
- uses `GETDATE()` for `Docs_Crea_ts` and `Docs_last_updt_ts`

---

## 1) Create the project

Open PowerShell in the folder you want and run:

```powershell
dotnet new winforms -n EvDocUploader -f net8.0
cd EvDocUploader
dotnet add package Microsoft.Data.SqlClient
````

> If you’re on older .NET, tell me what version and I’ll adjust (net6.0 / net48).

---

## 2) Files to create/replace

### File: `EvDocUploader.csproj`

Replace the entire file with this:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

---

### File: `Program.cs`

Replace the entire file with this:

```csharp
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text;

namespace EvDocUploader;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

public sealed class MainForm : Form
{
    // ✅ Change this to the real table name
    private const string TargetTable = "dbo.Docs";

    private readonly TextBox _txtConn;
    private readonly TextBox _txtFile;
    private readonly TextBox _txtDctyCd;
    private readonly TextBox _txtSrceTblCd;
    private readonly NumericUpDown _numSrceId;
    private readonly Button _btnBrowse;
    private readonly Button _btnUpload;
    private readonly Label _lblStatus;

    public MainForm()
    {
        Text = "EV Doc Uploader (Minimal)";
        Width = 820;
        Height = 320;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        var lblConn = new Label { Text = "Connection String:", Left = 12, Top = 18, Width = 140 };
        _txtConn = new TextBox { Left = 160, Top = 14, Width = 630, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        _txtConn.PlaceholderText = "Paste SQL Server connection string here...";

        var lblFile = new Label { Text = "File:", Left = 12, Top = 58, Width = 140 };
        _txtFile = new TextBox { Left = 160, Top = 54, Width = 520, ReadOnly = true };
        _btnBrowse = new Button { Text = "Browse...", Left = 690, Top = 52, Width = 100 };
        _btnBrowse.Click += (_, _) => BrowseFile();

        var lblDcty = new Label { Text = "docs_dcty_cd (char(4)):", Left = 12, Top = 98, Width = 140 };
        _txtDctyCd = new TextBox { Left = 160, Top = 94, Width = 120, Text = "FILE" }; // default

        var lblSrcTbl = new Label { Text = "docs_srce_tbl_cd (char(6)):", Left = 300, Top = 98, Width = 170 };
        _txtSrceTblCd = new TextBox { Left = 480, Top = 94, Width = 120, Text = "MANUAL" }; // default (trimmed on save)

        var lblSrcId = new Label { Text = "docs_srce_id (numeric(12,0)):", Left = 610, Top = 98, Width = 180 };
        _numSrceId = new NumericUpDown
        {
            Left = 610,
            Top = 122,
            Width = 180,
            Minimum = 0,
            Maximum = 999999999999,
            Value = 0
        };

        _btnUpload = new Button { Text = "Upload", Left = 690, Top = 160, Width = 100, Height = 34 };
        _btnUpload.Click += async (_, _) => await UploadAsync();

        _lblStatus = new Label { Left = 12, Top = 210, Width = 780, Height = 60 };
        _lblStatus.Text = "Status: Ready";

        Controls.AddRange(new Control[]
        {
            lblConn, _txtConn,
            lblFile, _txtFile, _btnBrowse,
            lblDcty, _txtDctyCd,
            lblSrcTbl, _txtSrceTblCd,
            lblSrcId, _numSrceId,
            _btnUpload,
            _lblStatus
        });

        // Layout tweak for srcId label to align nicely
        lblSrcId.Top = 126;
    }

    private void BrowseFile()
    {
        using var ofd = new OpenFileDialog
        {
            Title = "Select a file to upload",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false
        };

        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
            _txtFile.Text = ofd.FileName;
            SetStatus("Ready to upload selected file.");
        }
    }

    private async Task UploadAsync()
    {
        try
        {
            var connStr = _txtConn.Text?.Trim();
            if (string.IsNullOrWhiteSpace(connStr))
            {
                SetStatus("ERROR: Connection string is required.");
                return;
            }

            var fullPath = _txtFile.Text?.Trim();
            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            {
                SetStatus("ERROR: Please choose a valid file.");
                return;
            }

            var fileNameOnly = Path.GetFileName(fullPath);
            var bytes = await File.ReadAllBytesAsync(fullPath);
            var fileSize = (long)bytes.Length;

            // Normalize fixed-length char fields (pad/trim on SQL side too, but we keep it clean here)
            var dcty = NormalizeFixedChar(_txtDctyCd.Text, 4);
            var srcTbl = NormalizeFixedChar(_txtSrceTblCd.Text, 6);
            var srcId = (long)_numSrceId.Value;

            SetUiEnabled(false);
            SetStatus("Uploading...");

            // Generate a 12-digit numeric ID (0..999,999,999,999). Retry a few times on collision.
            const int maxAttempts = 5;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var docsId = GenerateNumeric12();

                var rows = await InsertDocAsync(
                    connStr,
                    docsId,
                    fileNameOnly,
                    fileSize,
                    dcty,
                    srcTbl,
                    srcId,
                    bytes);

                if (rows == 1)
                {
                    SetStatus($"SUCCESS: Uploaded '{fileNameOnly}' as Docs_ID={docsId} (bytes={fileSize}).");
                    return;
                }

                // If 0 rows affected, treat as unexpected.
                SetStatus($"ERROR: Insert affected {rows} rows (attempt {attempt}/{maxAttempts}).");
            }

            SetStatus("ERROR: Failed to insert after multiple attempts (possible ID collisions or constraint issues).");
        }
        catch (SqlException ex)
        {
            SetStatus("SQL ERROR:\r\n" + ex.Message);
        }
        catch (Exception ex)
        {
            SetStatus("ERROR:\r\n" + ex);
        }
        finally
        {
            SetUiEnabled(true);
        }
    }

    private static async Task<int> InsertDocAsync(
        string connStr,
        long docsId,
        string fileNameOnly,
        long fileSize,
        string docsDctyCd,
        string docsSrceTblCd,
        long docsSrceId,
        byte[] blob)
    {
        // NOTE:
        // - Docs_Crea_ts and Docs_last_updt_ts use GETDATE() per your requirement
        // - Docs_Blob_MO is SQL `image` -> parameter type SqlDbType.Image is correct
        //
        // If your real table name/column names differ in casing or underscores,
        // adjust the SQL below accordingly.

        var sql = $@"
INSERT INTO {TargetTable}
(
    Docs_ID,
    Docs_File_Nm,
    docs_cuml_size_nr,
    docs_dcty_cd,
    docs_srce_tbl_cd,
    docs_srce_id,
    Docs_Crea_ts,
    Docs_last_updt_ts,
    Docs_Blob_MO
)
VALUES
(
    @Docs_ID,
    @Docs_File_Nm,
    @docs_cuml_size_nr,
    @docs_dcty_cd,
    @docs_srce_tbl_cd,
    @docs_srce_id,
    GETDATE(),
    GETDATE(),
    @Docs_Blob_MO
);";

        using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        // numeric(12,0)
        cmd.Parameters.Add(new SqlParameter("@Docs_ID", SqlDbType.Decimal)
        {
            Precision = 12,
            Scale = 0,
            Value = docsId
        });

        // char(254) - SQL char is fixed-length; we store as string and let SQL handle/pad
        cmd.Parameters.Add(new SqlParameter("@Docs_File_Nm", SqlDbType.Char, 254)
        {
            Value = fileNameOnly ?? string.Empty
        });

        // numeric(12,0)
        cmd.Parameters.Add(new SqlParameter("@docs_cuml_size_nr", SqlDbType.Decimal)
        {
            Precision = 12,
            Scale = 0,
            Value = fileSize
        });

        // char(4)
        cmd.Parameters.Add(new SqlParameter("@docs_dcty_cd", SqlDbType.Char, 4)
        {
            Value = docsDctyCd
        });

        // char(6)
        cmd.Parameters.Add(new SqlParameter("@docs_srce_tbl_cd", SqlDbType.Char, 6)
        {
            Value = docsSrceTblCd
        });

        // numeric(12,0)
        cmd.Parameters.Add(new SqlParameter("@docs_srce_id", SqlDbType.Decimal)
        {
            Precision = 12,
            Scale = 0,
            Value = docsSrceId
        });

        // image
        cmd.Parameters.Add(new SqlParameter("@Docs_Blob_MO", SqlDbType.Image)
        {
            Value = blob
        });

        return await cmd.ExecuteNonQueryAsync();
    }

    private static long GenerateNumeric12()
    {
        // 0..999,999,999,999 (12 digits max)
        // We use RNG for better distribution.
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        ulong value = BitConverter.ToUInt64(bytes);

        const ulong max = 999_999_999_999UL;
        return (long)(value % (max + 1));
    }

    private static string NormalizeFixedChar(string? input, int length)
    {
        var s = (input ?? string.Empty).Trim();

        // If user types longer than fixed length, we truncate.
        if (s.Length > length) s = s.Substring(0, length);

        // For CHAR fields, padding isn't required (SQL will pad),
        // but we keep it as-is. If you want to pad, uncomment:
        // s = s.PadRight(length, ' ');

        return s;
    }

    private void SetStatus(string message)
    {
        _lblStatus.Text = "Status: " + message;
    }

    private void SetUiEnabled(bool enabled)
    {
        _txtConn.Enabled = enabled;
        _btnBrowse.Enabled = enabled;
        _btnUpload.Enabled = enabled;
        _txtDctyCd.Enabled = enabled;
        _txtSrceTblCd.Enabled = enabled;
        _numSrceId.Enabled = enabled;
    }
}
```

---

## 3) Run it

```powershell
dotnet run
```

Paste the connection string, browse for a file, and click Upload.

---

## Notes / Assumptions

1. **Table name**
    

- This project uses `dbo.Docs` by default.
    
- Change `TargetTable` at the top of `MainForm` if needed.
    

2. **Fixed-length CHAR fields**
    

- You gave `char(254)`, `char(4)`, and `char(6)`. We send them as `SqlDbType.Char` with lengths.
    

3. **Docs_ID**
    

- Generated as a random numeric up to 12 digits.
    
- If your DB enforces uniqueness and collisions occur, we retry a few times.
    

4. **Docs_Crea_ts / Docs_last_updt_ts**
    

- Insert uses `GETDATE()` in SQL, exactly as requested.
    

If you paste your exact table name (and whether Docs_ID is a PK), I can tighten the insert logic (e.g., detect duplicate-key and retry only in that case).

```

---

If you reply with:
1) the **actual table name** (schema + table), and  
2) whether **Docs_ID is a primary key / unique**,  

…I’ll adjust the insert to be 100% aligned (including smarter duplicate-key retry), **without making the project more complex**.
```
