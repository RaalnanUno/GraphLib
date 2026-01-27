# This is what I have so far:

Access to fetch at 'http://127.0.0.1:60375/api/supervisory-info-by-casemgr' from origin 'http://127.0.0.1:5500' has been blocked by CORS policy: No 'Access-Control-Allow-Origin' header is present on the requested resource.
app.js:141  GET http://127.0.0.1:60375/api/supervisory-info-by-casemgr net::ERR_FAILED 200 (OK)
getAll @ app.js:141
loadAndRender @ app.js:205
(anonymous) @ app.js:340
e @ jquery.min.js:2
(anonymous) @ jquery.min.js:2
setTimeout
(anonymous) @ jquery.min.js:2
c @ jquery.min.js:2
fireWith @ jquery.min.js:2
fire @ jquery.min.js:2
c @ jquery.min.js:2
fireWith @ jquery.min.js:2
ready @ jquery.min.js:2
B @ jquery.min.js:2
installHook.js:1 TypeError: Failed to fetch
    at getAll (app.js:141:23)
    at loadAndRender (app.js:205:24)
    at HTMLDocument.<anonymous> (app.js:340:13)
    at e (jquery.min.js:2:30038)
    at jquery.min.js:2:30340

## docs\temp\temp.html

```json
<!doctype html>
<html lang="en">

<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />

  <!-- Bootstrap 4 CSS -->
  <link
    rel="stylesheet"
    href="https://cdnjs.cloudflare.com/ajax/libs/twitter-bootstrap/4.5.2/css/bootstrap.min.css"
  />

  <!-- DataTables + Bootstrap 4 CSS -->
  <link
    rel="stylesheet"
    href="https://cdn.datatables.net/1.13.8/css/dataTables.bootstrap4.min.css"
  />

  <title>Supervisory Info by CaseMgr</title>
</head>


<body class="p-3">
  <div class="container-fluid">
    <div class="d-flex align-items-center justify-content-between mb-3">
      <h3 class="m-0">Supervisory Info by CaseMgr</h3>

      <button id="populateDatabaseBtn" class="btn btn-primary">
        Populate Database
      </button>
    </div>

    <table id="myTable" class="table table-striped table-bordered w-100">
      <thead>
        <tr></tr>
      </thead>
      <tbody></tbody>
    </table>
  </div>

  <!-- jQuery FIRST -->
  <script src="https://ajax.googleapis.com/ajax/libs/jquery/3.6.0/jquery.min.js"></script>

  <!-- Popper + Bootstrap 4 JS -->
  <script src="https://cdnjs.cloudflare.com/ajax/libs/popper.js/1.16.1/umd/popper.min.js"></script>
  <script src="https://cdnjs.cloudflare.com/ajax/libs/twitter-bootstrap/4.5.2/js/bootstrap.min.js"></script>

  <!-- DataTables -->
  <script src="https://cdn.datatables.net/1.13.8/js/jquery.dataTables.min.js"></script>
  <script src="https://cdn.datatables.net/1.13.8/js/dataTables.bootstrap4.min.js"></script>

  <!-- Your app -->
  <script src="app.js"></script>
</body>


</html>

```

## docs\temp\db.json

```json
{
  "supervisory-info-by-casemgr": [
    {
      "id": 1,
      "CASE_MGR_NTWK_ID": "STRING",
      "CASE_MGR_FST_NME": "STRING",
      "CASE_MGR_LST_NME": "STRING",
      "CASE_MGR_MID_NME": "STRING",
      "ORG_UNIQ_NUM": 0,
      "INST_OCC_CHTR_NUM": 0,
      "CASE_VRSN_RND_DTE": "01/09/1974",
      "CASE_SPVR_REGN_CDE": "STRING"
    }
  ]
}


```

## docs\temp\app.js

```js
/* app.js
   - json-server collection: /supervisory-info-by-casemgr
   - DataTables view + inline edit (PATCH)
   - Populate DB with random records (POST)
*/

(function () {
  // ----------------------------
  // Config
  // ----------------------------
const API_BASE = "http://127.0.0.1:60375";
const API_PREFIX = "/api";
const COLLECTION = "supervisory-info-by-casemgr";
const ENDPOINT = `${API_BASE}${API_PREFIX}/${COLLECTION}`;


  // How many records to generate when clicking Populate
  const DEFAULT_GENERATE_COUNT = 15;

  // Columns in your model (in the order you want displayed)
  const COLS = [
    { key: "id", editable: false, title: "id" }, // json-server likes an "id" field
    { key: "CASE_MGR_NTWK_ID", editable: true, title: "CASE_MGR_NTWK_ID" },
    { key: "CASE_MGR_FST_NME", editable: true, title: "CASE_MGR_FST_NME" },
    { key: "CASE_MGR_LST_NME", editable: true, title: "CASE_MGR_LST_NME" },
    { key: "CASE_MGR_MID_NME", editable: true, title: "CASE_MGR_MID_NME" },
    { key: "ORG_UNIQ_NUM", editable: true, title: "ORG_UNIQ_NUM" },
    { key: "INST_OCC_CHTR_NUM", editable: true, title: "INST_OCC_CHTR_NUM" },
    { key: "CASE_VRSN_RND_DTE", editable: true, title: "CASE_VRSN_RND_DTE" }, // MM/DD/YYYY string
    { key: "CASE_SPVR_REGN_CDE", editable: true, title: "CASE_SPVR_REGN_CDE" }
  ];

  // ----------------------------
  // Utilities
  // ----------------------------
  function pad2(n) {
    return String(n).padStart(2, "0");
  }

  function formatDateMMDDYYYY(d) {
    const mm = pad2(d.getMonth() + 1);
    const dd = pad2(d.getDate());
    const yyyy = d.getFullYear();
    return `${mm}/${dd}/${yyyy}`;
  }

  function randomInt(min, max) {
    return Math.floor(Math.random() * (max - min + 1)) + min;
  }

  // Pronounceable-ish string: alternating consonant/vowel
  function pronounceable(len) {
    const vowels = "aeiou";
    const consonants = "bcdfghjklmnpqrstvwxyz";
    let s = "";
    for (let i = 0; i < len; i++) {
      const pool = i % 2 === 0 ? consonants : vowels;
      s += pool.charAt(randomInt(0, pool.length - 1));
    }
    // capitalize first letter for name-like values
    return s.charAt(0).toUpperCase() + s.slice(1);
  }

  // Uppercase code-like value (still pronounceable-ish)
  function pronounceableCode(len) {
    return pronounceable(len).toUpperCase();
  }

  function randomDateThisYear() {
    const now = new Date();
    const year = now.getFullYear();
    const start = new Date(year, 0, 1).getTime();
    const end = new Date(year, 11, 31).getTime();
    const t = randomInt(start, end);
    return new Date(t);
  }

  function toTypedValue(colKey, raw) {
    // Convert strings back to numbers where appropriate
    if (colKey === "ORG_UNIQ_NUM" || colKey === "INST_OCC_CHTR_NUM") {
      const n = Number(raw);
      return Number.isFinite(n) ? n : 0;
    }
    return raw;
  }

  function escapeHtml(str) {
    return String(str)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#039;");
  }

  // ----------------------------
  // Data generation
  // ----------------------------
  function makeRandomRecord() {
    return {
      // id is optional; json-server will auto-generate if omitted.
      // If you want your own IDs, you can set it here.
      CASE_MGR_NTWK_ID: pronounceableCode(8),
      CASE_MGR_FST_NME: pronounceable(randomInt(4, 8)),
      CASE_MGR_LST_NME: pronounceable(randomInt(5, 10)),
      CASE_MGR_MID_NME: pronounceable(randomInt(3, 7)),
      ORG_UNIQ_NUM: randomInt(1, 999999),
      INST_OCC_CHTR_NUM: randomInt(1, 999999),
      CASE_VRSN_RND_DTE: formatDateMMDDYYYY(randomDateThisYear()),
      CASE_SPVR_REGN_CDE: pronounceableCode(randomInt(3, 5))
    };
  }

  async function postRecord(rec) {
    const res = await fetch(ENDPOINT, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(rec)
    });
    if (!res.ok) {
      const txt = await res.text().catch(() => "");
      throw new Error(`POST failed (${res.status}): ${txt}`);
    }
    return res.json();
  }

  async function patchRecord(id, patch) {
    const res = await fetch(`${ENDPOINT}/${id}`, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(patch)
    });
    if (!res.ok) {
      const txt = await res.text().catch(() => "");
      throw new Error(`PATCH failed (${res.status}): ${txt}`);
    }
    return res.json();
  }

  async function getAll() {
    const res = await fetch(ENDPOINT);
    if (!res.ok) {
      const txt = await res.text().catch(() => "");
      throw new Error(`GET failed (${res.status}): ${txt}`);
    }
    return res.json();
  }

  // ----------------------------
  // UI helpers
  // ----------------------------
  function ensureDeps() {
    // DataTables requires its scripts/styles; if missing, fail loudly.
    if (!window.jQuery) throw new Error("jQuery is required but not loaded.");
    if (!$.fn.DataTable) throw new Error("DataTables is required but not loaded.");
  }

  function setStatus(msg, type) {
    // Adds a lightweight status area above the table if not present.
    let $box = $("#statusBox");
    if ($box.length === 0) {
      $("#populateDatabaseBtn").after(
        `<div id="statusBox" class="mt-3 alert" role="alert" style="display:none;"></div>`
      );
      $box = $("#statusBox");
    }

    if (!msg) {
      $box.hide();
      return;
    }

    const cls =
      type === "success"
        ? "alert alert-success"
        : type === "warning"
        ? "alert alert-warning"
        : type === "danger"
        ? "alert alert-danger"
        : "alert alert-info";

    $box.attr("class", `mt-3 ${cls}`).html(escapeHtml(msg)).show();
  }

  function buildTableHeader() {
    const $theadRow = $("#myTable thead tr");
    $theadRow.empty();
    for (const c of COLS) {
      $theadRow.append(`<th>${escapeHtml(c.title)}</th>`);
    }
  }

  function mapRowToArray(rowObj) {
    return COLS.map((c) => rowObj[c.key]);
  }

  // ----------------------------
  // DataTable setup
  // ----------------------------
  let dt = null;

  async function loadAndRender() {
    setStatus("Loading records...", "info");

    const data = await getAll();

    // Build table header to match COLS
    buildTableHeader();

    // If DataTable already exists, just replace data
    if (dt) {
      dt.clear();
      dt.rows.add(data.map(mapRowToArray));
      dt.draw(false);
      setStatus(`Loaded ${data.length} record(s). Click a cell to edit.`, "success");
      return;
    }

    // Init DataTable first time
    dt = $("#myTable").DataTable({
      data: data.map(mapRowToArray),
      columns: COLS.map((c) => ({ title: c.title })),
      pageLength: 10,
      lengthMenu: [10, 25, 50, 100],
      responsive: true
    });

    // Inline editing handler (click-to-edit)
    $("#myTable tbody").on("click", "td", function () {
      const cell = dt.cell(this);
      const idx = cell.index();
      if (!idx) return;

      const colDef = COLS[idx.column];
      if (!colDef.editable) return;

      // Current row data as array; id is in column 0 by our schema
      const rowArr = dt.row(idx.row).data();
      const id = rowArr[0];
      if (id === undefined || id === null) {
        setStatus("Cannot edit a row without an 'id' field. Ensure json-server returns id.", "danger");
        return;
      }

      const original = cell.data() ?? "";
      const $td = $(cell.node());

      // prevent double-edit
      if ($td.find("input").length) return;

      const inputType = colDef.key === "CASE_VRSN_RND_DTE" ? "text" : "text";
      const $input = $(
        `<input type="${inputType}" class="form-control form-control-sm" value="${escapeHtml(
          original
        )}">`
      );

      $td.empty().append($input);
      $input.trigger("focus").select();

      const commit = async () => {
        const newValRaw = $input.val();
        const newVal = toTypedValue(colDef.key, newValRaw);

        // restore cell immediately (optimistic UI)
        cell.data(newVal).draw(false);

        // If no change, skip PATCH
        if (String(newVal) === String(original)) return;

        try {
          await patchRecord(id, { [colDef.key]: newVal });
          setStatus(`Saved ${colDef.key} for id=${id}`, "success");
        } catch (e) {
          // revert on failure
          cell.data(original).draw(false);
          setStatus(e.message || "Save failed", "danger");
        }
      };

      $input.on("keydown", function (e) {
        if (e.key === "Enter") {
          e.preventDefault();
          commit();
        }
        if (e.key === "Escape") {
          // cancel
          cell.data(original).draw(false);
        }
      });

      $input.on("blur", function () {
        commit();
      });
    });

    setStatus(`Loaded ${data.length} record(s). Click a cell to edit.`, "success");
  }

  // ----------------------------
  // Populate DB button
  // ----------------------------
  async function populateDatabase(count) {
    setStatus(`Creating ${count} new record(s)...`, "info");

    // Create sequentially (simpler, easier error reporting).
    // If you want faster, you can Promise.all, but sequential is safer for starters.
    let created = 0;

    for (let i = 0; i < count; i++) {
      const rec = makeRandomRecord();
      await postRecord(rec);
      created++;
    }

    setStatus(`Created ${created} record(s). Reloading...`, "success");
    await loadAndRender();
  }

  // ----------------------------
  // Boot
  // ----------------------------
  $(async function () {
    try {
      ensureDeps();

      // Wire up populate button
      $("#populateDatabaseBtn").on("click", async function () {
        $(this).prop("disabled", true);
        try {
          await populateDatabase(DEFAULT_GENERATE_COUNT);
        } catch (e) {
          setStatus(e.message || "Populate failed", "danger");
        } finally {
          $(this).prop("disabled", false);
        }
      });

      // Initial render
      await loadAndRender();
    } catch (e) {
      console.error(e);
      setStatus(e.message || "Initialization failed", "danger");
    }
  });
})();


```

## My json is running at

http://127.0.0.1:60375/api/supervisory-info-by-casemgr