<#
.SYNOPSIS
    Builds FolderHub.exe, and optionally the installer.

.EXAMPLE
    .\publish.ps1
    Single small executable (~700 KB). Needs the .NET 10 Desktop Runtime.

.EXAMPLE
    .\publish.ps1 -SelfContained
    Single executable that runs on any Windows 11, nothing to install first.

.EXAMPLE
    .\publish.ps1 -Installer
    Self-contained build plus FolderHub-Setup-<version>.exe (needs Inno Setup 6).
#>
param(
    [switch]$SelfContained,
    [switch]$Installer,
    [string]$Version = '1.0.0',
    [string]$OutDir = "$PSScriptRoot\dist"
)

$ErrorActionPreference = 'Stop'

# O instalador é distribuído para quem talvez não tenha o runtime .NET, então
# ele sempre carrega a build autocontida.
if ($Installer) { $SelfContained = $true }

$publishArgs = @(
    'publish'
    "$PSScriptRoot\src\FolderHub\FolderHub.csproj"
    '-c', 'Release'
    '-r', 'win-x64'
    '-o', $OutDir
    "-p:Version=$Version"
    '-p:PublishSingleFile=true'
    '-p:IncludeNativeLibrariesForSelfExtract=true'
    '-p:DebugType=none'
    '--nologo'
)

# Precisa ser propriedade MSBuild, não a flag --self-contained: projetos WPF
# fazem um build interno (*_wpftmp.csproj) que não recebe a flag da linha de comando.
$publishArgs += if ($SelfContained) { '-p:SelfContained=true' } else { '-p:SelfContained=false' }

dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw 'publish failed' }

$exe = Join-Path $OutDir 'FolderHub.exe'
Write-Host ''
Write-Host ("  {0}  ({1:N1} MB)" -f $exe, ((Get-Item $exe).Length / 1MB)) -ForegroundColor Green

if (-not $Installer) {
    Write-Host ''
    Write-Host '  Tip: .\publish.ps1 -Installer builds the setup, shortcuts included.' -ForegroundColor DarkGray
    Write-Host ''
    return
}

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    throw 'Inno Setup 6 not found. Install it (winget install JRSoftware.InnoSetup) and run again.'
}

& $iscc "/DAppVersion=$Version" "$PSScriptRoot\installer\FolderHub.iss" | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'installer build failed' }

$setup = Join-Path $OutDir "FolderHub-Setup-$Version.exe"
Write-Host ("  {0}  ({1:N1} MB)" -f $setup, ((Get-Item $setup).Length / 1MB)) -ForegroundColor Green
Write-Host ''
