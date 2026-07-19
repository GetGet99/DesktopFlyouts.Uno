#if HAS_UNO
// Real implementation: tray icon for KDE Plasma via StatusNotifierItem D-Bus protocol.
// https://specifications.freedesktop.org/status-notifier-item/latest/status-notifier-item.html

using System;
using System.Drawing;

namespace DesktopFlyouts
{
    /// <summary>
    /// Provides functionality for displaying and managing a system tray icon in the taskbar.
    /// </summary>
    public partial class SystemTrayIcon : IDisposable
    {
        private bool _disposed;
        private StatusNotifierItemBackend? _backend;

        /// <summary>
        /// Gets the full path to the current icon file, or <see langword="null"/> if the icon was set
        /// from an existing handle.
        /// </summary>
        public string? IconPath { get; private set; }

        private string _Tooltip = string.Empty;

        /// <summary>
        /// Gets or sets the tooltip text shown for the tray icon.
        /// </summary>
        public string Tooltip
        {
            get => _Tooltip;
            set
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _Tooltip = value;
                _backend?.SetTooltip(value);
            }
        }

        private bool _IsVisible;

        /// <summary>
        /// Gets or sets whether the tray icon is visible.
        /// </summary>
        public bool IsVisible
        {
            get => _IsVisible;
            set
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _IsVisible = value;
                _backend?.SetStatus(value ? "Active" : "Passive");
            }
        }

        /// <summary>
        /// Gets the stable identifier used for the tray icon.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Occurs when the tray icon receives a left-click.
        /// </summary>
        public event EventHandler<MouseEventReceivedEventArgs>? LeftClicked;

        /// <summary>
        /// Occurs when the tray icon receives a right-click.
        /// </summary>
        public event EventHandler<MouseEventReceivedEventArgs>? RightClicked;

        /// <summary>
        /// Occurs when the tray icon receives a left double-click.
        /// </summary>
        public event EventHandler<MouseEventReceivedEventArgs>? LeftDoubleClicked;

        /// <summary>
        /// Occurs when the tray icon receives a right double-click.
        /// </summary>
        public event EventHandler<MouseEventReceivedEventArgs>? RightDoubleClicked;

        /// <summary>
        /// Initializes a new instance of <see cref="SystemTrayIcon"/> with an icon loaded from a
        /// file path.
        /// </summary>
        public SystemTrayIcon(string iconPath, string tooltip, Guid id)
        {
            IconPath = iconPath;
            _Tooltip = tooltip;
            Id = id;
            _backend = new StatusNotifierItemBackend(iconPath, tooltip, id);
            _backend.LeftClicked += OnLeftClicked;
            _backend.RightClicked += OnRightClicked;
            _backend.LeftDoubleClicked += OnLeftDoubleClicked;
            _backend.RightDoubleClicked += OnRightDoubleClicked;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="SystemTrayIcon"/> with an existing icon handle.
        /// </summary>
        public SystemTrayIcon(nint hIcon, string tooltip, Guid id)
        {
            _Tooltip = tooltip;
            Id = id;
            // On Linux, nint icon handles are not supported. The icon will use a default.
            _backend = new StatusNotifierItemBackend(string.Empty, tooltip, id);
            _backend.LeftClicked += OnLeftClicked;
            _backend.RightClicked += OnRightClicked;
            _backend.LeftDoubleClicked += OnLeftDoubleClicked;
            _backend.RightDoubleClicked += OnRightDoubleClicked;
        }

        /// <summary>
        /// Makes the tray icon visible and synchronizes its state to the shell.
        /// </summary>
        public void Show()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _IsVisible = true;
            _backend?.Show();
        }

        /// <summary>
        /// Replaces the current icon with one loaded from a file path.
        /// </summary>
        public void SetIcon(string iconPath)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            IconPath = iconPath;
            _backend?.SetIcon(iconPath);
        }

        /// <summary>
        /// Replaces the current icon with an existing icon handle.
        /// </summary>
        public void SetIcon(nint hIcon)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            // On Linux, nint icon handles are not supported.
            IconPath = null;
        }

        /// <summary>
        /// Removes the associated notification icon from the system tray.
        /// </summary>
        public void Destroy()
        {
            _IsVisible = false;
            _backend?.Destroy();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _backend?.Dispose();
            _backend = null;
            GC.SuppressFinalize(this);
        }

        private void OnLeftClicked(object? sender, MouseEventReceivedEventArgs e)
            => LeftClicked?.Invoke(this, e);

        private void OnRightClicked(object? sender, MouseEventReceivedEventArgs e)
            => RightClicked?.Invoke(this, e);

        private void OnLeftDoubleClicked(object? sender, MouseEventReceivedEventArgs e)
            => LeftDoubleClicked?.Invoke(this, e);

        private void OnRightDoubleClicked(object? sender, MouseEventReceivedEventArgs e)
            => RightDoubleClicked?.Invoke(this, e);
    }
}
#endif
