<#
  测试结果上报（局域网）
  ------------------------------------------------------------
  用法：
    .\Send-TestResult.ps1 -Result PASS
    .\Send-TestResult.ps1 -Result FAIL -Station A1 -SN SN001 -Message "电压超限"
    .\Send-TestResult.ps1 -Server 192.0.2.10 -Result PASS

  在测试脚本里调用：
    & "$PSScriptRoot\Send-TestResult.ps1" -Server 192.0.2.10 -Result $r -SN $sn
#>
[CmdletBinding()]
param(
    [string]$Server = "192.0.2.10",
    [int]$Port = 8080,
    [Parameter(Mandatory = $true)][string]$Result,
    [string]$Station = "",
    [string]$SN = "",
    [string]$Message = "",
    [int]$TimeoutSec = 5,
    [switch]$Quiet
)

if ([string]::IsNullOrWhiteSpace($Station)) { $Station = $env:COMPUTERNAME }

$payload = [ordered]@{
    result  = $Result
    station = $Station
    sn      = $SN
    msg     = $Message
    time    = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
}

$json  = $payload | ConvertTo-Json -Compress
$bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
$url   = "http://{0}:{1}/report" -f $Server, $Port

try {
    $null = Invoke-RestMethod -Uri $url -Method Post `
        -ContentType "application/json; charset=utf-8" `
        -Body $bytes -TimeoutSec $TimeoutSec
    if (-not $Quiet) {
        Write-Host ("[OK]   {0,-4}  {1}:{2}  工位={3}  SN={4}  {5}" -f $Result, $Server, $Port, $Station, $SN, $Message) -ForegroundColor Green
    }
    exit 0
}
catch {
    if (-not $Quiet) {
        Write-Host ("[FAIL] 无法连接 {0}  {1}" -f $url, $_.Exception.Message) -ForegroundColor Red
    }
    exit 1
}
