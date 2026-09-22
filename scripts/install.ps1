param([switch]$NoStartup, [switch]$NoLaunch, [string]$SourcePath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$publishPath = if ($SourcePath) { [IO.Path]::GetFullPath($SourcePath) } else { Join-Path $projectRoot 'dist\ShinPlayer' }
$installPath = Join-Path $env:LOCALAPPDATA 'Programs\ShinPlayer'
$exePath = Join-Path $installPath 'ShinPlayer.exe'
if (-not (Test-Path -LiteralPath (Join-Path $publishPath 'ShinPlayer.exe'))) { throw 'Run scripts\build.ps1 first.' }
$isUpdate = Test-Path -LiteralPath $exePath
$startupWasEnabled = $null -ne (Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name ShinPlayer -ErrorAction SilentlyContinue)
$enginePath = Join-Path $publishPath 'libmpv-2.dll'
if (-not (Test-Path -LiteralPath $enginePath)) {
    Write-Output 'Downloading the pinned playback engine from its upstream GitHub release (cached after first install).'
    $engineCache = Join-Path $env:LOCALAPPDATA 'ShinPlayer\engine-cache'
    & (Join-Path $PSScriptRoot 'fetch-native.ps1') -NativePath $engineCache
    $enginePath = Join-Path $engineCache 'libmpv-2.dll'
}
if (Test-Path -LiteralPath $exePath) {
    $closing = Start-Process -FilePath $exePath -ArgumentList '--exit' -PassThru -WindowStyle Hidden
    if (-not $closing.WaitForExit(10000)) { throw 'Could not close the previous version.' }
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $running = Get-Process -Name ShinPlayer -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exePath }
        if (-not $running) { break }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($running) { throw 'ShinPlayer is still running. Close it before updating.' }
}
New-Item -ItemType Directory -Force -Path $installPath | Out-Null
Get-ChildItem -LiteralPath $publishPath | Copy-Item -Destination $installPath -Recurse -Force
# Remove only obsolete developer files from older installs; keep runtime and user data intact.
foreach ($name in @('ShinPlayer.pdb', 'Microsoft.Web.WebView2.Core.xml', 'Microsoft.Web.WebView2.Wpf.xml', 'Microsoft.Web.WebView2.WinForms.xml')) {
    $obsolete = Join-Path $installPath $name
    if (-not (Test-Path -LiteralPath (Join-Path $publishPath $name)) -and (Test-Path -LiteralPath $obsolete)) { Remove-Item -LiteralPath $obsolete -Force }
}
Copy-Item -LiteralPath $enginePath -Destination (Join-Path $installPath 'libmpv-2.dll') -Force
$registered = Start-Process -FilePath $exePath -ArgumentList '--register' -PassThru -WindowStyle Hidden
$registered.WaitForExit()
if ($registered.ExitCode -ne 0) { throw 'Application registration failed.' }
if (-not $NoStartup -and (-not $isUpdate -or $startupWasEnabled)) {
    $startup = Start-Process -FilePath $exePath -ArgumentList '--enable-startup' -PassThru -WindowStyle Hidden
    $startup.WaitForExit()
    if ($startup.ExitCode -ne 0) { throw 'Startup registration failed.' }
}
$shellObject = New-Object -ComObject WScript.Shell
$shortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) '신플레이어.lnk'
$shortcut = $shellObject.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $installPath
$shortcut.IconLocation = "$exePath,0"
$shortcut.Description = '신플레이어 - 빠른 Windows 영상 플레이어'
$shortcut.Save()
if (-not $SourcePath) {
    $localShortcut = $shellObject.CreateShortcut((Join-Path $projectRoot '신플레이어.lnk'))
    $localShortcut.TargetPath = $exePath
    $localShortcut.WorkingDirectory = $installPath
    $localShortcut.IconLocation = "$exePath,0"
    $localShortcut.Save()
}
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ShinPlayer'
New-Item -Path $uninstallKey -Force | Out-Null
$uninstallCommand = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + (Join-Path $installPath 'uninstall.ps1') + '"'
$version = (Get-Item -LiteralPath $exePath).VersionInfo.ProductVersion.Split('+')[0]
$values = @{ DisplayName = '신플레이어'; DisplayVersion = $version; Publisher = 'ShinPlayer'; DisplayIcon = "$exePath,0"; InstallLocation = $installPath; UninstallString = $uninstallCommand; QuietUninstallString = $uninstallCommand }
foreach ($key in $values.Keys) { New-ItemProperty -Path $uninstallKey -Name $key -Value $values[$key] -PropertyType String -Force | Out-Null }
New-ItemProperty -Path $uninstallKey -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null
$size = [int]((Get-ChildItem -LiteralPath $installPath -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1024)
New-ItemProperty -Path $uninstallKey -Name EstimatedSize -Value $size -PropertyType DWord -Force | Out-Null
if (-not $NoLaunch) { Start-Process -FilePath $exePath -ArgumentList '--background' -WindowStyle Hidden }
Write-Output "Installed: $exePath"
Write-Output 'Default apps: ms-settings:defaultapps?registeredAppUser=ShinPlayer'
