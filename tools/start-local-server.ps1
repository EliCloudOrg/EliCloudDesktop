# Compatibility wrapper: the local development stack moved to the repository root.
#
# The real implementation is now:
#   local-dev\dev.ps1   (or python local-dev\dev.py)
#
# It starts the same SSO + email, and additionally file + userinfo + mc + gateway.
# This wrapper keeps the old command line working for existing docs and scripts:
#
#   tools\start-local-server.ps1                 # foreground; Ctrl+C stops all
#   tools\start-local-server.ps1 -Background     # detached
#   tools\start-local-server.ps1 -Stop           # stop every local service
#   tools\start-local-server.ps1 -Background -Smtp
#   tools\start-local-server.ps1 -Port 8180 -EmailPort 8111
#
# On the first run it imports the old data from .local\data (accounts + RSA key)
# into local-dev\.data, so existing Debug-Local accounts keep working.
# Set ELICLOUD_WORKSPACE if the five module checkouts are not siblings of
# EliCloudDesktop.
#
# ASCII-only on purpose: Windows PowerShell reads BOM-less .ps1 as ANSI and
# would mangle any non-ASCII text in here.
param(
    [string]$SsoSource = $env:ELICLOUD_SSO_SRC,
    [string]$EmailSource = $env:ELICLOUD_EMAIL_SRC,
    [int]$Port = 8080,
    [int]$EmailPort = 8011,
    [int]$PortOffset = 0,
    [string]$DataDir = '',
    [switch]$Background,
    [switch]$Stop,
    [switch]$Reinstall,
    [switch]$Smtp,
    [string]$SmtpHost = '127.0.0.1',
    [int]$SmtpPort = 2525,
    [string]$MailFrom = 'no-reply@elipese.cloud',
    [switch]$Help
)

$ErrorActionPreference = 'Stop'

function Show-Usage {
    Write-Host @'
Local development server (compatibility wrapper)
  tools\start-local-server.ps1                 foreground; Ctrl+C stops all
  tools\start-local-server.ps1 -Background     start detached
  tools\start-local-server.ps1 -Stop           stop every local service
  tools\start-local-server.ps1 -Smtp           send real mail through SMTP_HOST:SMTP_PORT
  tools\start-local-server.ps1 -Port 8180      change the gateway port
  tools\start-local-server.ps1 -EmailPort 8111 change the email direct port
  tools\start-local-server.ps1 -PortOffset 100 shift every default port
  tools\start-local-server.ps1 -DataDir path   use an isolated data directory
  tools\start-local-server.ps1 -Reinstall      recreate the shared venv

The full stack (sso/email/file/userinfo/mc) is defined in:
  local-dev\README.md
'@
}

if ($Help) { Show-Usage; exit 0 }

$desktopRoot = Split-Path -Parent $PSScriptRoot
$workspace = Split-Path -Parent $desktopRoot
$devPy = Join-Path $desktopRoot 'local-dev\dev.py'
if ($DataDir) {
    if ([System.IO.Path]::IsPathRooted($DataDir)) {
        $newData = $DataDir
    } else {
        $newData = Join-Path $desktopRoot $DataDir
    }
} else {
    $newData = Join-Path $desktopRoot 'local-dev\.data'
}
$oldData = Join-Path $desktopRoot '.local\data'

if (-not (Test-Path $devPy)) {
    throw "New local-dev harness not found: $devPy"
}

$python = $env:ELICLOUD_PYTHON
if (-not $python) {
    $cmd = Get-Command python -ErrorAction SilentlyContinue
    if ($cmd) { $python = $cmd.Source }
}
if (-not $python) {
    throw 'Python not found on PATH. Install Python 3.11+ or set ELICLOUD_PYTHON.'
}

function Stop-LegacyProcesses {
    # Stop servers started by the previous version of this script.
    foreach ($name in @('server', 'email')) {
        $pidFile = Join-Path $desktopRoot ".local\$name.pid"
        if (-not (Test-Path $pidFile)) { continue }
        $processId = 0
        try { $processId = [int]((Get-Content $pidFile -Raw).Trim()) } catch { }
        if ($processId -gt 0) {
            $proc = Get-Process -Id $processId -ErrorAction SilentlyContinue
            if ($proc) {
                Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
                Write-Host "stopped legacy $name (pid $processId)"
            }
        }
        Remove-Item $pidFile -Force -ErrorAction SilentlyContinue
    }
}

function Assert-DefaultSource {
    param([string]$Candidate, [string]$Name)
    if (-not $Candidate) { return }
    $resolved = Resolve-Path -LiteralPath $Candidate -ErrorAction SilentlyContinue
    if (-not $resolved) { throw "$Name source not found: $Candidate" }
    $expected = Join-Path $workspace $Name
    if ($resolved.Path.TrimEnd('\') -ne $expected.TrimEnd('\')) {
        throw "The new local-dev harness uses the sibling '$Name' source only ($expected). " +
              "Move the source there, or keep using the old local-dev/local_server.py setup."
    }
}

function Import-LegacyData {
    # Old layout: .local\data\{sso.db,jwt_private.pem,email.db,keys}
    # New layout: local-dev\.data\{sso\sso.db,sso\jwt_private.pem,email\email.db}
    if (-not (Test-Path $oldData)) { return }
    $newSsoDb = Join-Path $newData 'sso\sso.db'
    if (Test-Path $newSsoDb) { return }

    New-Item -ItemType Directory -Force -Path (Join-Path $newData 'sso\keys') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $newData 'email') | Out-Null

    $pairs = @(
        @('sso.db', 'sso\sso.db'),
        @('sso.db-wal', 'sso\sso.db-wal'),
        @('sso.db-shm', 'sso\sso.db-shm'),
        @('jwt_private.pem', 'sso\jwt_private.pem'),
        @('email.db', 'email\email.db'),
        @('email.db-wal', 'email\email.db-wal'),
        @('email.db-shm', 'email\email.db-shm')
    )
    $copied = 0
    foreach ($pair in $pairs) {
        $source = Join-Path $oldData $pair[0]
        if (-not (Test-Path $source)) { continue }
        Copy-Item -LiteralPath $source -Destination (Join-Path $newData $pair[1]) -Force
        $copied++
    }

    $oldKeys = Join-Path $oldData 'keys'
    if ((Test-Path $oldKeys) -and (Get-ChildItem $oldKeys -Force -ErrorAction SilentlyContinue)) {
        Copy-Item -Path (Join-Path $oldKeys '*') -Destination (Join-Path $newData 'sso\keys') -Recurse -Force
    }

    if ($copied -gt 0) {
        Write-Host "imported legacy local data from .local\data ($copied files)"
        Write-Host "  new data: $newData (accounts and RSA key preserved)"
    }
}

if ($Stop) {
    Stop-LegacyProcesses
    $stopArgs = @($devPy, 'down')
    if ($DataDir) { $stopArgs += @('--data-dir', $newData) }
    & $python @stopArgs
    exit $LASTEXITCODE
}

Assert-DefaultSource $SsoSource 'sso'
Assert-DefaultSource $EmailSource 'email'
Import-LegacyData
Stop-LegacyProcesses

$arguments = New-Object System.Collections.Generic.List[string]
$arguments.Add($devPy)
$arguments.Add('up')
if ($Background) { $arguments.Add('--background') }
if ($Reinstall) { $arguments.Add('--reinstall') }
if ($DataDir) { $arguments.Add('--data-dir'); $arguments.Add($newData) }
if ($PortOffset -ne 0) { $arguments.Add('--port-offset'); $arguments.Add("$PortOffset") }
if ($Port -ne 8080) { $arguments.Add('--gateway-port'); $arguments.Add("$Port") }
if ($EmailPort -ne 8011) { $arguments.Add('--email-port'); $arguments.Add("$EmailPort") }
if ($Smtp) {
    $arguments.Add('--smtp')
    $arguments.Add('--smtp-host'); $arguments.Add($SmtpHost)
    $arguments.Add('--smtp-port'); $arguments.Add("$SmtpPort")
    $arguments.Add('--mail-from'); $arguments.Add($MailFrom)
}

Write-Host 'tools\start-local-server.ps1 -> local-dev\dev.py (same SSO + email, plus file/userinfo/mc)'
$argumentArray = $arguments.ToArray()
& $python @argumentArray
exit $LASTEXITCODE
