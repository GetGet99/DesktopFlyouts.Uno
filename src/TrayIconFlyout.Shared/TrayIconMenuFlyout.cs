// Copyright (c) 0x5BFA. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Drawing;

#if UWP
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;
using Windows.Win32.UI.WindowsAndMessaging;
using Windows.Win32.Foundation;

#elif WASDK
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
#endif

namespace U5BFA.Libraries
{
    /// <summary>
    /// Displays a tray icon menu flyout hosted in a XAML island window.
    /// </summary>
    [ContentProperty(Name = nameof(Items))]
    public partial class TrayIconMenuFlyout : ItemsControl, IDisposable
    {
        private const string PART_RootGrid = "PART_RootGrid";
        private const string PART_MenuFlyoutTargetControl = "PART_MenuFlyoutTargetControl";

        private readonly XamlIslandHostWindow? _host;
        private MenuFlyout? _menuFlyout;
        private bool _disposed;

        private Border? MenuFlyoutTargetControl;

        /// <summary>
        /// Identifies the <see cref="IsOpen"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(TrayIconMenuFlyout), new PropertyMetadata(false));

        /// <summary>
        /// Gets whether the menu flyout is currently open.
        /// </summary>
        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            private set => SetValue(IsOpenProperty, value);
        }

        /// <summary>
        /// Initializes a new instance of <see cref="TrayIconMenuFlyout"/>.
        /// </summary>
        public TrayIconMenuFlyout()
        {
            DefaultStyleKey = typeof(TrayIconMenuFlyout);

            _host = new XamlIslandHostWindow();
            _host.SetContent(this);
            _host.UpdateWindowVisibility(false);
        }

        /// <inheritdoc/>
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (GetTemplateChild(PART_RootGrid) is not Grid)
                throw new MissingFieldException($"Could not find {PART_RootGrid} in the given {nameof(TrayIconMenuFlyout)}'s style.");
            MenuFlyoutTargetControl = GetTemplateChild(PART_MenuFlyoutTargetControl) as Border
                ?? throw new MissingFieldException($"Could not find {PART_MenuFlyoutTargetControl} in the given {nameof(TrayIconMenuFlyout)}'s style.");
        }

        /// <inheritdoc/>
        protected override void OnItemsChanged(object e)
        {
            base.OnItemsChanged(e);

            if (_disposed)
                return;

            if (_menuFlyout is null)
            {
                _menuFlyout = new MenuFlyout();
                _menuFlyout.Closed += MenuFlyout_Closed;
            }

            _menuFlyout.Items.Clear();

            foreach (var item in Items)
                _menuFlyout.Items.Add((MenuFlyoutItemBase)item);
        }

        /// <summary>
        /// Opens the menu flyout at the specified screen point.
        /// </summary>
        /// <param name="point">The screen point used to position the menu.</param>
        public void Show(Point point)
        {
            if (_disposed || _menuFlyout is null)
                return;

            UpdateFlyoutTheme();

            _host?.MoveAndResize(new(point.X, point.Y, 0, 0));
            _host?.SetHWndRectRegion(new(0, 0, 1, 1));
            _host?.UpdateWindowVisibility(true);

            _menuFlyout.ShowAt(MenuFlyoutTargetControl);

            IsOpen = true;
        }

        /// <summary>
        /// Closes the menu flyout.
        /// </summary>
        public void Hide()
        {
            if (_disposed)
                return;

            _host?.UpdateWindowVisibility(false);

            _menuFlyout?.Hide();

            IsOpen = false;
        }

        private void MenuFlyout_Closed(object? sender, object e)
        {
            _host?.UpdateWindowVisibility(false);
            IsOpen = false;
        }

#if UWP
        /// <summary>
        /// Lets the XAML island process a native keyboard message before dispatch.
        /// </summary>
        /// <param name="msg">The native message to process.</param>
        /// <returns><see langword="true"/> if the message was handled; otherwise, <see langword="false"/>.</returns>
        public unsafe bool TryPreTranslateMessage(MSG* msg)
        {
            return _host?.TryPreTranslateMessage(msg) ?? false;
        }
#endif

        private void UpdateFlyoutTheme()
        {
            RequestedTheme = GeneralHelpers.IsTaskbarLight() ? ElementTheme.Light : ElementTheme.Dark;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_menuFlyout is not null)
            {
                _menuFlyout.Closed -= MenuFlyout_Closed;
                if (IsOpen)
                    _menuFlyout.Hide();

                _menuFlyout.Items.Clear();
                _menuFlyout = null;
            }

            _host?.Dispose();
            IsOpen = false;

            GC.SuppressFinalize(this);
        }
    }
}
