# Drive the EliCloudDesktop login form through UI Automation (a real end-to-end check
# against a running server), then screenshot the result.
#
# ASCII-only: Windows PowerShell reads BOM-less .ps1 as ANSI and mangles non-ASCII.
param(
    [string]$Username = 'localdev',
    [string]$Password = 'LocalDev#2026',
    [string]$SignInButtonName = [char]0x767B + [char]0x5F55,   # 登录
    [string]$Out = 'D:\work\projects\EliCloud\_recon2\after-login.png'
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class Win2 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@

function Get-AppWindow {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, 'EliCloud Desktop')
    $win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
    if (-not $win) { throw 'EliCloud Desktop window not found (is the app running?)' }
    return $win
}

$proc = Get-Process EliCloudDesktop -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { throw 'EliCloudDesktop process not found' }
[void][Win2]::ShowWindow($proc.MainWindowHandle, 9)
[void][Win2]::SetForegroundWindow($proc.MainWindowHandle)
Start-Sleep -Milliseconds 700

$win = Get-AppWindow
$editType = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::Edit)
$edits = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $editType)
Write-Output "found $($edits.Count) edit fields"
if ($edits.Count -lt 2) { throw 'expected at least 2 input fields on the login page' }

$valuePattern = [System.Windows.Automation.ValuePattern]::Pattern
$userField = $edits.Item(0).GetCurrentPattern($valuePattern)
$userField.SetValue($Username)
Start-Sleep -Milliseconds 200
$passField = $edits.Item(1).GetCurrentPattern($valuePattern)
$passField.SetValue($Password)
Start-Sleep -Milliseconds 300
Write-Output "filled credentials for '$Username'"

$buttonType = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::Button)
$buttons = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $buttonType)
$target = $null
foreach ($b in $buttons) {
    if ($b.Current.Name -eq $SignInButtonName) { $target = $b; break }
}
if (-not $target) {
    $names = ($buttons | ForEach-Object { $_.Current.Name }) -join ' | '
    throw "sign-in button '$SignInButtonName' not found. buttons: $names"
}

$target.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Write-Output 'clicked sign-in, waiting for the main shell...'
Start-Sleep -Seconds 6

# screenshot
$dir = Split-Path -Parent $Out
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$handle = (Get-Process EliCloudDesktop | Select-Object -First 1).MainWindowHandle
$rect = New-Object Win2+RECT
[void][Win2]::GetWindowRect($handle, [ref]$rect)
$w = $rect.Right - $rect.Left; $h = $rect.Bottom - $rect.Top
$bmp = New-Object System.Drawing.Bitmap $w, $h
$gfx = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $gfx.GetHdc()
[void][Win2]::PrintWindow($handle, $hdc, 2)
$gfx.ReleaseHdc($hdc); $gfx.Dispose()
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output "saved $Out ($w x $h)"
