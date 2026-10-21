param(
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectRoot = Join-Path $repoRoot 'src\OtpClipboard'
$projectFile = Join-Path $projectRoot 'OtpClipboard.csproj'
$developmentManifest = Join-Path $projectRoot 'Package.Development.appxmanifest'
$layoutRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot '.local\development-layout'))
$localRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot '.local'))

if (-not $layoutRoot.StartsWith(
        $localRoot + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Development layout must stay inside the repository .local directory.'
}

if (Test-Path -LiteralPath $layoutRoot) {
    Remove-Item -LiteralPath $layoutRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $layoutRoot | Out-Null

dotnet build $projectFile -c $Configuration -p:Platform=x64 --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

$outputRoot = Join-Path $projectRoot "bin\x64\$Configuration\net8.0-windows10.0.26100.0\win-x64"
Copy-Item -Path (Join-Path $outputRoot '*') -Destination $layoutRoot -Recurse -Force
$targetManifest = Join-Path $layoutRoot 'AppxManifest.xml'
Copy-Item -LiteralPath $developmentManifest -Destination $targetManifest -Force

return $layoutRoot
