# Builds the mod and the test harness, runs Valheim once with the harness active, and collects
# screenshots plus the BepInEx log under tests/out/<timestamp>/. The game window is kept off-screen
# and the harness mutes all audio, so nothing from the run is shown or heard.
param(
    [string]$ValheimDir = "E:\SteamLibrary\steamapps\common\Valheim",
    [int]$TimeoutSec = 420
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$plugins = Join-Path $ValheimDir "BepInEx\plugins"
$harnessDll = Join-Path $plugins "SeekerBeGone.TestHarness.dll"
$outDir = Join-Path $PSScriptRoot ("out\" + (Get-Date -Format "yyyyMMdd-HHmmss"))

if (Get-Process valheim -ErrorAction SilentlyContinue) {
    throw "Valheim is already running"
}

dotnet build (Join-Path $PSScriptRoot "Harness\SeekerBeGone.TestHarness.csproj") -c Release "-p:ValheimDir=$ValheimDir"
if ($LASTEXITCODE -ne 0) {
    throw "build failed"
}

Copy-Item (Join-Path $root "bin\Release\net472\SeekerBeGone.dll") $plugins -Force
Copy-Item (Join-Path $PSScriptRoot "Harness\bin\Release\net472\SeekerBeGone.TestHarness.dll") $harnessDll -Force
New-Item -ItemType Directory -Force $outDir | Out-Null

Add-Type -Namespace Win32 -Name Native -MemberDefinition @"
[DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
public struct RECT { public int Left, Top, Right, Bottom; }
"@
Add-Type -AssemblyName System.Windows.Forms
$screen = [System.Windows.Forms.SystemInformation]::VirtualScreen
$offX = $screen.Right + 200
$offY = $screen.Bottom + 200
$SWP_NOSIZE = 0x0001; $SWP_NOZORDER = 0x0004; $SWP_NOACTIVATE = 0x0010

try {
    $gameArgs = @("-console", "-screen-fullscreen", "0", "-screen-width", "1280", "-screen-height", "720", "-sbgtest", "`"$outDir`"")
    $proc = Start-Process (Join-Path $ValheimDir "valheim.exe") -WorkingDirectory $ValheimDir -ArgumentList $gameArgs -PassThru
    Write-Host "valheim started (pid $($proc.Id)), waiting up to $TimeoutSec s"
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $confirmed = $false
    while (-not $proc.HasExited -and (Get-Date) -lt $deadline) {
        $proc.Refresh()
        $h = $proc.MainWindowHandle
        if ($h -ne [IntPtr]::Zero) {
            [Win32.Native]::SetWindowPos($h, [IntPtr]::Zero, $offX, $offY, 0, 0, $SWP_NOSIZE -bor $SWP_NOZORDER -bor $SWP_NOACTIVATE) | Out-Null
            $rect = New-Object Win32.Native+RECT
            if (-not $confirmed -and [Win32.Native]::GetWindowRect($h, [ref]$rect)) {
                $confirmed = $rect.Left -ge $screen.Right
                Write-Host ("window rect: {0},{1}-{2},{3} off-screen={4}" -f $rect.Left, $rect.Top, $rect.Right, $rect.Bottom, $confirmed)
            }
        }
        Start-Sleep -Milliseconds 500
    }
    if (-not $proc.HasExited) {
        Write-Warning "timeout, killing valheim"
        Stop-Process -Id $proc.Id -Force
    }
}
finally {
    Remove-Item $harnessDll -Force -ErrorAction SilentlyContinue
    Copy-Item (Join-Path $ValheimDir "BepInEx\LogOutput.log") (Join-Path $outDir "LogOutput.log") -ErrorAction SilentlyContinue
}

Write-Host "output: $outDir"
Get-ChildItem $outDir
