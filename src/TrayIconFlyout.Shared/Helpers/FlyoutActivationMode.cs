// Copyright (c) 0x5BFA. All rights reserved.
// Licensed under the MIT license.

namespace U5BFA.Libraries
{
    /// <summary>
    /// Specifies how a flyout interacts with window activation and focus.
    /// </summary>
    public enum FlyoutActivationMode
    {
        /// <summary>
        /// Activates the flyout when it opens.
        /// </summary>
        Activate,

        /// <summary>
        /// Opens the flyout without activating it.
        /// </summary>
        NoActivateOnOpen,

        /// <summary>
        /// Prevents the flyout from becoming active or focused.
        /// </summary>
        NeverActivate,
    }
}
