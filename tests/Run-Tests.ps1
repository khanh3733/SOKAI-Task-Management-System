$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$applicationBin = Join-Path $repository 'EmployeeTaskManagement/bin'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$visualStudio = & $vswhere -latest -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $visualStudio 'MSBuild/Current/Bin/Roslyn/csc.exe'
$testOutput = Join-Path ([System.IO.Path]::GetTempPath()) ('EmployeeTaskManagement-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testOutput | Out-Null
Get-ChildItem -LiteralPath $applicationBin -Filter '*.dll' | Copy-Item -Destination $testOutput
$executable = Join-Path $testOutput 'EmployeeEligibilityTests.exe'
$references = @(
    'EmployeeTaskManagement.dll',
    'EntityFramework.dll',
    'Microsoft.AspNet.Identity.Core.dll',
    'Microsoft.AspNet.Identity.EntityFramework.dll',
    'System.Web.Mvc.dll'
) | ForEach-Object { '/reference:' + (Join-Path $applicationBin $_) }
& $compiler /nologo /target:exe "/out:$executable" /reference:System.Web.dll /reference:System.ComponentModel.DataAnnotations.dll $references (Join-Path $PSScriptRoot 'EmployeeEligibilityTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& $executable
if ($LASTEXITCODE -ne 0) { throw 'Regression checks failed.' }
