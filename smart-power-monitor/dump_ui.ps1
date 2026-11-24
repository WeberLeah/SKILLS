param(
    [string]$ProcessName = 'Smart_power_monitor',
    [string]$Out = ''
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Output "未找到进程 $ProcessName"; exit 1 }

$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
if (-not $win) { Write-Output "未找到主窗口"; exit 1 }

$allCond = [System.Windows.Automation.Condition]::TrueCondition
$all = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $allCond)

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("window = " + $win.Current.Name + "  (PID " + $proc.Id + ")")
$lines.Add("descendants = " + $all.Count)
$lines.Add('')
$lines.Add('NAME                 X      Y      W      H     TYPE                 ID')

foreach ($e in $all) {
    try {
        $n = $e.Current.Name
        if ([string]::IsNullOrWhiteSpace($n)) { continue }
        $r = $e.Current.BoundingRectangle
        $t = $e.Current.ControlType.ProgrammaticName.Replace('ControlType.', '')
        $lines.Add(('{0,-20} {1,6:F0} {2,6:F0} {3,6:F0} {4,6:F0} {5,-20} {6}' -f
            $n, $r.X, $r.Y, $r.Width, $r.Height, $t, $e.Current.AutomationId))
    } catch { }
}

if ($Out) {
    [IO.File]::WriteAllLines($Out, $lines, (New-Object Text.UTF8Encoding($true)))
    Write-Output ("written -> " + $Out + "  (" + $lines.Count + " lines)")
} else {
    $lines | ForEach-Object { Write-Output $_ }
}
