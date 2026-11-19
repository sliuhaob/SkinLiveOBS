param(
    [Parameter(Mandatory=$true)][string]$SkinPng,
    [Parameter(Mandatory=$true)][string]$Psd2LiveCheckout,
    [Parameter(Mandatory=$true)][string]$KotlinHome,
    [Parameter(Mandatory=$true)][string]$Output
)
$ErrorActionPreference='Stop'
$work=[IO.Directory]::CreateDirectory((Join-Path ([IO.Path]::GetTempPath()) ('skinlive-art-'+[guid]::NewGuid().ToString('N')))).FullName
$source=Split-Path -Parent $PSScriptRoot
$drawing=@('SkinMap.cs','Mesh.cs','Raster.cs','author/BuildArt.cs') | ForEach-Object {(Get-Item -LiteralPath (Join-Path $source $_)).FullName}
Add-Type -Path $drawing -ReferencedAssemblies 'System.dll','System.Drawing.dll','System.Core.dll'
[SkinLive.BuildArt]::Write((Get-Item -LiteralPath $SkinPng).FullName,$work)
$k=(Get-Item -LiteralPath $KotlinHome).FullName
$core=Join-Path (Get-Item -LiteralPath $Psd2LiveCheckout).FullName 'umamo/src/main/kotlin/org/umamo/format'
$code=@((Join-Path $core 'FormatVersion.kt'),(Join-Path $core 'moc3/MocDocument.kt'),(Join-Path $PSScriptRoot 'BakeRig.kt'))
foreach($dir in 'model','moc','io','encode','decode') {
    $code += @(Get-ChildItem -LiteralPath (Join-Path $core ('moc3/'+$dir)) -Filter '*.kt' -File | ForEach-Object FullName)
}
$argFile=Join-Path $work 'kotlin.args'
[IO.File]::WriteAllLines($argFile,[string[]]@($code | ForEach-Object {'"'+($_ -replace '\\','/')+'"'}),[Text.UTF8Encoding]::new($false))
$compiler=Join-Path $work 'model-compiler.jar'
& java -cp ($k+'/lib/*') org.jetbrains.kotlin.cli.jvm.K2JVMCompiler -kotlin-home $k ('@'+$argFile) -d $compiler
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
& java -cp ($compiler+';'+$k+'/lib/kotlin-stdlib.jar') BakeRigKt (Join-Path $work 'avatar.rig') (Join-Path $work 'avatar.moc3')
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
$outDir=[IO.Directory]::CreateDirectory($Output).FullName
foreach($file in 'avatar.moc3','avatar.rig','texture_00.png','preview.png'){Copy-Item -LiteralPath (Join-Path $work $file) -Destination (Join-Path $outDir $file)}
