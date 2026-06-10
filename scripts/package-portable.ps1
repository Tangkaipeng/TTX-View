param(
    [string]$Version = "v0.1.0",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path -LiteralPath (Join-Path $scriptRoot "..")
$projectPath = Join-Path $repoRoot "TTXView.Wpf\TTXView.Wpf.csproj"
$nugetConfig = Join-Path $repoRoot "NuGet.Config"
$publishDir = Join-Path $repoRoot "dist\portable\TTXView"
$artifactsDir = Join-Path $repoRoot "artifacts"
$stagingDir = Join-Path $artifactsDir "TTXView"
$zipName = "TTXView-$Version-$Runtime-portable.zip"
$zipPath = Join-Path $artifactsDir $zipName
$hashPath = "$zipPath.sha256"
$localDotnet = Join-Path $repoRoot ".dotnet\dotnet.exe"
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { "dotnet" }

function Remove-DirectoryIfExists {
    param([string]$Path)

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

Push-Location $repoRoot
try {
    $env:DOTNET_CLI_HOME = Join-Path $repoRoot ".dotnet_home"
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
    $env:APPDATA = Join-Path $repoRoot ".appdata"
    $env:NUGET_PACKAGES = Join-Path $repoRoot ".nuget_packages"

    Remove-DirectoryIfExists (Join-Path $repoRoot "dist\portable")
    Remove-DirectoryIfExists $artifactsDir
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
    New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

    & $dotnet restore $projectPath --configfile $nugetConfig
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    & $dotnet publish $projectPath `
        -c Release `
        -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -o $publishDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Get-ChildItem -LiteralPath $publishDir -Force |
        Where-Object { $_.Name -notlike "*.pdb" } |
        Copy-Item -Destination $stagingDir -Recurse -Force

    Copy-Item -LiteralPath (Join-Path $repoRoot "README.md") -Destination (Join-Path $stagingDir "README.md") -Force

    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $stagingDir "*") -DestinationPath $zipPath -CompressionLevel Optimal -Force
    Remove-DirectoryIfExists $stagingDir

    $hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $zipName" | Set-Content -LiteralPath $hashPath -Encoding ASCII

    Write-Host "Portable package created:"
    Write-Host "  $zipPath"
    Write-Host "  $hashPath"
}
finally {
    Pop-Location
}
