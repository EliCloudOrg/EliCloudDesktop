# Capture the EliCloudDesktop main window (found by title) for Figma comparison.
# ASCII-only on purpose: Windows PowerShell reads BOM-less .ps1 as ANSI and would
# mangle non-ASCII text.
param(
    [string]$TitleMatch = 'EliCloud',
    [string]$Out = 'D:\work\projects\EliCloud\EliCloudDesktop\docs\screenshots\capture.png'
)

Add-Type -AssemblyName System.Drawing

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class Win {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@

$proc = Get-Process | Where-Object { $_.MainWindowTitle -and $_.MainWindowTitle -like "*$TitleMatch*" } | Select-Object -First 1
if (-not $proc) { Write-Error "no window matching '$TitleMatch'"; exit 1 }

$handle = $proc.MainWindowHandle
[void][Win]::ShowWindow($handle, 9)      # SW_RESTORE
[void][Win]::SetForegroundWindow($handle)
Start-Sleep -Milliseconds 900

$rect = New-Object Win+RECT
[void][Win]::GetWindowRect($handle, [ref]$rect)
$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
if ($width -le 0 -or $height -le 0) { Write-Error "bad window size: $width x $height"; exit 1 }

$bmp = New-Object System.Drawing.Bitmap $width, $height
$gfx = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $gfx.GetHdc()
[void][Win]::PrintWindow($handle, $hdc, 2)   # PW_RENDERFULLCONTENT
$gfx.ReleaseHdc($hdc)
$gfx.Dispose()
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

Write-Output ("saved {0} ({1}x{2}) window='{3}'" -f $Out, $width, $height, $proc.MainWindowTitle)
