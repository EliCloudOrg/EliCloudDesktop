# Drive the EliCloudDesktop registration flow end-to-end:
#   create account -> verify page -> read the confirmation link from .local\email.log
#   -> open the link (simulates the user's click) -> wait for the main shell.
#
# Prerequisites:
#   local-dev\dev.ps1 up -Background
#   dotnet run --project src\EliCloudDesktop\EliCloudDesktop.csproj -c Debug-Local -p:Platform=x64
#
# ASCII-only: Windows PowerShell reads BOM-less .ps1 as ANSI and mangles non-ASCII.
param(
    [string]$Username = '',
    [string]$Email = '',
    [string]$Password = 'S3cret!pass1',
    [string]$Out = 'docs\screenshots\06-register-verified.png',
    [string]$EmailLog = ''
)

$ErrorActionPreference = 'Stop'

if (-not $Username) { $Username = 'smoke_' + [guid]::NewGuid().ToString('N').Substring(0, 8) }
if (-not $Email) { $Email = "$Username@example.com" }

$createAccountName = -join [char[]]@(0x521B, 0x5EFA, 0x8D26, 0x53F7)   # 创建账号
$registerName = -join [char[]]@(0x6CE8, 0x518C)                       # 注册
$backToSignInName = -join [char[]]@(0x8FD4, 0x56DE, 0x767B, 0x5F55)   # 返回登录

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

function Find-Button {
    param($Window, [string]$Name)
    $buttonType = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Button)
    $buttons = $Window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $buttonType)
    foreach ($b in $buttons) {
        if ($b.Current.Name -eq $Name) { return $b }
    }
    $names = ($buttons | ForEach-Object { $_.Current.Name }) -join ' | '
    throw "button '$Name' not found. buttons: $names"
}

function Find-Edits {
    param($Window)
    $editType = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Edit)
    return $Window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $editType)
}

function Set-EditValue {
    param($Edit, [string]$Value)
    $pattern = $Edit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $pattern.SetValue($Value)
}

function Invoke-Button {
    param($Button)
    $Button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}

function Save-Screenshot {
    param([string]$Path)
    $proc = Get-Process EliCloudDesktop -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $proc) { throw 'EliCloudDesktop process not found' }
    [void][Win2]::ShowWindow($proc.MainWindowHandle, 9)
    [void][Win2]::SetForegroundWindow($proc.MainWindowHandle)
    Start-Sleep -Milliseconds 500

    $rect = New-Object Win2+RECT
    [void][Win2]::GetWindowRect($proc.MainWindowHandle, [ref]$rect)
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) { throw 'invalid window rect' }

    $bmp = New-Object System.Drawing.Bitmap($width, $height)
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $gfx.GetHdc()
    [void][Win2]::PrintWindow($proc.MainWindowHandle, $hdc, 2)
    $gfx.ReleaseHdc($hdc)
    $gfx.Dispose()

    $dir = Split-Path -Parent $Path
    if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "screenshot: $Path"
}

$proc = Get-Process EliCloudDesktop -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { throw 'EliCloudDesktop process not found (start the Debug-Local build first)' }
[void][Win2]::ShowWindow($proc.MainWindowHandle, 9)
[void][Win2]::SetForegroundWindow($proc.MainWindowHandle)
Start-Sleep -Milliseconds 700

$win = Get-AppWindow

# 1) Switch the login card into register mode.
Invoke-Button (Find-Button $win $createAccountName)
Start-Sleep -Milliseconds 700

# 2) Fill username / email / password.
$edits = Find-Edits $win
Write-Output "found $($edits.Count) edit fields on register page"
if ($edits.Count -lt 3) { throw 'expected at least 3 input fields on the register page' }
Set-EditValue $edits.Item(0) $Username
Start-Sleep -Milliseconds 150
Set-EditValue $edits.Item(1) $Email
Start-Sleep -Milliseconds 150
Set-EditValue $edits.Item(2) $Password
Start-Sleep -Milliseconds 250

Invoke-Button (Find-Button $win $registerName)
Write-Output "submitted registration for '$Username'"

# 3) Wait for the verify page (it always has a 返回登录 button).
$deadline = (Get-Date).AddSeconds(15)
$verifyButton = $null
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 500
    try {
        $verifyButton = Find-Button $win $backToSignInName
        break
    } catch { }
}
if (-not $verifyButton) { throw 'verification page did not appear' }
Write-Output 'verification page appeared'

# 4) Read the confirmation link from the local email log and open it.
$desktopRoot = Split-Path -Parent $PSScriptRoot
$workspace = Split-Path -Parent $desktopRoot
$candidates = @()
if ($EmailLog) { $candidates += $EmailLog }
$candidates += (Join-Path $desktopRoot 'local-dev\.data\logs\email.log')
$candidates += (Join-Path $desktopRoot '.local\email.log')
$logPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $logPath) {
    throw "email log not found. Tried: $($candidates -join ' ; ')"
}

$link = $null
$deadline = (Get-Date).AddSeconds(20)
while ((Get-Date) -lt $deadline -and -not $link) {
    $text = Get-Content $logPath -Raw -ErrorAction SilentlyContinue
    if ($text) {
        # New gateway: http://127.0.0.1:8080/email/v1/confirm?token=...
        # Legacy:      http://127.0.0.1:8011/v1/confirm?token=...
        $matches = [regex]::Matches($text, 'https?://[^\s"'']+/(?:email/)?v1/confirm\?token=[A-Za-z0-9_\-]+')
        if ($matches.Count -gt 0) { $link = $matches[$matches.Count - 1].Value }
    }
    if (-not $link) { Start-Sleep -Milliseconds 800 }
}
if (-not $link) { throw "confirmation link not found in $logPath" }
Write-Output "confirm link: $link"
$response = Invoke-WebRequest -UseBasicParsing -Uri $link
Write-Output "confirm response: $($response.Content)"

# 5) Wait for the client to poll, auto-login and show the main shell.
Start-Sleep -Seconds 12
Save-Screenshot $Out

$profile = $Username
Write-Output "registration smoke completed for $profile"
