// File: Program.cs
// Project: EvDocUploader (WinForms)
// Target: net8.0-windows
//
// Fixes:
// - Resizable/maximizable UI
// - Forces usable row heights so NumericUpDown fields never collapse to 2px
// - Clean, simple form with connection string, file picker, and required fields
//
// IMPORTANT:
// 1) Update TargetTable to your real table name (schema.table)
// 2) If your table does NOT have docs_inst_id, set IncludeDocsInstId = false

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

    // You mentioned docs_inst_id. If your table has it, keep true; else set false.
    private const bool IncludeDocsInstId = true;

    // ---- UI controls ----
    private readonly TextBox _txtConn = new();
    private readonly TextBox _txtFile = new();

    private readonly TextBox _txtDctyCd = new();
    private readonly TextBox _txtSrceTblCd = new();

    private readonly NumericUpDown _numSrceId = new();
    private readonly NumericUpDown _numInstId = new();

    private readonly Button _btnBrowse = new();
    private readonly Button _btnUpload = new();
    private readonly Button _btnClear = new();

    private readonly TextBox _txtStatus = new();

    // Row height constants (prevents 2px-tall controls)
    private const int RowH = 36;
    private const int LabelW = 170;

    public MainForm()
    {
        Text = "EV Doc Uploader";
        StartPosition = FormStartPosition.CenterScreen;

        Width = 1050;
        Height = 560;
        MinimumSize = new Size(900, 460);

        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;

        // Root layout: a single TableLayoutPanel that fills the whole form
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12),
        };

        // Top: form fields area (auto)
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        // Middle: spacer/optional (fills)
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        // Bottom: status (fixed-ish)
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));

        // ---- Fields panel (fixed row heights) ----
        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 4,
            AutoSize = true,
        };

        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LabelW));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LabelW));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        // Helper to add a row with fixed height
        int r = 0;

        AddRow(fields, ref r,
            leftLabel: "Connection String:",
            leftControl: ConfigureTextBox(_txtConn, multiline: false, readOnly: false),
            rightLabel: null,
            rightControl: null);

        AddRowFilePicker(fields, ref r);

        AddRow(fields, ref r,
            leftLabel: "docs_dcty_cd (char(4)):",
            leftControl: ConfigureTextBox(_txtDctyCd, multiline: false, readOnly: false, defaultText: "FILE"),
            rightLabel: "docs_srce_tbl_cd (char(6)):",
            rightControl: ConfigureTextBox(_txtSrceTblCd, multiline: false, readOnly: false, defaultText: "MANUAL"));

        AddRow(fields, ref r,
            leftLabel: "docs_srce_id (numeric(12,0)):",
            leftControl: ConfigureNumeric(_numSrceId),
            rightLabel: "docs_inst_id (numeric(12,0)):",
            rightControl: ConfigureNumeric(_numInstId));

        // ---- Buttons row ----
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(0, 10, 0, 0)
        };

        _btnUpload.Text = "Upload";
        _btnUpload.Width = 140;
        _btnUpload.Height = RowH;
        _btnUpload.Click += async (_, _) => await UploadAsync();

        _btnClear.Text = "Clear";
        _btnClear.Width = 140;
        _btnClear.Height = RowH;
        _btnClear.Click += (_, _) => ClearForm();

        buttons.Controls.Add(_btnUpload);
        buttons.Controls.Add(_btnClear);

        // Container panel for top section
        var topPanel = new Panel { Dock = DockStyle.Top, AutoSize = true };
        topPanel.Controls.Add(buttons);
        topPanel.Controls.Add(fields);

        // ---- Status box (bottom) ----
        _txtStatus.Dock = DockStyle.Fill;
        _txtStatus.Multiline = true;
        _txtStatus.ReadOnly = true;
        _txtStatus.ScrollBars = ScrollBars.Vertical;
        _txtStatus.Font = new Font(FontFamily.GenericMonospace, 9.5f);
        SetStatus("Ready.");

        root.Controls.Add(topPanel, 0, 0);
        root.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 1); // spacer
        root.Controls.Add(_txtStatus, 0, 2);

        Controls.Add(root);
    }

    // ---------- UI builders ----------

    private void AddRow(
        TableLayoutPanel grid,
        ref int rowIndex,
        string? leftLabel,
        Control? leftControl,
        string? rightLabel,
        Control? rightControl)
    {
        grid.RowCount = rowIndex + 1;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, RowH));

        if (leftLabel is not null)
        {
            var lbl = new Label
            {
                Text = leftLabel,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            grid.Controls.Add(lbl, 0, rowIndex);
        }

        if (leftControl is not null)
        {
            leftControl.Dock = DockStyle.Fill;
            leftControl.Margin = new Padding(3, 6, 12, 6); // vertical padding helps visuals
            grid.Controls.Add(leftControl, 1, rowIndex);
        }

        if (rightLabel is not null)
        {
            var lbl = new Label
            {
                Text = rightLabel,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            grid.Controls.Add(lbl, 2, rowIndex);
        }

        if (rightControl is not null)
        {
            rightControl.Dock = DockStyle.Fill;
            rightControl.Margin = new Padding(3, 6, 3, 6);
            grid.Controls.Add(rightControl, 3, rowIndex);
        }

        rowIndex++;
    }

    private void AddRowFilePicker(TableLayoutPanel grid, ref int rowIndex)
    {
        grid.RowCount = rowIndex + 1;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, RowH));

        var lbl = new Label
        {
            Text = "File:",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill
        };
        grid.Controls.Add(lbl, 0, rowIndex);

        _txtFile.ReadOnly = true;
        _txtFile.Dock = DockStyle.Fill;
        _txtFile.Margin = new Padding(3, 6, 12, 6);
        grid.Controls.Add(_txtFile, 1, rowIndex);

        // Span file textbox across columns 1 and 2 (so Browse can sit in col 3)
        grid.SetColumnSpan(_txtFile, 2);

        _btnBrowse.Text = "Browse...";
        _btnBrowse.Dock = DockStyle.Fill;
        _btnBrowse.Margin = new Padding(3, 4, 3, 4);
        _btnBrowse.Height = RowH;
        _btnBrowse.Click += (_, _) => BrowseFile();

        grid.Controls.Add(_btnBrowse, 3, rowIndex);

        rowIndex++;
    }

    private static TextBox ConfigureTextBox(TextBox tb, bool multiline, bool readOnly, string? defaultText = null)
    {
        tb.Multiline = multiline;
        tb.ReadOnly = readOnly;
        tb.Text = defaultText ?? tb.Text;

        // Force a sensible height even when dock-filled
        tb.MinimumSize = new Size(0, 26);

        return tb;
    }

    private static NumericUpDown ConfigureNumeric(NumericUpDown n)
    {
        n.Minimum = 0;
        n.Maximum = 999_999_999_999;
        n.DecimalPlaces = 0;
        n.ThousandsSeparator = true;

        // ✅ The important part: prevent the "2px tall" collapse
        n.MinimumSize = new Size(0, 28);
        n.Height = 28;

        // Also prevent weird autosize behavior
        n.AutoSize = false;

        return n;
    }

    // ---------- Actions ----------

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
            SetStatus("Selected file. Ready to upload.");
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

            const int maxAttempts = 6;
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
                    instId,
                    blob);

                if (rows == 1)
                {
                    SetStatus($"SUCCESS: Uploaded '{fileNameOnly}' as Docs_ID={docsId}\r\nSize: {fileSize} bytes");
                    return;
                }

                SetStatus($"ERROR: Insert affected {rows} rows (attempt {attempt}/{maxAttempts}).");
            }

            SetStatus("ERROR: Failed after multiple attempts (possible ID collisions/constraints).");
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

        cmd.Parameters.Add(new SqlParameter("@Docs_ID", SqlDbType.Decimal)
        {
            Precision = 12,
            Scale = 0,
            Value = docsId
        });

        cmd.Parameters.Add(new SqlParameter("@Docs_File_Nm", SqlDbType.Char, 254)
        {
            Value = fileNameOnly ?? string.Empty
        });

        cmd.Parameters.Add(new SqlParameter("@docs_cuml_size_nr", SqlDbType.Decimal)
        {
            Precision = 12,
            Scale = 0,
            Value = fileSize
        });

        cmd.Parameters.Add(new SqlParameter("@docs_dcty_cd", SqlDbType.Char, 4)
        {
            Value = docsDctyCd
        });

        cmd.Parameters.Add(new SqlParameter("@docs_srce_tbl_cd", SqlDbType.Char, 6)
        {
            Value = docsSrceTblCd
        });

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
        SetStatus("Ready.");
    }

    private void SetStatus(string message)
    {
        _txtStatus.Text = message;
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
