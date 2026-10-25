# Manage accounts on the LOCAL development server (the one Debug-Local points at).
#
# Compatibility wrapper: the local stack lives in local-dev\. The real
# implementation is `python local-dev\dev.py account ...`, which selects the
# same SSO database and interpreter that the running local stack uses.
#
# Usage (unchanged):
#   tools\local-account.ps1 -List
#   tools\local-account.ps1 -Create  -Username alice -Password 'Abc12345' -Email alice@example.com
#   tools\local-account.ps1 -Create  -Username alice -Password 'Abc12345' -Email alice@example.com -EmailVerified
#   tools\local-account.ps1 -VerifyEmail -Username alice
#   tools\local-account.ps1 -ResetPassword -Username alice -Password 'NewPass#2026'
#   tools\local-account.ps1 -SetStatus -Username alice -Status disabled
#   tools\local-account.ps1 -List -DataDir local-dev\.data-b   # isolated stack
#
# Rules enforced by the service:
#   username  3-32 chars, must start with a letter or digit, then [A-Za-z0-9_.-]
#   password  8-64 chars, must contain both a letter and a digit, not a common password
#
# ASCII-only: Windows PowerShell reads BOM-less .ps1 as ANSI and would mangle non-ASCII.
param(
    [switch]$List,
    [switch]$Create,
    [switch]$VerifyEmail,
    [switch]$ResetPassword,
    [switch]$SetStatus,
    [switch]$EmailVerified,
    [string]$Username,
    [string]$Password,
    [string]$Email,
    [ValidateSet('active', 'disabled')][string]$Status,
    [string]$SsoSource = $env:ELICLOUD_SSO_SRC,
    [int]$Port = 8080,
    [string]$DataDir = ''
)

$ErrorActionPreference = 'Stop'

$desktopRoot = Split-Path -Parent $PSScriptRoot
$workspace = Split-Path -Parent $desktopRoot
$devPy = Join-Path $desktopRoot 'local-dev\dev.py'

if (-not (Test-Path $devPy)) {
    throw "New local-dev harness not found: $devPy"
}
if ($SsoSource) {
    Write-Warning "Ignoring -SsoSource: the new local-dev harness uses the sibling sso source."
}
if ($Port -ne 8080) {
    Write-Warning "Ignoring -Port: account commands only read/write the local SQLite database."
}

$python = $env:ELICLOUD_PYTHON
if (-not $python) {
    $cmd = Get-Command python -ErrorAction SilentlyContinue
    if ($cmd) { $python = $cmd.Source }
}
if (-not $python) {
    throw 'Python not found on PATH. Install Python 3.11+ or set ELICLOUD_PYTHON.'
}

if ($DataDir) {
    if (-not [System.IO.Path]::IsPathRooted($DataDir)) {
        $DataDir = Join-Path $desktopRoot $DataDir
    }
}

$arguments = New-Object System.Collections.Generic.List[string]
$arguments.Add($devPy)
$arguments.Add('account')
if ($DataDir) { $arguments.Add('--data-dir'); $arguments.Add($DataDir) }

if ($List) {
    $arguments.Add('list')
} elseif ($Create) {
    if (-not $Username) { throw '-Create needs -Username' }
    $arguments.Add('create')
    $arguments.Add('--username'); $arguments.Add($Username)
    if ($Password) { $arguments.Add('--password-stdin') }
    if ($Email) { $arguments.Add('--email'); $arguments.Add($Email) }
    if ($EmailVerified) { $arguments.Add('--verified') }
} elseif ($VerifyEmail) {
    if (-not $Username) { throw '-VerifyEmail needs -Username' }
    $arguments.Add('verify')
    $arguments.Add('--username'); $arguments.Add($Username)
} elseif ($ResetPassword) {
    if (-not $Username) { throw '-ResetPassword needs -Username' }
    $arguments.Add('reset-password')
    $arguments.Add('--username'); $arguments.Add($Username)
    if ($Password) { $arguments.Add('--password-stdin') }
} elseif ($SetStatus) {
    if (-not $Username -or -not $Status) { throw '-SetStatus needs -Username and -Status' }
    $arguments.Add('status')
    $arguments.Add('--username'); $arguments.Add($Username)
    $arguments.Add('--status'); $arguments.Add($Status)
} else {
    Write-Output 'Nothing to do. Use -List / -Create / -VerifyEmail / -ResetPassword / -SetStatus (see the header of this file).'
    exit 2
}

$argumentArray = $arguments.ToArray()
if ($Password) {
    # Feed the password through stdin rather than the command line, so it does not
    # show up in the process list.
    $Password | & $python @argumentArray
} else {
    & $python @argumentArray
}
exit $LASTEXITCODE
