using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel;

namespace DesktopFlyouts;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        AppDomain.CurrentDomain.ProcessExit += AppDomain_ProcessExit;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        TrayIconManager.Default.Initialize();

        _window = new Window();
        _window.Content = new RootView();
        _window.Closed += (_, _) => TrayIconManager.Default.Dispose();
        _window.Activate();
        Debug.WriteLine("[App] Main window activated.");
    }

    private void AppDomain_ProcessExit(object? sender, EventArgs e)
    {
        TrayIconManager.Default.Dispose();
    }
}
