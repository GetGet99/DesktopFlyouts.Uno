using System;
using System.Collections.Generic;
using System.Text;
using Windows.UI.Xaml;

namespace DesktopFlyouts.Shared
{
#if UWP
    /// <summary>
    /// Provides the UWP XAML application used by XAML island hosting.
    /// </summary>
    /// <remarks>
    /// This type is used by the UWP package to initialize XAML island hosting infrastructure.
    /// Application code usually does not need to create it directly.
    /// </remarks>
    public partial class XamlIslandApplication : Application
    {
    }
#endif
}
