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
    public partial class XamlIslandApplication : Application
    {
    }
#endif
}
