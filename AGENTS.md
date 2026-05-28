# AGENTS.md

DesktopFlyouts is a WinUI library for showing lightweight desktop flyouts, menu flyouts, and tray-icon driven UI from desktop apps. It ships two public library flavors:

## Code Structure

```text
.
├───DesktopFlyouts.slnx
├───src
│   ├───DesktopFlyouts.Shared
│   │   └───DesktopFlyouts.Shared.shproj
│   ├───DesktopFlyouts.Wasdk
│   │   └───DesktopFlyouts.Wasdk.csproj
│   └───DesktopFlyouts.Uwp
│       └───DesktopFlyouts.Uwp.csproj
└───samples
    ├───DesktopFlyouts.Wasdk.Sample.App
    │   └───DesktopFlyouts.Wasdk.Sample.App.csproj
    ├───DesktopFlyouts.Uwp.Sample.App
    │   └───DesktopFlyouts.Uwp.Sample.App.csproj
    ├───DesktopFlyouts.Uwp.Sample.Packaging
    │   └───DesktopFlyouts.Uwp.Sample.Packaging.wapproj
    └───DesktopFlyouts.Uwp.Sample.TrayHost
        └───DesktopFlyouts.Uwp.Sample.TrayHost.csproj
```

Most runtime behavior lives in `src/DesktopFlyouts.Shared` behind `#if WASDK` and `#if UWP`. Keep both branches building when changing shared files.

## Formatting And Editing Rules

- This repo uses CRLF. Preserve CRLF for every changed text file.
- `.editorconfig` sets UTF-8, CRLF, final newline, spaces, and 4-space indentation.
- `.gitattributes` enforces `text=auto eol=crlf`.
- Use existing patterns and dependency property generation style. Avoid broad refactors while fixing focused behavior.
- Do not edit generated `bin`, `obj`, or `Generated Files` output.

## Build Commands

Run validation one command at a time and inspect each result before moving on.

1. Build the WinUI 3 sample and library:

```powershell
dotnet msbuild /restore:false samples\DesktopFlyouts.Wasdk.Sample.App\DesktopFlyouts.Wasdk.Sample.App.csproj /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never
```

2. Build the UWP library:

```powershell
dotnet msbuild /restore:false src\DesktopFlyouts.Uwp\DesktopFlyouts.Uwp.csproj /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never
```

3. Check whitespace and line endings:

```powershell
git diff --check
git ls-files --eol
```

`NETSDK1057` preview SDK messages can appear on this machine. Treat them as environment notices unless accompanied by a build failure.

## Packaged App Debugging And Launch

The WASDK sample is a packaged app. Do not validate launch by running the built `.exe` directly with `Start-Process` or `dotnet run`. That path can fail with `REGDB_E_CLASSNOTREG`. Use a package-aware launch path.

After building the WASDK sample, register the generated package manifest and launch through `shell:AppsFolder`.

```powershell
$manifest = Join-Path (Get-Location) 'samples\DesktopFlyouts.Wasdk.Sample.App\bin\x64\Debug\net10.0-windows10.0.26100.0\AppxManifest.xml'
Add-AppxPackage -Register $manifest -DisableDevelopmentMode

$package = Get-AppxPackage -Name 'd6120692-1c26-4251-b686-e2321694e3b0'
Start-Process "shell:AppsFolder\$($package.PackageFamilyName)!App"

Start-Sleep -Seconds 5
Get-Process -Name DesktopFlyouts.Wasdk.Sample.App -ErrorAction SilentlyContinue |
    Select-Object Id, ProcessName, MainWindowHandle, Responding, HasExited
```

A successful launch should show a live `DesktopFlyouts.Wasdk.Sample.App` process with `Responding = True` and a non-zero `MainWindowHandle`.

## Debugging

Use Event Viewer for startup and packaged-launch failures. Check `Windows Logs > Application` first, especially `.NET Runtime` event `1026`, `Application Error` event `1000`, and `Windows Error Reporting` event `1001`.

From PowerShell, read the same Application log with:

```powershell
Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=(Get-Date).AddMinutes(-10)} |
    Where-Object { $_.Message -match 'DesktopFlyouts.Wasdk.Sample.App|DesktopFlyouts' } |
    Select-Object -First 10 TimeCreated, ProviderName, Id, LevelDisplayName, Message |
    Format-List
```

If the log shows `0x80040154 REGDB_E_CLASSNOTREG` from `WindowsAppRuntime.DeploymentInitializeOptions`, treat it as an invalid unpackaged/direct `.exe` launch path before blaming app code. Re-run the packaged launch steps above.

Stop running samples before rebuilding if output files are locked:

```powershell
Get-Process -Name DesktopFlyouts.Wasdk.Sample.App -ErrorAction SilentlyContinue | Stop-Process -Force
```

## Validation Checklist

Validate one item at a time:

1. `git status --short --branch` to understand the current branch and dirty files.
2. Build the WASDK sample.
3. Build the UWP library when shared code changed.
4. Launch the WASDK sample as packaged, never by direct `.exe`.
5. Confirm the app has a responsive process and real window handle.
6. Run `git diff --check`.
7. Confirm changed text files remain CRLF.
8. Summarize what was changed, what was validated, and any remaining risk.

