#if !WINDOWS
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using CommunityToolkit.WinUI;
using FoundationSize = Windows.Foundation.Size;
using Windows.Graphics;

#if UWP
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;
using Windows.Win32.UI.WindowsAndMessaging;
#elif WASDK
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
#endif

namespace DesktopFlyouts
{
    /// <summary>
    /// Displays a menu flyout in an independent XAML island window.
    /// </summary>
    /// <remarks>
    /// Use <see cref="DesktopMenuFlyout"/> when a context menu must be opened at a physical screen
    /// point, such as a tray icon click. Add <see cref="MenuFlyoutItemBase"/> items as children and
    /// call the point-based show overload to display the menu.
    /// Submenus are not supported in this implementation.
    /// </remarks>
    [ContentProperty(Name = nameof(Items))]
    public partial class DesktopMenuFlyout : ItemsControl, IDisposable
    {
        private const string PART_MenuFlyoutTargetControl = "PART_MenuFlyoutTargetControl";
        private const string LogTag = "[DesktopMenuFlyout]";

        private readonly XamlIslandHostWindow? _host;
        private MenuFlyoutPresenter? _presenter;
        private bool _disposed;
        private bool _isShowPending;

        private Panel? MenuFlyoutTargetControl;

        /// <summary>
        /// Gets whether the menu flyout is currently open.
        /// </summary>
        /// <value><see langword="true"/> while the hosted menu is open; otherwise, <see langword="false"/>.</value>
        [GeneratedDependencyProperty]
        public partial bool IsOpen { get; private set; }

        /// <summary>
        /// Initializes a new instance of <see cref="DesktopMenuFlyout"/>.
        /// </summary>
        /// <remarks>
        /// The constructor creates the hidden desktop host window used to display the menu.
        /// </remarks>
        public DesktopMenuFlyout()
        {
            Debug.WriteLine($"{LogTag} Constructor: creating XamlIslandHostWindow");
            DefaultStyleKey = typeof(DesktopMenuFlyout);

            _host = new XamlIslandHostWindow();
            _host.SetContent(this);
            _ = _host.UpdateWindowVisibility(false);
            _host.SystemSettingsChanged += HostWindow_SystemSettingsChanged;

            Debug.WriteLine($"{LogTag} Constructor: host created, content set, window hidden");
        }

        /// <inheritdoc/>
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            MenuFlyoutTargetControl = GetTemplateChild(PART_MenuFlyoutTargetControl) as Panel;
            Debug.WriteLine($"{LogTag} OnApplyTemplate: MenuFlyoutTargetControl={(MenuFlyoutTargetControl?.GetType().Name ?? "NULL")}");
        }

        /// <inheritdoc/>
        protected override void OnItemsChanged(object e)
        {
            base.OnItemsChanged(e);

            Debug.WriteLine($"{LogTag} OnItemsChanged: Items.Count={Items.Count}, disposed={_disposed}");

            if (_disposed)
                return;

            EnsurePresenter();
        }

        private void EnsurePresenter()
        {
            var isNew = _presenter is null;
            _presenter ??= new MenuFlyoutPresenter();

            _presenter.Items.Clear();

            foreach (var item in Items)
                _presenter.Items.Add((MenuFlyoutItemBase)item);

            Debug.WriteLine($"{LogTag} EnsurePresenter: created={isNew}, presenter.Items.Count={_presenter.Items.Count}");
        }

        /// <summary>
        /// Opens the menu flyout at the specified screen point.
        /// </summary>
        /// <param name="point">The physical screen pixel where the menu host should be positioned.</param>
        /// <remarks>
        /// The menu is built from the current <see cref="ItemsControl.Items"/> collection. Items must
        /// derive from <see cref="MenuFlyoutItemBase"/>. Submenus are not supported in this implementation.
        /// </remarks>
        public void Show(Point point)
        {
            Debug.WriteLine($"{LogTag} Show({point.X}, {point.Y}): disposed={_disposed}, IsOpen={IsOpen}, _isShowPending={_isShowPending}");

            if (_disposed)
                return;

            // If a previous show is still pending its deferred layout, hide first.
            if (_isShowPending || IsOpen)
            {
                Debug.WriteLine($"{LogTag} Show: previous show pending or already open, hiding first");
                HideImmediate();
            }

            EnsurePresenter();

            Debug.WriteLine($"{LogTag} Show: MenuFlyoutTargetControl={(MenuFlyoutTargetControl?.GetType().Name ?? "NULL")}, host={(_host is not null ? "OK" : "NULL")}");

            UpdateFlyoutTheme();

            // Add the presenter to the visual tree.
            MenuFlyoutTargetControl?.Children.Clear();
            MenuFlyoutTargetControl?.Children.Add(_presenter);

            // Get the work area for positioning.
            var workArea = WindowHelpers.GetFlyoutWorkAreaRect(new Point(point.X, point.Y));
            Debug.WriteLine($"{LogTag} Show: workArea=({workArea.X}, {workArea.Y}, {workArea.Width}, {workArea.Height})");

            // Step 1: Place the host window at the target position with a generous initial size
            // so that XAML can compute layout for the MenuFlyoutPresenter.
            var initRegion = new RectInt32()
            {
                X = point.X,
                Y = point.Y - workArea.Height / 2,
                Width = workArea.Width / 2,
                Height = workArea.Height / 2
            };

            Debug.WriteLine($"{LogTag} Show (pass 1): initial region=({initRegion.X}, {initRegion.Y}, {initRegion.Width}, {initRegion.Height})");

            _host?.MoveAndResize(initRegion);
            _host?.SetHWndRectRegion(new RectInt32() { Width = initRegion.Width, Height = initRegion.Height });
            _ = _host?.UpdateWindowVisibility(true);

            IsOpen = true;
            _isShowPending = true;

            Debug.WriteLine($"{LogTag} Show: host mapped, waiting for layout pass...");

            // Step 2: After the window is mapped and XAML has had a layout pass,
            // measure the presenter and resize to the correct size.
            var showPoint = new Point(point.X, point.Y);
            _ = Task.Run(async () =>
            {
#if UWP
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => FinalizeShow(showPoint));
#elif WASDK
                DispatcherQueue.TryEnqueue(() => FinalizeShow(showPoint));
#endif
            });
        }

        private void FinalizeShow(Point point)
        {
            if (_disposed || !_isShowPending || _presenter is null || MenuFlyoutTargetControl is null)
            {
                Debug.WriteLine($"{LogTag} FinalizeShow: aborted (disposed={_disposed}, pending={_isShowPending})");
                _isShowPending = false;
                return;
            }

            Debug.WriteLine($"{LogTag} FinalizeShow: measuring presenter...");

            // Force a layout pass so the presenter calculates its desired size.
            _presenter.Measure(new FoundationSize(double.PositiveInfinity, double.PositiveInfinity));
            _presenter.Arrange(new Windows.Foundation.Rect(0, 0, _presenter.DesiredSize.Width, _presenter.DesiredSize.Height));

            // Get the desired size of the presenter.
            var presenterWidth = _presenter.ActualWidth > 0 ? _presenter.ActualWidth : _presenter.DesiredSize.Width;
            var presenterHeight = _presenter.ActualHeight > 0 ? _presenter.ActualHeight : _presenter.DesiredSize.Height;

            Debug.WriteLine($"{LogTag} FinalizeShow: DesiredSize=({_presenter.DesiredSize.Width}, {_presenter.DesiredSize.Height}), ActualSize=({_presenter.ActualWidth}, {_presenter.ActualHeight}), resolved=({presenterWidth}, {presenterHeight})");

            // Ensure minimum size.
            var regionWidth = Math.Max(1, (int)Math.Ceiling(presenterWidth));
            var regionHeight = Math.Max(1, (int)Math.Ceiling(presenterHeight));

            Debug.WriteLine($"{LogTag} FinalizeShow: regionSize=({regionWidth}, {regionHeight})");

            // Get the work area for positioning.
            var workArea = WindowHelpers.GetFlyoutWorkAreaRect(new Point(point.X, point.Y));

            // Calculate position: center horizontally on point, place above the point (like a context menu).
            var left = (double)point.X - (regionWidth / 2D);
            var top = (double)point.Y - regionHeight;

            Debug.WriteLine($"{LogTag} FinalizeShow: raw position=({left}, {top})");

            // Clamp to work area.
            left = Clamp(left, workArea.Left, workArea.Right - regionWidth);
            top = Clamp(top, workArea.Top, workArea.Bottom - regionHeight);

            var region = new RectInt32()
            {
                X = (int)Math.Round((double)left),
                Y = (int)Math.Round((double)top),
                Width = regionWidth,
                Height = regionHeight
            };

            Debug.WriteLine($"{LogTag} FinalizeShow: final region=({region.X}, {region.Y}, {region.Width}, {region.Height})");

            // Resize and reposition the host window to the correct size.
            _host?.MoveAndResize(region);
            _host?.SetHWndRectRegion(new RectInt32() { Width = regionWidth, Height = regionHeight });

            _isShowPending = false;
            Debug.WriteLine($"{LogTag} FinalizeShow: complete");
        }

        /// <summary>
        /// Closes the menu flyout.
        /// </summary>
        /// <remarks>
        /// This also hides the desktop host window.
        /// </remarks>
        public void Hide()
        {
            Debug.WriteLine($"{LogTag} Hide(): disposed={_disposed}, IsOpen={IsOpen}, _isShowPending={_isShowPending}");

            if (_disposed)
                return;

            _isShowPending = false;
            HideImmediate();
        }

        private void HideImmediate()
        {
            Debug.WriteLine($"{LogTag} HideImmediate()");

            _ = _host?.UpdateWindowVisibility(false);

            // Remove the presenter from the visual tree.
            MenuFlyoutTargetControl?.Children.Clear();

            IsOpen = false;
            Debug.WriteLine($"{LogTag} HideImmediate(): complete, IsOpen=false");
        }

        private void HostWindow_SystemSettingsChanged(object? sender, EventArgs e)
        {
            Debug.WriteLine($"{LogTag} HostWindow_SystemSettingsChanged: disposed={_disposed}");

            if (_disposed)
                return;

            UpdateFlyoutTheme();
        }

#if UWP
        /// <summary>
        /// Lets the XAML island process a native keyboard message before dispatch.
        /// </summary>
        /// <param name="msg">The native message to process.</param>
        /// <returns><see langword="true"/> if the message was handled; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// UWP desktop-host scenarios should call this from their native message loop so keyboard
        /// navigation and accelerator processing can reach the hosted XAML island.
        /// </remarks>
        public unsafe bool TryPreTranslateMessage(MSG* msg)
        {
            return _host?.TryPreTranslateMessage(msg) ?? false;
        }
#endif

        private void UpdateFlyoutTheme()
        {
            var isLight = GeneralHelpers.IsTaskbarLight();
            Debug.WriteLine($"{LogTag} UpdateFlyoutTheme: isTaskbarLight={isLight}");
            RequestedTheme = isLight ? ElementTheme.Light : ElementTheme.Dark;
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Debug.WriteLine($"{LogTag} Dispose(): disposed={_disposed}");

            if (_disposed)
                return;

            _disposed = true;
            _isShowPending = false;

            // Remove the presenter from the visual tree.
            MenuFlyoutTargetControl?.Children.Clear();

            if (_presenter is not null)
            {
                _presenter.Items.Clear();
                _presenter = null;
            }

            _host?.SystemSettingsChanged -= HostWindow_SystemSettingsChanged;
            _host?.Dispose();
            IsOpen = false;

            Debug.WriteLine($"{LogTag} Dispose(): complete");

            GC.SuppressFinalize(this);
        }
    }
}

#endif
