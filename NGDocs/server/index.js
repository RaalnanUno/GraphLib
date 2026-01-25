const express = require('express');
const cors = require('cors');

const {
  getDbPaths,
  getSelectedDbPath,
  fileExists,
  openDbOrNull,
  safeIdentifier
} = require('./db');

const app = express();
app.use(cors());
app.use(express.json());

const PORT = process.env.NGDOCS_API_PORT || 3001;

// Prevent “BigInt cannot be serialized” crashes
app.set('json replacer', (_k, v) => (typeof v === 'bigint' ? v.toString() : v));

function getModeAndPath() {
  return getSelectedDbPath(); // { mode, dbPath }
}

// GET /api/db/status
app.get('/api/db/status', (_req, res) => {
  const { mode, dbPath } = getModeAndPath();
  const paths = getDbPaths();

  res.json({
    ok: true,
    mode,
    selectedPath: dbPath,
    selectedExists: fileExists(dbPath),
    knownPaths: {
      debug: paths.debugPath,
      release: paths.releasePath
    },
    howToChange: 'Set env var NGDOCS_DB_MODE=debug|release (server-owned).'
  });
});

// GET /api/db/tables
app.get('/api/db/tables', (_req, res) => {
  const { mode, dbPath } = getModeAndPath();

  if (!fileExists(dbPath)) return res.status(404).json({ ok: false, error: 'DB not found', mode, dbPath });

  const db = openDbOrNull(dbPath);
  if (!db) return res.status(500).json({ ok: false, error: 'Failed to open DB.', mode, dbPath });

  try {
    const rows = db
      .prepare(`SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;`)
      .all();

    res.json({ ok: true, mode, dbPath, tables: rows.map((r) => r.name) });
  } finally {
    db.close();
  }
});

// GET /api/db/table/:name?limit=200&offset=0
app.get('/api/db/table/:name', (req, res) => {
  const { mode, dbPath } = getModeAndPath();
  const table = String(req.params.name || '');

  if (!fileExists(dbPath)) return res.status(404).json({ ok: false, error: 'DB not found', mode, dbPath });

  if (!safeIdentifier(table)) {
    return res.status(400).json({ ok: false, error: 'Invalid table name. (letters/numbers/underscore only)' });
  }

  const limit = Math.min(Number(req.query.limit || 200), 1000);
  const offset = Math.max(Number(req.query.offset || 0), 0);

  const db = openDbOrNull(dbPath);
  if (!db) return res.status(500).json({ ok: false, error: 'Failed to open DB.', mode, dbPath });

  try {
    const columns = db.prepare(`PRAGMA table_info(${table});`).all();
    if (!columns?.length) return res.status(404).json({ ok: false, error: 'Table not found', table });

    const pk = columns.find((c) => c.pk === 1)?.name || null;
    const rows = db.prepare(`SELECT * FROM ${table} LIMIT ? OFFSET ?;`).all(limit, offset);
    const countRow = db.prepare(`SELECT COUNT(1) as count FROM ${table};`).get();

    res.json({
      ok: true,
      mode,
      dbPath,
      table,
      primaryKey: pk,
      columns: columns.map((c) => ({
        name: c.name,
        type: c.type,
        notnull: !!c.notnull,
        defaultValue: c.dflt_value,
        pk: c.pk
      })),
      paging: { limit, offset, total: countRow?.count ?? rows.length },
      rows
    });
  } finally {
    db.close();
  }
});

// POST /api/db/table/:name/update
app.post('/api/db/table/:name/update', (req, res) => {
  const { mode, dbPath } = getModeAndPath();
  const table = String(req.params.name || '');

  if (!fileExists(dbPath)) return res.status(404).json({ ok: false, error: 'DB not found', mode, dbPath });
  if (!safeIdentifier(table)) return res.status(400).json({ ok: false, error: 'Invalid table name' });

  const pkColumn = String(req.body?.pkColumn || '');
  const pkValue = req.body?.pkValue;
  const patch = req.body?.patch;

  if (!pkColumn || pkValue === undefined || pkValue === null) {
    return res.status(400).json({ ok: false, error: 'pkColumn and pkValue are required.' });
  }
  if (!patch || typeof patch !== 'object' || Array.isArray(patch)) {
    return res.status(400).json({ ok: false, error: 'patch must be an object {col:value}.' });
  }

  const db = openDbOrNull(dbPath);
  if (!db) return res.status(500).json({ ok: false, error: 'Failed to open DB.', mode, dbPath });

  try {
    const columns = db.prepare(`PRAGMA table_info(${table});`).all();
    if (!columns?.length) return res.status(404).json({ ok: false, error: 'Table not found', table });

    const validCols = new Set(columns.map((c) => c.name));
    if (!validCols.has(pkColumn)) return res.status(400).json({ ok: false, error: 'pkColumn not in table' });

    const entries = Object.entries(patch);
    if (!entries.length) return res.status(400).json({ ok: false, error: 'No fields to update' });

    for (const [col] of entries) {
      if (!safeIdentifier(col) || !validCols.has(col)) {
        return res.status(400).json({ ok: false, error: `Invalid column: ${col}` });
      }
    }

    const setSql = entries.map(([col]) => `${col} = ?`).join(', ');
    const values = entries.map(([, v]) => v);

    const stmt = db.prepare(`UPDATE ${table} SET ${setSql} WHERE ${pkColumn} = ?;`);
    const info = stmt.run(...values, pkValue);

    res.json({ ok: true, mode, dbPath, table, pkColumn, pkValue, changed: info.changes });
  } finally {
    db.close();
  }
});

app.listen(PORT, () => {
  const { mode, dbPath } = getSelectedDbPath();
  console.log(`[NGDocs API] http://localhost:${PORT}`);
  console.log(`[NGDocs API] DB mode=${mode}`);
  console.log(`[NGDocs API] DB path=${dbPath}`);
});
