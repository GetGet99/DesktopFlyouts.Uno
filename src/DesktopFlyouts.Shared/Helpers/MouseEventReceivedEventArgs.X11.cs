#if HAS_UNO
// Polyfill for MouseEventReceivedEventArgs.
// The real implementation is a simple event args class used by SystemTrayIcon.

using System;
using System.Drawing;

namespace DesktopFlyouts
{
    /// <summary>
    /// Provides data for mouse events originating from a system tray icon.
    /// </summary>
    public class MouseEventReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the screen point of the tray icon in physical pixels.
        /// </summary>
        public Point Point { get; }

        internal MouseEventReceivedEventArgs(Point point)
        {
            Point = point;
        }
    }
}
#endif
