param(
  [Parameter(Mandatory=$true)][string]$DocPath,
  [Parameter(Mandatory=$true)][string]$OpsFile,
  [switch]$UpdateFields,
  [switch]$NoSave,
  [switch]$ForceKillWord
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $DocPath)) { throw "doc not found: $DocPath" }
if (-not (Test-Path $OpsFile)) { throw "ops not found: $OpsFile" }

$dir = Split-Path $DocPath -Parent
$locks = @(Get-ChildItem $dir -Filter '~$*' -ErrorAction SilentlyContinue)
$procs = @(Get-Process WINWORD -ErrorAction SilentlyContinue)
if ($procs.Count -gt 0 -or $locks.Count -gt 0) {
  "WARN: WINWORD running=$($procs.Count), lock files=$($locks.Count)"
  if ($ForceKillWord) {
    $procs | Stop-Process -Force
    Start-Sleep -Seconds 2
    "killed leftover WINWORD"
  } else {
    throw "文档可能正被 Word 占用。请先关闭 Word，或加 -ForceKillWord（会强杀所有 WINWORD，未保存内容会丢失）。"
  }
}

function Get-ParaTexts($doc) {
  $arr = New-Object System.Collections.ArrayList
  for ($i = 1; $i -le $doc.Paragraphs.Count; $i++) {
    [void]$arr.Add(($doc.Paragraphs.Item($i).Range.Text -replace "`r", "" -replace "`a", ""))
  }
  return $arr
}
function Count-InTexts($texts, $needle) {
  $n = 0
  foreach ($t in $texts) {
    $idx = $t.IndexOf($needle, [System.StringComparison]::Ordinal)
    while ($idx -ge 0) { $n++; $idx = $t.IndexOf($needle, $idx + 1, [System.StringComparison]::Ordinal) }
  }
  return $n
}

$tsBefore = (Get-Item $DocPath).LastWriteTime
$word = New-Object -ComObject Word.Application
$word.Visible = $false; $word.DisplayAlerts = 0
$doc = $null
try {
  $doc = $word.Documents.Open($DocPath, $false, $false)
  "opened readonly=$($doc.ReadOnly) saved=$($doc.Saved)"

  $lines = [System.IO.File]::ReadAllLines($OpsFile, [System.Text.Encoding]::UTF8)
  $ok = 0; $fail = 0
  foreach ($line in $lines) {
    if ($line.Trim().Length -eq 0 -or $line.StartsWith('#')) { continue }
    $parts = $line -split "`t"
    $op = $parts[0]

    if ($op -eq 'FIND') {
      $old = $parts[1]; $new = $parts[2]
      $expect = -1
      if ($parts.Count -ge 4) { $expect = [int]$parts[3] }
      $n = Count-InTexts (Get-ParaTexts $doc) $old
      if ($n -eq 0) { "FAIL  FIND [$old] found=0"; $fail++; continue }
      if ($expect -ge 0 -and $n -ne $expect) { "FAIL  FIND [$old] found=$n expected=$expect"; $fail++; continue }

      $f = $doc.Content.Find
      $f.ClearFormatting(); $f.Replacement.ClearFormatting()
      $res = $f.Execute($old, $true, $false, $false, $false, $false, $true, 1, $false, $new, 2)
      $left = Count-InTexts (Get-ParaTexts $doc) $old
      if (-not $res -or $left -ne 0) { "FAIL  FIND [$old] execute=$res remaining=$left"; $fail++; continue }
      "OK    FIND [$old] -> [$new] (replaced=$n)"
      $ok++
    }
    elseif ($op -eq 'PARA') {
      $key = $parts[1]; $new = $parts[2]
      $found = $null; $count = 0; $foundIdx = -1
      for ($i = 1; $i -le $doc.Paragraphs.Count; $i++) {
        $p = $doc.Paragraphs.Item($i)
        $t = ($p.Range.Text -replace "`r", "" -replace "`a", "")
        if ($t.Contains($key)) { $count++; $found = $p; $foundIdx = $i }
      }
      if ($count -ne 1) { "FAIL  PARA key=[$key] matches=$count"; $fail++; continue }
      $r = $found.Range
      $r.MoveEnd(1, -1) | Out-Null
      $r.Text = $new
      "OK    PARA p$foundIdx :: $new"
      $ok++
    }
    elseif ($op -eq 'HEAD') {
      $key = $parts[1]; $new = $parts[2]
      $found = $null; $count = 0; $foundIdx = -1
      for ($i = 1; $i -le $doc.Paragraphs.Count; $i++) {
        $p = $doc.Paragraphs.Item($i)
        $t = ($p.Range.Text -replace "`r", "" -replace "`a", "")
        if ($t.Trim() -eq $key -and $p.Style.NameLocal -like '标题*') { $count++; $found = $p; $foundIdx = $i }
      }
      if ($count -ne 1) { "FAIL  HEAD key=[$key] matches=$count"; $fail++; continue }
      $r = $found.Range
      $r.MoveEnd(1, -1) | Out-Null
      $r.Text = $new
      "OK    HEAD p$foundIdx :: $new"
      $ok++
    }
    elseif ($op -eq 'STYLE') {
      $key = $parts[1]; $styleId = [int]$parts[2]; $rmNum = $parts[3]
      $found = $null; $count = 0; $foundIdx = -1
      for ($i = 1; $i -le $doc.Paragraphs.Count; $i++) {
        $p = $doc.Paragraphs.Item($i)
        $t = ($p.Range.Text -replace "`r", "" -replace "`a", "")
        if ($t.Trim() -eq $key) { $count++; $found = $p; $foundIdx = $i }
      }
      if ($count -ne 1) { "FAIL  STYLE key=[$key] matches=$count"; $fail++; continue }
      if ($rmNum -eq 'yes') { $found.Range.ListFormat.RemoveNumbers() | Out-Null }
      $found.Style = $doc.Styles.Item($styleId)
      "OK    STYLE p$foundIdx -> $($found.Style.NameLocal), num=[$($found.Range.ListFormat.ListString)]"
      $ok++
    }
    elseif ($op -eq 'PIDX') {
      $idx = [int]$parts[1]; $new = $parts[2]
      if ($idx -lt 1 -or $idx -gt $doc.Paragraphs.Count) { "FAIL  PIDX $idx out of range"; $fail++; continue }
      $p = $doc.Paragraphs.Item($idx)
      $old = ($p.Range.Text -replace "`r", "" -replace "`a", "")
      $r = $p.Range
      $r.MoveEnd(1, -1) | Out-Null
      $r.Text = $new
      "OK    PIDX p$idx [$old] -> [$new]"
      $ok++
    }
    elseif ($op -eq 'PDEL') {
      $idx = [int]$parts[1]
      if ($idx -lt 1 -or $idx -gt $doc.Paragraphs.Count) { "FAIL  PDEL $idx out of range"; $fail++; continue }
      $p = $doc.Paragraphs.Item($idx)
      $old = ($p.Range.Text -replace "`r", "" -replace "`a", "")
      if ($old.Trim().Length -ne 0) { "FAIL  PDEL p$idx not empty: [$old]"; $fail++; continue }
      $p.Range.Delete() | Out-Null
      "OK    PDEL p$idx (was empty)"
      $ok++
    }
    elseif ($op -eq 'PDELX') {
      $idx = [int]$parts[1]
      if ($idx -lt 1 -or $idx -gt $doc.Paragraphs.Count) { "FAIL  PDELX $idx out of range"; $fail++; continue }
      $p = $doc.Paragraphs.Item($idx)
      $old = ($p.Range.Text -replace "`r", "" -replace "`a", "")
      $p.Range.Delete() | Out-Null
      "OK    PDELX p$idx (was [$old])"
      $ok++
    }
    else { "SKIP  unknown op: $op"; $fail++ }
  }

  ""
  "applied OK=$ok FAIL=$fail"
  "modified=$(-not $doc.Saved)"

  if ($UpdateFields) {
    foreach ($fd in $doc.Fields) { try { $fd.Update() | Out-Null } catch {} }
    foreach ($toc in $doc.TablesOfContents) { $toc.Update() }
    "fields updated"
  }

  if (-not $NoSave) {
    if ($doc.Saved) {
      "nothing to save (doc.Saved = True)"
    } else {
      $doc.Save()
      $tsAfter = (Get-Item $DocPath).LastWriteTime
      if ($tsAfter -le $tsBefore) { "WARN: timestamp not advanced ($tsBefore -> $tsAfter)" }
      "saved; ts=$tsAfter"
    }
  }
}
finally {
  if ($null -ne $doc) { try { $doc.Close($false) } catch {} }
  try { $word.Quit() } catch {}
  try { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($word) | Out-Null } catch {}
}
