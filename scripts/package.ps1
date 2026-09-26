# Builds the mod and creates a Thunderstore / r2modman compatible zip in dist/.
param([string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

dotnet build "$root\GWYFVR.slnx" -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$version = (Get-Content "$root\thunderstore\manifest.json" -Raw | ConvertFrom-Json).version_number
$stage = Join-Path $root "dist\stage"
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force "$stage\plugins\GWYFVR\RuntimeDeps", "$stage\patchers\GWYFVR" | Out-Null

Copy-Item "$root\thunderstore\manifest.json", "$root\thunderstore\icon.png", "$root\thunderstore\README.md", "$root\thunderstore\CHANGELOG.md" $stage
Copy-Item "$root\LICENSE" $stage -ErrorAction SilentlyContinue
Copy-Item "$root\src\GWYFVR\bin\$Configuration\netstandard2.1\GWYFVR.dll" "$stage\plugins\GWYFVR"
Copy-Item "$root\lib\RuntimeDeps\*" "$stage\plugins\GWYFVR\RuntimeDeps"
Copy-Item "$root\src\GWYFVR.Preload\bin\$Configuration\netstandard2.1\GWYFVR.Preload.dll" "$stage\patchers\GWYFVR"

$zip = Join-Path $root "dist\GWYFVR-$version.zip"
Remove-Item $zip -ErrorAction SilentlyContinue
# Thunderstore needs forward slashes in entry names, which Compress-Archive on Windows PowerShell doesn't use.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem $stage -Recurse -File | ForEach-Object {
        $entry = $_.FullName.Substring($stage.Length + 1).Replace('\', '/')
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $entry)
    }
}
finally {
    $archive.Dispose()
}
Remove-Item $stage -Recurse -Force
Write-Host "Packaged $zip"
