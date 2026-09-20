param([string]$ReportName = 'installed-verification')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $env:LOCALAPPDATA 'Programs\ShinPlayer\ShinPlayer.exe'
$fixture = Join-Path $projectRoot 'artifacts\fixtures\한글 영상 sample.mp4'
function Get-ShinStatus {
    $response = & $exe --status | Out-String
    if (-not $response.Trim()) { throw 'No response from installed app.' }
    return $response | ConvertFrom-Json
}
$deadline = [DateTime]::UtcNow.AddSeconds(30)
do {
    $standby = Get-ShinStatus
    if ($standby.engineReady -and -not $standby.visible) { break }
    Start-Sleep -Milliseconds 200
} while ([DateTime]::UtcNow -lt $deadline)
if (-not $standby.engineReady -or $standby.visible) { throw 'Standby is not prepared and hidden.' }
$existing = @(Get-Process -Name ShinPlayer | Where-Object { $_.Path -eq $exe })
if ($existing.Count -ne 1) { throw 'Expected one installed background process.' }
$originalPid = $existing[0].Id
Start-Sleep -Milliseconds 1500
$cpuBefore = $existing[0].TotalProcessorTime
Start-Sleep -Milliseconds 1500
$idleCpuMs = ($existing[0].TotalProcessorTime - $cpuBefore).TotalMilliseconds
$launchWatch = [Diagnostics.Stopwatch]::StartNew()
$forwarder = Start-Process -FilePath $exe -ArgumentList ('"' + $fixture + '"') -WindowStyle Hidden -PassThru
if (-not $forwarder.WaitForExit(5000)) { throw 'File forwarding timed out.' }
$forwardMs = ($forwarder.ExitTime - $forwarder.StartTime).TotalMilliseconds
$deadline = [DateTime]::UtcNow.AddSeconds(10)
do {
    $playing = Get-ShinStatus
    if ($playing.loaded -and $playing.path -eq $fixture -and $playing.position -gt 0.1) { break }
    Start-Sleep -Milliseconds 50
} while ([DateTime]::UtcNow -lt $deadline)
$observedPlaybackMs = $launchWatch.Elapsed.TotalMilliseconds
if (-not $playing.loaded -or $playing.path -ne $fixture -or -not $playing.visible) { throw 'Installed player did not open the file.' }
$after = @(Get-Process -Name ShinPlayer | Where-Object { $_.Path -eq $exe })
if ($after.Count -ne 1 -or $after[0].Id -ne $originalPid) { throw 'Single-instance forwarding failed.' }
& $exe --hide | Out-Null
Start-Sleep -Milliseconds 350
$hidden = Get-ShinStatus
if ($hidden.visible -or -not $hidden.idle) { throw 'Hide did not stop and return to tray.' }
$registration = Get-ItemProperty -LiteralPath 'HKCU:\Software\ShinPlayer\Capabilities\FileAssociations'
$extensions = @($registration.PSObject.Properties | Where-Object { $_.Name.StartsWith('.') })
if ($extensions.Count -ne 30) { throw 'Video association registration is incomplete.' }
$command = (Get-Item -LiteralPath 'HKCU:\Software\Classes\ShinPlayer.Video\shell\open\command').GetValue('')
if ($command -ne ('"' + $exe + '" "%1"')) { throw 'File association command does not quote its file path.' }
$startup = (Get-Item -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run').GetValue('ShinPlayer')
if ($startup -ne ('"' + $exe + '" --background')) { throw 'Background startup registration is missing.' }
$report = [ordered]@{ passed = $true; executable = $exe; pid = $originalPid; forwardingProcessMs = $forwardMs; observedPlaybackMsIncludingStatusProbe = $observedPlaybackMs; nativeLoadMs = $playing.loadMs; playbackReadyMs = $playing.playbackReadyMs; idleCpuMsOver1500ms = $idleCpuMs; idleWorkingSetMB = $existing[0].WorkingSet64 / 1MB; registeredVideoExtensions = $extensions.Count; finalState = $hidden; defaultAppSelection = 'Registered as candidate; Windows user selection still required.' }
$output = Join-Path $projectRoot ('artifacts\' + $ReportName + '.json')
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $output -Encoding UTF8
$report | ConvertTo-Json -Depth 6
