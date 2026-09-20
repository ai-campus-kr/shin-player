param([string]$Executable, [string]$ReportName = 'verification')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $Executable) { $Executable = Join-Path $projectRoot 'dist\ShinPlayer\ShinPlayer.exe' }
& python (Join-Path $PSScriptRoot 'create-test-media.py')
if ($LASTEXITCODE -ne 0) { throw 'Fixture generation failed.' }
$fixtures = Join-Path $projectRoot 'artifacts\fixtures'
$reportPath = Join-Path $projectRoot ('artifacts\' + $ReportName)
foreach ($name in @('complete.txt', 'results.json')) {
    $oldReport = Join-Path $reportPath $name
    if (Test-Path -LiteralPath $oldReport) { Remove-Item -LiteralPath $oldReport }
}
$process = Start-Process -FilePath $Executable -ArgumentList @('--self-test',('"' + $fixtures + '"'),('"' + $reportPath + '"')) -PassThru -WindowStyle Hidden
if (-not $process.WaitForExit(120000)) { throw 'Integration test timed out. Inspect the test process and artifacts.' }
$result = Get-Content -LiteralPath (Join-Path $reportPath 'results.json') -Raw | ConvertFrom-Json
$result.tests | Select-Object name,passed,elapsedMs | Format-Table -AutoSize
if ($process.ExitCode -ne 0 -or $result.failures -gt 0 -or -not (Test-Path -LiteralPath (Join-Path $reportPath 'complete.txt'))) { throw 'Integration test failed.' }
Write-Output "PASS: $($result.tests.Count) integration checks. Reports: $reportPath"
