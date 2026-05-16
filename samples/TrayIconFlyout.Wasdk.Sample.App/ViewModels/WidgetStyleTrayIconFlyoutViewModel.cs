// Copyright (c) 0x5BFA. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace U5BFA.Libraries;

internal partial class WidgetStyleTrayIconFlyoutViewModel : ObservableObject
{
    [ObservableProperty]
    internal partial string? NowDateText { get; set; }

    internal WidgetStyleTrayIconFlyoutViewModel()
    {
        NowDateText = "May 5";
    }
}
