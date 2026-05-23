# SystemTrayIcon

`SystemTrayIcon` is an optional helper for creating a Win32 notification icon and receiving click events.

The flyout controls do not require a tray icon. Use this type only when your app needs tray integration.

## Basic usage

```csharp
var trayIcon = new SystemTrayIcon(
    iconPath: "Assets/AppIcon.ico",
    tooltip: "My app",
    id: new Guid("00000000-0000-0000-0000-000000000000"));

trayIcon.LeftClicked += TrayIcon_LeftClicked;
trayIcon.RightClicked += TrayIcon_RightClicked;
trayIcon.Show();
```

## Click points

`LeftClicked` and `RightClicked` provide the center point of the tray icon in physical screen pixels.

```csharp
private void TrayIcon_LeftClicked(object? sender, MouseEventReceivedEventArgs e)
{
    flyout.Show(e.Point);
}

private void TrayIcon_RightClicked(object? sender, MouseEventReceivedEventArgs e)
{
    menu.Show(e.Point);
}
```

## Updating the icon

Setting `IconPath`, `Tooltip`, or `IsVisible` updates the existing shell icon immediately.

```csharp
trayIcon.Tooltip = "New tooltip";
trayIcon.IsVisible = false;
trayIcon.IsVisible = true;
```

`IconPath` must point to an icon file that can be loaded by the Win32 `LoadImage` API. `Show()` throws `ArgumentOutOfRangeException` when the icon cannot be loaded.

## Cleanup

Call `Destroy()` when the tray icon should be removed.

```csharp
trayIcon.Destroy();
```

Unsubscribe event handlers when the owning object is disposed.
