param([string]$NativePath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $NativePath) { $NativePath = Join-Path $projectRoot 'native' }
$archive = Join-Path $nativePath 'mpv-dev.7z'
$expectedHash = '60f9102db46aea8cef9bfb4345ee6a106f34fdbd1df9587e38f0660688039341'
$url = 'https://github.com/shinchiro/mpv-winbuild-cmake/releases/download/20260920/mpv-dev-x86_64-20260920-git-e76a35ec95.7z'
New-Item -ItemType Directory -Force -Path $nativePath | Out-Null
if (-not (Test-Path -LiteralPath $archive)) {
    $download = $archive + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    try {
        $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest -Uri $url -OutFile $download -UseBasicParsing
        if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Native engine download SHA256 mismatch.' }
        Move-Item -LiteralPath $download -Destination $archive -Force
    } finally {
        if (Test-Path -LiteralPath $download) { Remove-Item -LiteralPath $download }
    }
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Native engine archive SHA256 mismatch.' }
& tar -xf $archive -C $nativePath
if ($LASTEXITCODE -ne 0) { throw 'Native engine extraction failed.' }
Write-Output 'Verified and extracted libmpv-2.dll.'
