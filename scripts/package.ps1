[CmdletBinding()]
param(
    [string]$Ref = 'HEAD',
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+$')]
    [string]$Version = '1.1.1',
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$repo = (Get-Item -LiteralPath (Join-Path $PSScriptRoot '..')).FullName
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repo 'dist' }
$outDir = [IO.Directory]::CreateDirectory($OutputDirectory).FullName
$destination = Join-Path $outDir ("SkinLiveOBS-Windows-v{0}.zip" -f $Version)
$id = [Guid]::NewGuid().ToString('N')
$pending = Join-Path $outDir ('.SkinLiveOBS-' + $id + '.zip')
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$work = [IO.Directory]::CreateDirectory((Join-Path $tempRoot ('SkinLiveOBS-release-' + $id))).FullName
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

try {
    # Both the executable and the sources in the ZIP come from this one Git tree.
    $commit = & git -C $repo rev-parse --verify --end-of-options ($Ref + '^{commit}')
    if ($LASTEXITCODE -ne 0) { throw 'Could not resolve the requested Git revision.' }
    & git -C $repo archive --format=zip --prefix=SkinLiveOBS/ ('--output=' + $pending) $commit
    if ($LASTEXITCODE -ne 0) { throw 'Could not archive the selected commit.' }

    [IO.Compression.ZipFile]::ExtractToDirectory($pending, $work)
    $tree = Join-Path $work 'SkinLiveOBS'
    & (Join-Path $tree 'source/build.cmd')
    if ($LASTEXITCODE -ne 0) { throw 'C# compilation failed.' }
    $exe = Join-Path $tree 'SkinLiveOBS.exe'
    $info = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
    if ($info.FileVersion -ne ($Version + '.0')) { throw 'The package version differs from the application version.' }
    $zip = [IO.Compression.ZipFile]::Open($pending, [IO.Compression.ZipArchiveMode]::Update)
    try {
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $exe, 'SkinLiveOBS/SkinLiveOBS.exe')
    } finally { $zip.Dispose() }
    if ([IO.File]::Exists($destination)) {
        [IO.File]::Replace($pending, $destination, [System.Management.Automation.Language.NullString]::Value)
    } else {
        [IO.File]::Move($pending, $destination)
    }
    Get-Item -LiteralPath $destination
} finally {
    if ([IO.File]::Exists($pending)) { [IO.File]::Delete($pending) }
    $verified = (Get-Item -LiteralPath $work).FullName
    if ($verified -eq [IO.Path]::GetFullPath((Join-Path $tempRoot ('SkinLiveOBS-release-' + $id)))) {
        [IO.Directory]::Delete($verified, $true)
    }
}
