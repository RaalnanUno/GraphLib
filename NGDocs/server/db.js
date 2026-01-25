const fs = require('fs');
const path = require('path');
const Database = require('better-sqlite3');

// Server-owned DB selection:
// - default: release
// - override with env var: NGDOCS_DB_MODE=debug|release
function getSelectedMode() {
  const raw = String(process.env.NGDOCS_DB_MODE || 'release').toLowerCase();
  return raw === 'debug' ? 'debug' : 'release';
}

function resolveRepoRoot() {
  // NGDocs/server -> NGDocs -> repo root (GraphLib)
  return path.resolve(__dirname, '..', '..');
}

function getDbPaths() {
  const repoRoot = resolveRepoRoot();

  const debugPath = path.join(
    repoRoot,
    'src',
    'GraphLib.Console',
    'bin',
    'Debug',
    'net8.0',
    'Data',
    'GraphLib.db'
  );

  const releasePath = path.join(
    repoRoot,
    'src',
    'GraphLib.Console',
    'bin',
    'Release',
    'net8.0',
    'Data',
    'GraphLib.db'
  );

  return { repoRoot, debugPath, releasePath };
}

function getSelectedDbPath() {
  const { debugPath, releasePath } = getDbPaths();
  const mode = getSelectedMode();
  const dbPath = mode === 'debug' ? debugPath : releasePath;
  return { mode, dbPath };
}

function fileExists(p) {
  try {
    return !!p && fs.existsSync(p);
  } catch {
    return false;
  }
}

function openDbOrNull(dbPath) {
  if (!fileExists(dbPath)) return null;

  // Readwrite because we have an update endpoint.
  return new Database(dbPath, { readonly: false });
}

function safeIdentifier(name) {
  // conservative: letters/numbers/underscore only
  return /^[A-Za-z0-9_]+$/.test(name);
}

module.exports = {
  getSelectedMode,
  getDbPaths,
  getSelectedDbPath,
  fileExists,
  openDbOrNull,
  safeIdentifier
};
