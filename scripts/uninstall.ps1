$ErrorActionPreference = 'Stop'
$expectedPath = [System.IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\ShinPlayer'))
$targetPath = [System.IO.Path]::GetFullPath($PSScriptRoot)
if (-not $targetPath.Equals($expectedPath, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Uninstaller must run from the installed ShinPlayer directory.' }
$exePath = Join-Path $targetPath 'ShinPlayer.exe'
if (Test-Path -LiteralPath $exePath) {
    $closing = Start-Process -FilePath $exePath -ArgumentList '--exit' -PassThru -WindowStyle Hidden
    if (-not $closing.WaitForExit(10000)) { throw 'Could not close ShinPlayer.' }
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $running = Get-Process -Name ShinPlayer -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exePath }
        if (-not $running) { break }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($running) { throw 'ShinPlayer is still running.' }
    $registration = Start-Process -FilePath $exePath -ArgumentList '--unregister' -PassThru -WindowStyle Hidden
    $registration.WaitForExit()
    if ($registration.ExitCode -ne 0) { throw 'Unregister failed.' }
}
$shortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) '신플레이어.lnk'
if (Test-Path -LiteralPath $shortcutPath) { Remove-Item -LiteralPath $shortcutPath }
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ShinPlayer'
if (Test-Path -LiteralPath $uninstallKey) { Remove-Item -LiteralPath $uninstallKey -Recurse }
# Both paths have been resolved and checked against the one exact per-user install target above.
Remove-Item -LiteralPath $targetPath -Recurse -Force
# Keep the user's settings/history in LocalAppData\ShinPlayer for reinstalls.
