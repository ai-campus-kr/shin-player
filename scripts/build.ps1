param([switch]$SkipNative, [switch]$AppOnly, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $SkipNative -and -not $AppOnly) { & (Join-Path $PSScriptRoot 'fetch-native.ps1') }
$publishPath = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } elseif ($AppOnly) { Join-Path $projectRoot 'dist\ShinPlayer-app' } else { Join-Path $projectRoot 'dist\ShinPlayer' }
$nativeOption = if ($AppOnly) { 'false' } else { 'true' }
& dotnet publish (Join-Path $projectRoot 'src\ShinPlayer\ShinPlayer.csproj') -c Release -r win-x64 --self-contained true -p:IncludeNativeEngine=$nativeOption -o $publishPath --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
if ($AppOnly -and (Test-Path -LiteralPath (Join-Path $publishPath 'libmpv-2.dll'))) { throw 'App-only output contains a native engine. Use an empty output folder.' }
$runtimeConfig = Get-Content -LiteralPath (Join-Path $publishPath 'ShinPlayer.runtimeconfig.json') -Raw | ConvertFrom-Json
$nugetRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
foreach ($framework in $runtimeConfig.runtimeOptions.includedFrameworks) {
    $packageName = $framework.name.ToLowerInvariant() + '.runtime.win-x64'
    $packagePath = Join-Path $nugetRoot ($packageName + '\' + $framework.version)
    $licenseFolder = if ($framework.name -eq 'Microsoft.NETCore.App') { 'dotnet-runtime' } else { 'dotnet-windowsdesktop' }
    $licensePath = Join-Path $publishPath ('licenses\' + $licenseFolder)
    New-Item -ItemType Directory -Force -Path $licensePath | Out-Null
    Get-ChildItem -LiteralPath $packagePath -File | Where-Object { $_.Name -match 'LICENSE|NOTICE' } | Copy-Item -Destination $licensePath -Force
}
$webViewVersion = ([xml](Get-Content -LiteralPath (Join-Path $projectRoot 'src\ShinPlayer\ShinPlayer.csproj') -Raw)).Project.ItemGroup.PackageReference | Where-Object { $_.Include -eq 'Microsoft.Web.WebView2' } | Select-Object -ExpandProperty Version
$webViewPackage = Join-Path $nugetRoot ('microsoft.web.webview2\' + $webViewVersion)
$webViewLicense = Join-Path $publishPath 'licenses\webview2-sdk'
New-Item -ItemType Directory -Force -Path $webViewLicense | Out-Null
foreach ($name in @('LICENSE.txt','NOTICE.txt')) { Copy-Item -LiteralPath (Join-Path $webViewPackage $name) -Destination $webViewLicense -Force }
foreach ($name in @('README.md','README.en.md','README.ja.md','README.zh-CN.md','LICENSE','THIRD-PARTY-NOTICES.md','CHANGELOG.md')) { Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $publishPath -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'uninstall.ps1') -Destination $publishPath -Force
$sourcePath = Join-Path $projectRoot ('artifacts\source-packages\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $sourcePath | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'scripts') -Destination $sourcePath -Recurse -Force
$srcPath = Join-Path $sourcePath 'src\ShinPlayer'
New-Item -ItemType Directory -Force -Path $srcPath | Out-Null
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src\ShinPlayer') -File | Copy-Item -Destination $srcPath -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\ShinPlayer\Assets') -Destination $srcPath -Recurse -Force
foreach ($name in @('README.md','README.en.md','README.ja.md','README.zh-CN.md','LICENSE','THIRD-PARTY-NOTICES.md','CHANGELOG.md','.gitignore')) { Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $sourcePath -Force }
Copy-Item -LiteralPath (Join-Path $projectRoot '.github') -Destination $sourcePath -Recurse -Force
Compress-Archive -Path (Join-Path $sourcePath '*') -DestinationPath (Join-Path $publishPath 'ShinPlayer-source.zip') -Force
Write-Output "Built: $publishPath"
