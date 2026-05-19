// Copyright (c) 0x5BFA. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Drawing;

namespace U5BFA.Libraries
{
    /// <summary>
    /// Provides the screen point associated with a tray icon mouse event.
    /// </summary>
    public class MouseEventReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the screen point associated with the mouse event.
        /// </summary>
        public Point Point { get; }

        internal MouseEventReceivedEventArgs(Point point)
        {
            Point = point;
        }
    }
}
