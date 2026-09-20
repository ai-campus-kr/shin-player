$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$version = ([xml](Get-Content -LiteralPath (Join-Path $projectRoot 'src\ShinPlayer\ShinPlayer.csproj') -Raw -Encoding UTF8)).Project.PropertyGroup.Version
$stage = Join-Path $projectRoot ('artifacts\release-staging\' + [Guid]::NewGuid().ToString('N'))
$appPath = Join-Path $stage 'app'
& (Join-Path $PSScriptRoot 'build.ps1') -AppOnly -OutputDirectory $appPath
if (Test-Path -LiteralPath (Join-Path $appPath 'libmpv-2.dll')) { throw 'The public archive must not bundle the prebuilt engine.' }
$scriptPath = Join-Path $stage 'scripts'
New-Item -ItemType Directory -Force -Path $scriptPath | Out-Null
foreach ($name in @('install.ps1', 'fetch-native.ps1')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $scriptPath }
$launcher = @'
@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\install.ps1" -SourcePath "%~dp0app"
if errorlevel 1 (
  echo Installation failed. Read the message above and retry Install.cmd.
  pause
  exit /b 1
)
echo Installed. Open ShinPlayer from the Start menu.
pause
'@
[IO.File]::WriteAllText((Join-Path $stage 'Install.cmd'), ($launcher -replace "`r?`n", "`r`n"), [Text.Encoding]::ASCII)
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $stage
$output = Join-Path $projectRoot 'dist\release'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$binaryZip = Join-Path $output "ShinPlayer-$version-win-x64.zip"
$sourceZip = Join-Path $output "ShinPlayer-$version-source.zip"
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $binaryZip -Force
Copy-Item -LiteralPath (Join-Path $appPath 'ShinPlayer-source.zip') -Destination $sourceZip -Force
$checksums = foreach ($path in @($binaryZip, $sourceZip)) { '{0}  {1}' -f (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant(), [IO.Path]::GetFileName($path) }
$checksums | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ASCII
Write-Output "Release files: $output"
Write-Output "Installer staging directory: $stage"
