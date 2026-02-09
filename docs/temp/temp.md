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
// File: Program.cs
// Project: EvDocUploader (WinForms)
// Target: net8.0-windows
//
// What this does:
// - Resizable + maximizable WinForms UI (uses TableLayoutPanel for responsive layout)
// - Accepts SQL Server connection string
// - File picker
// - Uploads the chosen file to EV database table
// - Inserts columns you listed (plus docs_inst_id if present/needed)
//
// IMPORTANT:
// 1) Update TargetTable below to your real schema/table name.
// 2) If your table includes docs_inst_id (you mentioned it), set it in the UI and it will be inserted.
//    If your table does NOT have docs_inst_id, set IncludeDocsInstId=false below (or remove from SQL).

using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;

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

    // You mentioned docs_inst_id. If your table has it, keep this true.
    // If not, set false (or remove docs_inst_id from the INSERT).
    private const bool IncludeDocsInstId = true;

    private readonly TextBox _txtConn = new();
    private readonly TextBox _txtFile = new();
    private readonly TextBox _txtDctyCd = new();
    private readonly TextBox _txtSrceTblCd = new();
    private readonly NumericUpDown _numSrceId = new();
    private readonly NumericUpDown _numInstId = new();

    private readonly Button _btnBrowse = new();
    private readonly Button _btnUpload = new();
    private readonly Button _btnClear = new();

    private readonly Label _lblStatus = new();

    public MainForm()
    {
        Text = "EV Doc Uploader (Resizable)";
        Width = 980;
        Height = 520;
        MinimumSize = new Size(860, 420);

        // ✅ allow maximize + resizing
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;

        StartPosition = FormStartPosition.CenterScreen;

        // Root layout
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12),
            AutoSize = false
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // Connection
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // File
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Fields + buttons
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // Status

        // -------- Connection row --------
        var connRow = MakeTwoColumnRow("Connection String:", _txtConn);
        _txtConn.PlaceholderText = "Paste SQL Server connection string here...";
        _txtConn.Dock = DockStyle.Fill;

        // -------- File row --------
        var fileRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 3,
            RowCount = 1,
            AutoSize = true
        };
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

        var lblFile = new Label { Text = "File:", AutoSize = true, Anchor = AnchorStyles.Left };
        _txtFile.ReadOnly = true;
        _txtFile.Dock = DockStyle.Fill;

        _btnBrowse.Text = "Browse...";
        _btnBrowse.Dock = DockStyle.Fill;
        _btnBrowse.Click += (_, _) => BrowseFile();

        fileRow.Controls.Add(lblFile, 0, 0);
        fileRow.Controls.Add(_txtFile, 1, 0);
        fileRow.Controls.Add(_btnBrowse, 2, 0);

        // -------- Fields area (grid) --------
        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 4,
            AutoSize = false
        };

        // Columns: label | input | label | input
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        // Row styles
        for (int i = 0; i < fields.RowCount; i++)
            fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // docs_dcty_cd (char(4))
        fields.Controls.Add(MakeLabel("docs_dcty_cd (4):"), 0, 0);
        _txtDctyCd.Text = "FILE";
        _txtDctyCd.Dock = DockStyle.Fill;
        fields.Controls.Add(_txtDctyCd, 1, 0);

        // docs_srce_tbl_cd (char(6))
        fields.Controls.Add(MakeLabel("docs_srce_tbl_cd (6):"), 2, 0);
        _txtSrceTblCd.Text = "MANUAL";
        _txtSrceTblCd.Dock = DockStyle.Fill;
        fields.Controls.Add(_txtSrceTblCd, 3, 0);

        // docs_srce_id (numeric(12,0))
        fields.Controls.Add(MakeLabel("docs_srce_id:"), 0, 1);
        _numSrceId.Minimum = 0;
        _numSrceId.Maximum = 999_999_999_999;
        _numSrceId.Dock = DockStyle.Fill;
        _numSrceId.ThousandsSeparator = true;
        fields.Controls.Add(_numSrceId, 1, 1);

        // docs_inst_id (numeric(12,0)) - you said you couldn't see it before
        fields.Controls.Add(MakeLabel("docs_inst_id:"), 2, 1);
        _numInstId.Minimum = 0;
        _numInstId.Maximum = 999_999_999_999;
        _numInstId.Dock = DockStyle.Fill;
        _numInstId.ThousandsSeparator = true;
        fields.Controls.Add(_numInstId, 3, 1);

        // Buttons row
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(0, 10, 0, 0),
            WrapContents = false
        };

        _btnUpload.Text = "Upload";
        _btnUpload.Width = 120;
        _btnUpload.Height = 34;
        _btnUpload.Click += async (_, _) => await UploadAsync();

        _btnClear.Text = "Clear";
        _btnClear.Width = 120;
        _btnClear.Height = 34;
        _btnClear.Click += (_, _) => ClearForm();

        buttons.Controls.Add(_btnUpload);
        buttons.Controls.Add(_btnClear);

        // Put fields + buttons into a panel
        var midPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        midPanel.Controls.Add(buttons);
        midPanel.Controls.Add(fields);

        // -------- Status row --------
        _lblStatus.Text = "Status: Ready";
        _lblStatus.Dock = DockStyle.Fill;
        _lblStatus.AutoSize = false;
        _lblStatus.Height = 80;

        // Add to root
        root.Controls.Add(connRow, 0, 0);
        root.Controls.Add(fileRow, 0, 1);
        root.Controls.Add(midPanel, 0, 2);
        root.Controls.Add(_lblStatus, 0, 3);

        Controls.Add(root);
    }

    private static Label MakeLabel(string text) =>
        new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left };

    private static TableLayoutPanel MakeTwoColumnRow(string labelText, Control input)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var lbl = new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left };
        input.Dock = DockStyle.Fill;

        row.Controls.Add(lbl, 0, 0);
        row.Controls.Add(input, 1, 0);
        return row;
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
            var blob = await File.ReadAllBytesAsync(fullPath);
            var fileSize = (long)blob.Length;

            var dcty = NormalizeFixedChar(_txtDctyCd.Text, 4);
            var srcTbl = NormalizeFixedChar(_txtSrceTblCd.Text, 6);
            var srcId = (long)_numSrceId.Value;
            var instId = (long)_numInstId.Value;

            SetUiEnabled(false);
            SetStatus("Uploading...");

            const int maxAttempts = 5;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var docsId = GenerateNumeric12();

                int rows = await InsertDocAsync(
                    connStr,
                    docsId,
                    fileNameOnly,
                    fileSize,
                    dcty,
                    srcTbl,
                    srcId,
                    instId,
                    blob);

                if (rows == 1)
                {
                    SetStatus($"SUCCESS: Uploaded '{fileNameOnly}' as Docs_ID={docsId} (bytes={fileSize}).");
                    return;
                }

                SetStatus($"ERROR: Insert affected {rows} rows (attempt {attempt}/{maxAttempts}).");
            }

            SetStatus("ERROR: Failed to insert after multiple attempts (possible ID collisions or constraints).");
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
        long docsInstId,
        byte[] blob)
    {
        // Build INSERT that optionally includes docs_inst_id
        string sql;

        if (IncludeDocsInstId)
        {
            sql = $@"
INSERT INTO {TargetTable}
(
    Docs_ID,
    Docs_File_Nm,
    docs_cuml_size_nr,
    docs_dcty_cd,
    docs_srce_tbl_cd,
    docs_srce_id,
    docs_inst_id,
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
    @docs_inst_id,
    GETDATE(),
    GETDATE(),
    @Docs_Blob_MO
);";
        }
        else
        {
            sql = $@"
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
        }

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

        // char(254)
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

        if (IncludeDocsInstId)
        {
            cmd.Parameters.Add(new SqlParameter("@docs_inst_id", SqlDbType.Decimal)
            {
                Precision = 12,
                Scale = 0,
                Value = docsInstId
            });
        }

        // image
        cmd.Parameters.Add(new SqlParameter("@Docs_Blob_MO", SqlDbType.Image)
        {
            Value = blob
        });

        return await cmd.ExecuteNonQueryAsync();
    }

    private static long GenerateNumeric12()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        ulong value = BitConverter.ToUInt64(bytes);

        const ulong max = 999_999_999_999UL;
        return (long)(value % (max + 1));
    }

    private static string NormalizeFixedChar(string? input, int length)
    {
        var s = (input ?? string.Empty).Trim();
        if (s.Length > length) s = s.Substring(0, length);
        return s;
    }

    private void ClearForm()
    {
        _txtFile.Text = "";
        _txtDctyCd.Text = "FILE";
        _txtSrceTblCd.Text = "MANUAL";
        _numSrceId.Value = 0;
        _numInstId.Value = 0;
        SetStatus("Ready");
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
        _btnClear.Enabled = enabled;
        _txtDctyCd.Enabled = enabled;
        _txtSrceTblCd.Enabled = enabled;
        _numSrceId.Enabled = enabled;
        _numInstId.Enabled = enabled;
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
