Add-Type -AssemblyName System.Drawing

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class IconNativeMethods
{
    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr handle);
}
'@

$assetDirectory = Join-Path $PSScriptRoot '..\Assets'
$assetDirectory = [System.IO.Path]::GetFullPath($assetDirectory)
[System.IO.Directory]::CreateDirectory($assetDirectory) | Out-Null
$iconPath = Join-Path $assetDirectory 'avocado.ico'

$bitmap = New-Object System.Drawing.Bitmap 32,32
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.Clear([System.Drawing.Color]::Transparent)

$skin = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,23,61,43))
$flesh = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,155,198,62))
$seed = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,113,63,37))

$graphics.FillRectangle($skin,13,1,6,5)
$graphics.FillRectangle($skin,9,5,14,4)
$graphics.FillRectangle($skin,6,9,20,6)
$graphics.FillRectangle($skin,3,15,26,10)
$graphics.FillRectangle($skin,6,25,20,4)
$graphics.FillRectangle($skin,10,29,12,2)
$graphics.FillRectangle($flesh,10,8,12,4)
$graphics.FillRectangle($flesh,7,12,18,11)
$graphics.FillRectangle($flesh,10,23,12,4)
$graphics.FillRectangle($seed,12,17,9,9)

$handle = $bitmap.GetHicon()
$temporaryIcon = [System.Drawing.Icon]::FromHandle($handle)
$file = [System.IO.File]::Create($iconPath)
$temporaryIcon.Save($file)
$file.Dispose()
[IconNativeMethods]::DestroyIcon($handle) | Out-Null
$seed.Dispose()
$flesh.Dispose()
$skin.Dispose()
$graphics.Dispose()
$bitmap.Dispose()

Write-Output $iconPath
