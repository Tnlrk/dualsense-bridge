# Sestaví přenosný DualShift.exe (jeden soubor, .NET runtime a instalátory ovladačů uvnitř).
# Použití:  ./build.ps1
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

dotnet test --nologo
if ($LASTEXITCODE -ne 0) { throw 'Testy neprošly.' }

dotnet publish src/DualShift/DualShift.csproj -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=none -o dist --nologo
if ($LASTEXITCODE -ne 0) { throw 'Publish selhal.' }

$exe = Get-Item dist/DualShift.exe
Write-Host ("Hotovo: {0} ({1:N1} MB)" -f $exe.FullName, ($exe.Length / 1MB))
