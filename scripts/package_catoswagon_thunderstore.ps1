[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $repoRoot 'MODS\thunderstore'
$stagingRoot = Join-Path $outputDir ('.staging-' + [guid]::NewGuid().ToString('N'))
$projectPath = Join-Path $repoRoot 'src\CatosWagon\CatosWagon.csproj'
$pluginPath = Join-Path $repoRoot 'src\CatosWagon\Plugin.cs'
$dllPath = Join-Path $repoRoot 'src\CatosWagon\bin\Release\net48\net48\CatosWagon.dll'
$iconSourcePath = Join-Path $repoRoot 'Assets\icon.png'

function Get-PluginVersion([string]$path) {
    $source = Get-Content -LiteralPath $path -Raw
    if ($source -notmatch 'public\s+const\s+string\s+VERSION\s*=\s*"(?<version>\d+\.\d+\.\d+)"') {
        throw "Could not read a semantic plugin version from $path"
    }
    return $Matches.version
}

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$version = Get-PluginVersion $pluginPath
$zipPath = Join-Path $outputDir "CatosWagon-$version.zip"

New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

try {
    Write-Host "Building CatosWagon $version..."
    & dotnet build $projectPath -c Release
    if ($LASTEXITCODE -ne 0) { throw "Build failed (exit code $LASTEXITCODE)." }
    if (-not (Test-Path -LiteralPath $dllPath -PathType Leaf)) {
        throw "Expected build output was not created: $dllPath"
    }

    $packageRoot = Join-Path $stagingRoot 'CatosWagon'
    $pluginDir = Join-Path $packageRoot 'BepInEx\plugins'
    New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null

    $manifest = [ordered]@{
        name = 'CatosWagon'
        version_number = $version
        website_url = ''
        description = 'Standalone decorative market wagons for Valheim.'
        dependencies = @(
            'denikson-BepInExPack_Valheim-5.4.2333',
            'ValheimModding-Jotunn-2.30.2'
        )
    }
    [System.IO.File]::WriteAllText(
        (Join-Path $packageRoot 'manifest.json'),
        ($manifest | ConvertTo-Json -Depth 4),
        [System.Text.UTF8Encoding]::new($false)
    )
    [System.IO.File]::WriteAllText(
        (Join-Path $packageRoot 'README.md'),
        (Get-Content -LiteralPath (Join-Path $repoRoot 'README.md') -Raw),
        [System.Text.UTF8Encoding]::new($false)
    )

    Copy-Item -LiteralPath $iconSourcePath -Destination (Join-Path $packageRoot 'icon.png')
    $icon = [System.Drawing.Image]::FromFile((Join-Path $packageRoot 'icon.png'))
    try {
        if ($icon.Width -ne 256 -or $icon.Height -ne 256) {
            throw 'The Thunderstore icon must be exactly 256x256 pixels.'
        }
    }
    finally { $icon.Dispose() }

    Copy-Item -LiteralPath $dllPath -Destination (Join-Path $pluginDir 'CatosWagon.dll')

    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    $zipFileStream = [System.IO.File]::Open($zipPath, [System.IO.FileMode]::CreateNew)
    $zipArchive = [System.IO.Compression.ZipArchive]::new(
        $zipFileStream,
        [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem -LiteralPath $packageRoot -Recurse -File | ForEach-Object {
            $relativeEntry = $_.FullName.Substring($packageRoot.Length).TrimStart([char[]]@('\', '/')).Replace('\', '/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zipArchive,
                $_.FullName,
                $relativeEntry,
                [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally {
        $zipArchive.Dispose()
        $zipFileStream.Dispose()
    }

    $archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $entries = @($archive.Entries | ForEach-Object { $_.FullName })
        foreach ($requiredEntry in @('manifest.json', 'README.md', 'icon.png', 'BepInEx/plugins/CatosWagon.dll')) {
            if ($entries -cnotcontains $requiredEntry) {
                throw "$zipPath is missing required entry '$requiredEntry'."
            }
        }
    }
    finally { $archive.Dispose() }

    Write-Host "Created $zipPath"
}
finally {
    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
