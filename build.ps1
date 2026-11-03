# Builds MouseTravel.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$src = Join-Path $root 'src\MouseTravel.cs'
$out = Join-Path $root 'MouseTravel.exe'
$ico = Join-Path $root 'src\MouseTravel.ico'
$refs = '/r:System.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Management.dll'

# First pass builds the app, which then draws its own icon; second pass bakes the icon into the .exe.
& $csc /nologo /target:winexe /optimize+ $refs "/out:$out" $src
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Start-Process -FilePath $out -ArgumentList '--make-icon', "`"$ico`"" -Wait
& $csc /nologo /target:winexe /optimize+ $refs "/win32icon:$ico" "/out:$out" $src
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Host "Built $out"
