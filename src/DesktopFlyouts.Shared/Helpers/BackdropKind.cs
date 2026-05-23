// Copyright (c) 0x5BFA. All rights reserved.
// Licensed under the MIT license.

namespace U5BFA.Libraries
{
    /// <summary>
    /// Specifies the system backdrop used by a flyout.
    /// </summary>
    /// <remarks>
    /// This setting affects Windows App SDK builds when
    /// <see cref="DesktopFlyout.IsBackdropEnabled"/> is enabled.
    /// </remarks>
    public enum BackdropKind
    {
        /// <summary>
        /// Uses an acrylic backdrop.
        /// </summary>
        Acrylic,

        /// <summary>
        /// Uses a mica backdrop.
        /// </summary>
        Mica,
    }
}
