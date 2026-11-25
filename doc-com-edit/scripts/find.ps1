param(
  [Parameter(Mandatory=$true)][string]$DocPath,
  [Parameter(Mandatory=$true)][string]$NeedleFile
)
# NeedleFile: UTF-8 文本，一行一个查找词。中文务必走文件，不要用命令行参数。
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $DocPath)) { throw "doc not found: $DocPath" }
$word = New-Object -ComObject Word.Application
$word.Visible = $false; $word.DisplayAlerts = 0
$doc = $null
try {
  $doc = $word.Documents.Open($DocPath, $false, $true)
  $needles = [System.IO.File]::ReadAllLines($NeedleFile, [System.Text.Encoding]::UTF8) | Where-Object { $_.Trim().Length -gt 0 }
  $texts = New-Object System.Collections.ArrayList
  for ($i = 1; $i -le $doc.Paragraphs.Count; $i++) {
    [void]$texts.Add(($doc.Paragraphs.Item($i).Range.Text -replace "`r", "" -replace "`a", ""))
  }
  "paragraphs=$($texts.Count) tables=$($doc.Tables.Count) shapes=$($doc.InlineShapes.Count)"
  foreach ($n in $needles) {
    "===== [$n] ====="
    $hits = 0
    for ($i = 0; $i -lt $texts.Count; $i++) {
      $t = $texts[$i]
      $idx = $t.IndexOf($n, [System.StringComparison]::Ordinal)
      while ($idx -ge 0) {
        $hits++
        "{0,4} :: {1}" -f ($i + 1), $t
        $idx = $t.IndexOf($n, $idx + 1, [System.StringComparison]::Ordinal)
      }
    }
    "count=$hits"
  }
}
finally {
  if ($null -ne $doc) { try { $doc.Close($false) } catch {} }
  try { $word.Quit() } catch {}
}
