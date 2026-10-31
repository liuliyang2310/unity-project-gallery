param(
    [string]$Version = '0.11.0-beta',
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$name = "UnityProjectGallery-$Version-$Runtime"
$releaseDirectory = Join-Path $root 'release'
$publishDirectory = Join-Path $releaseDirectory $name
$zipPath = Join-Path $releaseDirectory "$name.zip"
$checksumPath = Join-Path $releaseDirectory "$name.sha256.txt"

New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null

if (Test-Path $publishDirectory) {
    Remove-Item $publishDirectory -Recurse -Force
}

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

if (Test-Path $checksumPath) {
    Remove-Item $checksumPath -Force
}

$project = Join-Path $root 'src\UnityProjectGallery.App\UnityProjectGallery.App.csproj'
dotnet publish $project `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publishDirectory

if ($LASTEXITCODE -ne 0) {
    throw 'dotnet publish 失败。'
}

Get-ChildItem $publishDirectory -Recurse -Filter '*.pdb' -File |
    Remove-Item -Force -ErrorAction SilentlyContinue

Copy-Item (Join-Path $root 'README.md') $publishDirectory
Copy-Item (Join-Path $root 'BETA_RELEASE_NOTES.md') $publishDirectory
Copy-Item (Join-Path $root 'THIRD_PARTY_NOTICES.md') $publishDirectory

Compress-Archive `
    -Path (Join-Path $publishDirectory '*') `
    -DestinationPath $zipPath `
    -CompressionLevel Optimal

$hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash
Set-Content -Path $checksumPath -Value "$hash  $name.zip" -Encoding Ascii

Write-Output "发布完成：$zipPath"
Write-Output "SHA256：$hash"
Write-Output "校验文件：$checksumPath"
