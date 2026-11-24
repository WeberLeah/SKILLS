param(
  [Parameter(Mandatory=$true)][string]$DocPath,
  [int]$From = 1,
  [int]$To = 0,
  [switch]$OnlyHeadings
)
# 只读打开，按段落号打印：样式 / 自动编号 / 文本。
# 不指定 -To 时打印到文末。中文段落打印用 PowerShell 控制台，建议先 chcp 65001。
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $DocPath)) { throw "doc not found: $DocPath" }
$word = New-Object -ComObject Word.Application
$word.Visible = $false; $word.DisplayAlerts = 0
$doc = $null
try {
  $doc = $word.Documents.Open($DocPath, $false, $true)
  if ($To -le 0 -or $To -gt $doc.Paragraphs.Count) { $To = $doc.Paragraphs.Count }
  "paragraphs=$($doc.Paragraphs.Count) tables=$($doc.Tables.Count) shapes=$($doc.InlineShapes.Count) pages=$($doc.ComputeStatistics(2))"
  for ($i = $From; $i -le $To; $i++) {
    $p = $doc.Paragraphs.Item($i)
    if ($OnlyHeadings -and $p.Style.NameLocal -notlike '标题*') { continue }
    $t = $p.Range.Text -replace "`r", "" -replace "`a", ""
    "{0,4} [{1}] num=[{2}] :: {3}" -f $i, $p.Style.NameLocal, $p.Range.ListFormat.ListString, $t
  }
}
finally {
  if ($null -ne $doc) { try { $doc.Close($false) } catch {} }
  try { $word.Quit() } catch {}
}
