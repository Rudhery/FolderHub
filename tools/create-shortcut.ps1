<#
.SYNOPSIS
    Creates an extra FolderHub shortcut, for one specific folder.

.DESCRIPTION
    The installer already creates the main Start Menu shortcut. This is for the
    extra hubs: one .lnk per folder, each pinnable to the taskbar on its own.

    A .lnk rather than the .exe because Windows 11 pins a bare .exe unreliably,
    and a pinned .exe always starts with no arguments - which is exactly the
    folder argument you need here.

.EXAMPLE
    .\create-shortcut.ps1
    "FolderHub" in the Start Menu, opening the folder saved in the config.

.EXAMPLE
    .\create-shortcut.ps1 -Folder "D:\Games" -Name "Games Hub" -Desktop
    A hub for one specific folder, on the Desktop too. Repeat for as many
    hubs as you like.

.EXAMPLE
    .\create-shortcut.ps1 -Background
    Starts resident: hidden in the tray, listening for the global hotkey.
#>
param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\dist\FolderHub.exe'),
    [string]$Folder,
    [string]$Name = 'FolderHub',
    [switch]$Background,
    [switch]$Desktop
)

$ErrorActionPreference = 'Stop'

$Exe = [System.IO.Path]::GetFullPath($Exe)
if (-not (Test-Path $Exe)) {
    throw "FolderHub.exe not found at '$Exe'. Run .\publish.ps1 first, or pass -Exe."
}

$arguments = @()
if ($Background) { $arguments += '--background' }
if ($Folder) {
    $Folder = [System.IO.Path]::GetFullPath($Folder)
    if (-not (Test-Path $Folder -PathType Container)) { throw "Folder not found: '$Folder'" }
    $arguments += "`"$Folder`""
}

$targets = @(Join-Path ([Environment]::GetFolderPath('Programs')) "$Name.lnk")
if ($Desktop) { $targets += Join-Path ([Environment]::GetFolderPath('Desktop')) "$Name.lnk" }

$shell = New-Object -ComObject WScript.Shell
foreach ($path in $targets) {
    $link = $shell.CreateShortcut($path)
    $link.TargetPath = $Exe
    $link.Arguments = ($arguments -join ' ')
    $link.WorkingDirectory = Split-Path $Exe -Parent
    $link.IconLocation = "$Exe,0"
    $link.Description = if ($Folder) { "FolderHub - $Folder" } else { 'FolderHub' }
    $link.Save()
    Write-Host "  created  $path" -ForegroundColor Green
}

Write-Host ""
Write-Host "  To pin it to the taskbar:" -ForegroundColor Cyan
Write-Host "    open the Start Menu, find '$Name', right-click it and choose"
Write-Host "    'Pin to taskbar' (it may sit under 'More')."
Write-Host "    Dragging the .lnk onto the taskbar works too."
Write-Host ""
