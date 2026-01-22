INSERT INTO AppSettings (Key, Value, UpdatedUtc)
VALUES
  ('LogLevel', 'Information', datetime('now')),
  ('TempFolder', 'DocLibTemp', datetime('now')),
  ('PdfFolder', 'DocLibPdf', datetime('now')),
  ('CleanupTemp', 'true', datetime('now')),
  ('ConflictBehavior', 'replace', datetime('now'))
ON CONFLICT(Key)
DO UPDATE SET
  Value = excluded.Value,
  UpdatedUtc = datetime('now');
