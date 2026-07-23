using System;
using System.Diagnostics;
using Windows.Foundation;

namespace DesktopFlyouts;

internal partial class TrayIconManager : IDisposable
{
    private static readonly Lazy<TrayIconManager> _default = new(() => new TrayIconManager());
    internal static TrayIconManager Default => _default.Value;

    internal DesktopFlyout? DesktopFlyout { get; set; }
    internal DesktopMenuFlyout? DesktopMenuFlyout { get; set; }
    internal DesktopFlyoutSampleKind SelectedFlyoutExample { get; private set; }

    private bool _disposed;

    private TrayIconManager() { }

    internal void Initialize()
    {
        Debug.WriteLine("[TrayIconManager] Initialize called.");
        DesktopFlyout = CreateFlyout(SelectedFlyoutExample);
        Debug.WriteLine($"[TrayIconManager] Created flyout: {DesktopFlyout?.GetType().Name}");
        DesktopMenuFlyout = new MainDesktopMenuFlyout();
    }

    internal void SwitchFlyout(DesktopFlyoutSampleKind example)
    {
        Debug.WriteLine($"[TrayIconManager] SwitchFlyout to {example}, disposed={_disposed}");
        if (_disposed || (DesktopFlyout is not null && SelectedFlyoutExample == example))
            return;

        var oldFlyout = DesktopFlyout;
        var newFlyout = CreateFlyout(example);

        DesktopFlyout = newFlyout;
        SelectedFlyoutExample = example;
        oldFlyout?.Dispose();
    }

    private static DesktopFlyout CreateFlyout(DesktopFlyoutSampleKind example)
    {
        return example switch
        {
            DesktopFlyoutSampleKind.Button => new ButtonFlyout(),
            DesktopFlyoutSampleKind.IndicatorStyle => new IndicatorStyleFlyout(),
            DesktopFlyoutSampleKind.NotificationCenterStyle => new NotificationCenterStyleFlyout(),
            DesktopFlyoutSampleKind.StartMenuStyle => new StartMenuStyleFlyout(),
            DesktopFlyoutSampleKind.StickySmallStyle => new StickySmallFlyout(),
            DesktopFlyoutSampleKind.WidgetStyle => new WidgetStyleFlyout(),
            DesktopFlyoutSampleKind.Severity => new SeverityFlyout(),
            _ => new CustomizableFlyout(),
        };
    }

    internal void ToggleFlyout(Point? point = null)
    {
        Debug.WriteLine($"[TrayIconManager] ToggleFlyout called. point={point}, flyout={DesktopFlyout?.GetType().Name}, isOpen={DesktopFlyout?.IsOpen}");
        if (DesktopFlyout is null)
            return;

        if (DesktopFlyout.IsOpen)
        {
            DesktopFlyout.Hide();
        }
        else
        {
            if (DesktopFlyout is StickySmallFlyout && point is not null)
            {
                DesktopFlyout.Show(new((int)point.Value.X, (int)point.Value.Y));
            }
            else
            {
                DesktopFlyout.Show();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        DesktopFlyout?.Dispose();
        DesktopMenuFlyout?.Dispose();

        DesktopFlyout = null;
        DesktopMenuFlyout = null;

        GC.SuppressFinalize(this);
    }
}
