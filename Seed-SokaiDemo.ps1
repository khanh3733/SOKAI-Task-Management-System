[CmdletBinding(DefaultParameterSetName = 'Prompt')]
param(
    [Parameter(ParameterSetName = 'Prompt')]
    [Security.SecureString]$Password,

    [Parameter(Mandatory, ParameterSetName = 'Generate')]
    [switch]$GeneratePassword
)

$ErrorActionPreference = 'Stop'
$solutionRoot = $PSScriptRoot
$webRoot = Join-Path $solutionRoot 'EmployeeTaskManagement'
$source = Join-Path $solutionRoot 'tools\SokaiDemoSeeder.cs'
$applicationBin = Join-Path $webRoot 'bin'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$visualStudio = & $vswhere -latest -requires Microsoft.Component.MSBuild -property installationPath
if (-not $visualStudio) { throw 'Visual Studio with MSBuild/Roslyn is required.' }
$compiler = Join-Path $visualStudio 'MSBuild\Current\Bin\Roslyn\csc.exe'
$workingDirectory = Join-Path ([IO.Path]::GetTempPath()) ('SokaiDemoSeed-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $workingDirectory | Out-Null

$plainPassword = $null
if ($GeneratePassword) {
    $plainPassword = 'Sk!' + [Guid]::NewGuid().ToString('N').Substring(0, 14) + '9aA'
}
else {
    if (-not $Password) { $Password = Read-Host 'Enter the shared local-only password for SOKAI demo accounts' -AsSecureString }
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)
    try { $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}

try {
    Get-ChildItem -LiteralPath $applicationBin -Filter '*.dll' | Copy-Item -Destination $workingDirectory
    $executable = Join-Path $workingDirectory 'SokaiDemoSeeder.exe'
    $references = @(
        'EmployeeTaskManagement.dll', 'EntityFramework.dll', 'EntityFramework.SqlServer.dll',
        'Microsoft.AspNet.Identity.Core.dll', 'Microsoft.AspNet.Identity.EntityFramework.dll',
        'Microsoft.AspNet.Identity.Owin.dll', 'Microsoft.Owin.dll', 'BCrypt.Net-Next.dll'
    ) | ForEach-Object { '/reference:' + (Join-Path $workingDirectory $_) }

    & $compiler /nologo /target:exe "/out:$executable" /reference:System.Web.dll /reference:System.Data.dll /reference:System.ComponentModel.DataAnnotations.dll $references $source
    if ($LASTEXITCODE -ne 0) { throw 'SOKAI demo seeder compilation failed.' }

    $configuration = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <configSections>
    <section name="entityFramework" type="System.Data.Entity.Internal.ConfigFile.EntityFrameworkSection, EntityFramework, Version=6.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" requirePermission="false" />
  </configSections>
  <connectionStrings>
    <add name="DefaultConnection" connectionString="Data Source=.\SQLEXPRESS;Initial Catalog=EmployeeTaskManagementDB;Integrated Security=True;MultipleActiveResultSets=True" providerName="System.Data.SqlClient" />
  </connectionStrings>
  <startup useLegacyV2RuntimeActivationPolicy="true"><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.7.2" /></startup>
  <entityFramework>
    <defaultConnectionFactory type="System.Data.Entity.Infrastructure.SqlConnectionFactory, EntityFramework" />
    <providers><provider invariantName="System.Data.SqlClient" type="System.Data.Entity.SqlServer.SqlProviderServices, EntityFramework.SqlServer" /></providers>
  </entityFramework>
</configuration>
"@
    Set-Content -LiteralPath ($executable + '.config') -Value $configuration -Encoding UTF8
    $env:SOKAI_DEMO_PASSWORD = $plainPassword
    $seedOutput = @(& $executable $webRoot)
    if ($LASTEXITCODE -ne 0) { throw 'SOKAI demo seeder execution failed.' }
    $seedOutput | ForEach-Object { Write-Output $_ }
    if ($GeneratePassword -and ($seedOutput -match 'created successfully')) {
        Write-Output ('DEMO_PASSWORD=' + $plainPassword)
    }
}
finally {
    Remove-Item Env:SOKAI_DEMO_PASSWORD -ErrorAction SilentlyContinue
    $plainPassword = $null
    if (Test-Path -LiteralPath $workingDirectory) {
        $resolved = (Resolve-Path -LiteralPath $workingDirectory).Path
        $expectedRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
        if (-not $resolved.StartsWith($expectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Unexpected temporary directory: $resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
