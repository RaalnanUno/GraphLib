# src-files.ps1
# Generates src-files.xml with ONLY source paths (excludes bin/ and obj/)

$repoRoot = (Resolve-Path ".").Path
$srcPath  = Join-Path $repoRoot "src"
$outFile  = Join-Path $repoRoot "src-files.xml"

$files =
  Get-ChildItem -Path $srcPath -Recurse -File |
  Where-Object {
    $_.FullName -notmatch "\\bin\\" -and
    $_.FullName -notmatch "\\.vs\\" -and
    $_.FullName -notmatch "\\obj\\"
  } |
  Select-Object -ExpandProperty FullName |
  ForEach-Object { $_.Replace($repoRoot + "\", "") } |
  Sort-Object

# Write valid XML
$lines = @()
$lines += "<files>"

foreach ($f in $files) {
  # minimal XML escaping for attribute values
  $escaped = $f `
    -replace '&', '&amp;' `
    -replace '"', '&quot;' `
    -replace '<', '&lt;' `
    -replace '>', '&gt;'

  $lines += "  <file path=""$escaped"" />"
}

$lines += "</files>"

Set-Content -Path $outFile -Encoding UTF8 -Value $lines

Write-Host "Wrote $($files.Count) paths to $outFile"
