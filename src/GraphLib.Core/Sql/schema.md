PRAGMA journal_mode=WAL;

-- Aggregate conversion metrics (no per-file tracking)
CREATE TABLE IF NOT EXISTS ConversionMetrics (
  Id              INTEGER PRIMARY KEY AUTOINCREMENT,
  SourceExtension TEXT NOT NULL,  -- e.g. ".docx"
  TargetExtension TEXT NOT NULL,  -- e.g. ".pdf"
  ConversionCount INTEGER NOT NULL DEFAULT 0,
  SuccessCount    INTEGER NOT NULL DEFAULT 0,
  FailureCount    INTEGER NOT NULL DEFAULT 0,
  LastAttemptAt   TEXT,
  LastSuccessAt   TEXT,
  LastFailureAt   TEXT,

  -- ensures one row per src->target pair
  UNIQUE(SourceExtension, TargetExtension)
);

CREATE INDEX IF NOT EXISTS IX_ConversionMetrics_SourceTarget
  ON ConversionMetrics(SourceExtension, TargetExtension);


-- -----------------------------
-- Reference tables (options)
-- -----------------------------
CREATE TABLE IF NOT EXISTS RefConflictBehavior (
  Value TEXT PRIMARY KEY,
  Description TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS RefGraphStage (
  Value TEXT PRIMARY KEY,
  Description TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS RefLogLevel (
  Value TEXT PRIMARY KEY,
  Description TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS RefSettingKey (
  Key TEXT PRIMARY KEY,
  Description TEXT NOT NULL,
  RefTable TEXT NULL
);

-- -----------------------------
-- Settings (all config in SQLite)
-- -----------------------------
CREATE TABLE IF NOT EXISTS AppSettings (
  Id INTEGER PRIMARY KEY CHECK (Id = 1),

  -- Target
  SiteUrl TEXT NOT NULL,
  LibraryName TEXT NOT NULL,
  TempFolder TEXT NOT NULL,
  PdfFolder TEXT NOT NULL,

  -- Behavior
  CleanupTemp INTEGER NOT NULL,
  ConflictBehavior TEXT NOT NULL,

  -- Toggles
  StorePdfInSharePoint INTEGER NOT NULL,
  ProcessFolderMode INTEGER NOT NULL,
  IgnoreFailuresWhenFolderMode INTEGER NOT NULL,

  -- Auth (app-only)
  TenantId TEXT NOT NULL,
  ClientId TEXT NOT NULL,
  ClientSecret TEXT NOT NULL
);

-- -----------------------------
-- Metrics / events
-- -----------------------------
CREATE TABLE IF NOT EXISTS Runs (
  RunId TEXT PRIMARY KEY,
  StartedAtUtc TEXT NOT NULL,
  EndedAtUtc TEXT NULL,
  Success INTEGER NOT NULL,
  FileCountTotal INTEGER NOT NULL,
  FileCountSucceeded INTEGER NOT NULL,
  FileCountFailed INTEGER NOT NULL,
  TotalInputBytes INTEGER NOT NULL,
  TotalPdfBytes INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS FileEvents (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  RunId TEXT NOT NULL,
  FilePath TEXT NOT NULL,
  FileName TEXT NOT NULL,
  Extension TEXT NOT NULL,
  SizeBytes INTEGER NOT NULL,
  StartedAtUtc TEXT NOT NULL,
  EndedAtUtc TEXT NULL,
  Success INTEGER NOT NULL,
  DriveId TEXT NULL,
  TempItemId TEXT NULL,
  PdfItemId TEXT NULL
);

CREATE TABLE IF NOT EXISTS EventLogs (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  RunId TEXT NOT NULL,
  FileEventId INTEGER NULL,
  TimestampUtc TEXT NOT NULL,
  Level TEXT NOT NULL,
  Stage TEXT NOT NULL,
  PayloadJson TEXT NOT NULL
);

-- Helpful index
CREATE INDEX IF NOT EXISTS IX_FileEvents_Extension ON FileEvents(Extension);
CREATE INDEX IF NOT EXISTS IX_EventLogs_RunId ON EventLogs(RunId);

-- -----------------------------
-- Seed data
-- -----------------------------
INSERT OR IGNORE INTO RefConflictBehavior(Value, Description) VALUES
('fail','Fail if target exists'),
('replace','Overwrite if target exists'),
('rename','Auto-rename if target exists');

INSERT OR IGNORE INTO RefGraphStage(Value, Description) VALUES
('resolveSite','Resolve site from siteUrl'),
('resolveDrive','Resolve document library drive by libraryName'),
('ensureFolder','Ensure temp/pdf folders exist'),
('upload','Upload source file to temp folder'),
('convert','Download PDF via content?format=pdf'),
('storePdf','Upload/store PDF into pdf folder'),
('cleanup','Delete temp drive item');

INSERT OR IGNORE INTO RefLogLevel(Value, Description) VALUES
('Info','Informational'),
('Warn','Warning'),
('Error','Error');

INSERT OR IGNORE INTO RefSettingKey(Key, Description, RefTable) VALUES
('SiteUrl','Target SharePoint site URL',NULL),
('LibraryName','Document library display name',NULL),
('TempFolder','Temp folder under drive root',NULL),
('PdfFolder','PDF folder under drive root ("" disables storing)',NULL),
('CleanupTemp','Delete temp item when done',NULL),
('ConflictBehavior','fail|replace|rename','RefConflictBehavior'),
('StorePdfInSharePoint','If false, do not upload PDF back to SharePoint',NULL),
('ProcessFolderMode','Future toggle: folder processing',NULL),
('IgnoreFailuresWhenFolderMode','Future toggle: continue on errors in folder mode',NULL),
('TenantId','Azure AD tenant ID for app-only',NULL),
('ClientId','Azure AD app client ID',NULL),
('ClientSecret','Azure AD app client secret (demo plaintext)',NULL);
