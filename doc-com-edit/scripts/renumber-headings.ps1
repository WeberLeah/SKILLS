param(
  [Parameter(Mandatory=$true)][string]$DocPath,
  [string]$AnchorText = '概述',
  [string]$AnchorStyle = '标题 1',
  [string]$NumId = '',
  [switch]$DryRun
)
# 修复“标题编号从 1 重排”：某些标题被挂上了直接编号(numId)，
# 导致 Word 重新从 1 开始编号。做法是对每个目标标题 RemoveNumbers，
# 再用锚点标题的列表模板 ApplyListTemplateWithLevel(continue)。
# 不给 -NumId 时，自动找出与锚点 numId 不同的直接编号标题。
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $DocPath)) { throw "doc not found: $DocPath" }
$word = New-Object -ComObject Word.Application
$word.Visible = $false; $word.DisplayAlerts = 0
$doc = $null
try {
  $doc = $word.Documents.Open($DocPath, $false, $DryRun)
  $pSrc = $null
  for ($i = 1; $i -le $doc.Paragraphs.Count; $i++) {
    $p = $doc.Paragraphs.Item($i)
    $t = (($p.Range.Text -replace "`r", "").Trim())
    if ($t -eq $AnchorText -and $p.Style.NameLocal -eq $AnchorStyle) { $pSrc = $p; break }
  }
  if ($null -eq $pSrc) { throw "anchor heading not found: [$AnchorText] style=[$AnchorStyle]" }

  $srcNum = ''
  if ($pSrc.Range.WordOpenXML -match '<w:numId w:val="(\d+)"') { $srcNum = $Matches[1] }
  "anchor numId=[$srcNum]"

  $targets = @()
  for ($i = 1; $i -le $doc.Paragraphs.Count; $i++) {
    $p = $doc.Paragraphs.Item($i)
    if ($p.Style.NameLocal -notlike '标题*') { continue }
    if ($p.Range.WordOpenXML -notmatch '<w:numId w:val="(\d+)"') { continue }
    $nid = $Matches[1]
    $hit = $false
    if ($NumId) { $hit = ($nid -eq $NumId) }
    else { $hit = ($nid -ne $srcNum) }
    if ($hit) { $targets += ,@($i, $p, $nid) }
  }

  "targets=$($targets.Count)"
  foreach ($tp in $targets) {
    $txt = $tp[1].Range.Text -replace "`r", ""
    "  p$($tp[0]) numId=$($tp[2]) num=[$($tp[1].Range.ListFormat.ListString)] :: $txt"
  }

  if ($DryRun) { "dry run, nothing changed"; return }

  $tpl = $pSrc.Range.ListFormat.ListTemplate
  foreach ($tp in $targets) {
    $tp[1].Range.ListFormat.RemoveNumbers() | Out-Null
    $tp[1].Range.ListFormat.ApplyListTemplateWithLevel($tpl, $true, 2, 2)
  }

  ""
  "--- after ---"
  for ($i = 1; $i -le $doc.Paragraphs.Count; $i++) {
    $p = $doc.Paragraphs.Item($i)
    if ($p.Style.NameLocal -notlike '标题*') { continue }
    "{0,4} [{1}] num=[{2}] :: {3}" -f $i, $p.Style.NameLocal, $p.Range.ListFormat.ListString, ($p.Range.Text -replace "`r", "")
  }

  $doc.Save()
  "saved; ts=$((Get-Item $DocPath).LastWriteTime)"
}
finally {
  if ($null -ne $doc) { try { $doc.Close($false) } catch {} }
  try { $word.Quit() } catch {}
}
