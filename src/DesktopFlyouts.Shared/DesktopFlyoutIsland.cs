// Copyright (c) 0x5BFA. All rights reserved.
// Licensed under the MIT license.

using System;

#if UWP
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
#elif WASDK
using Microsoft.UI.Content;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
#endif

namespace U5BFA.Libraries
{
    /// <summary>
    /// Represents a content section inside a <see cref="DesktopFlyout"/>.
    /// </summary>
    /// <remarks>
    /// Islands are arranged by their owner flyout according to
    /// <see cref="DesktopFlyout.IslandsOrientation"/>. Put the XAML content for one visual section
    /// in each island.
    /// </remarks>
    public partial class DesktopFlyoutIsland : ContentControl
    {
        private const string PART_RootGrid = "PART_RootGrid";
        private const string PART_BackdropTargetGrid = "PART_BackdropTargetGrid";
        private const string PART_MainContentPresenter = "PART_MainContentPresenter";

        private Grid? RootGrid;
#if WASDK
        private Grid? BackdropTargetGrid;
#endif
        private ContentPresenter? MainContentPresenter;

#if WASDK
        // Use ContentExternalBackdropLink to apply backdrop to normal UI elements
        private ContentExternalBackdropLink? _backdropLink;
        private bool _isBackdropLinkAttached;
#endif

        private WeakReference<DesktopFlyout>? _owner;
        private long _propertyChangedCallbackTokenForContentProperty;
        private long _propertyChangedCallbackTokenForCornerRadiusProperty;

        /// <summary>
        /// Identifies the <see cref="IslandWidth"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty IslandWidthProperty =
            DependencyProperty.Register(nameof(IslandWidth), typeof(GridLength), typeof(DesktopFlyoutIsland), new PropertyMetadata(GridLength.Auto, OnIslandSizePropertyChanged));

        /// <summary>
        /// Gets or sets the island width.
        /// </summary>
        /// <value>The width used when the owner flyout arranges islands horizontally. The default is <see cref="GridLength.Auto"/>.</value>
        public GridLength IslandWidth
        {
            get => (GridLength)GetValue(IslandWidthProperty);
            set => SetValue(IslandWidthProperty, value);
        }

        /// <summary>
        /// Identifies the <see cref="IslandHeight"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty IslandHeightProperty =
            DependencyProperty.Register(nameof(IslandHeight), typeof(GridLength), typeof(DesktopFlyoutIsland), new PropertyMetadata(GridLength.Auto, OnIslandSizePropertyChanged));

        /// <summary>
        /// Gets or sets the island height.
        /// </summary>
        /// <value>The height used when the owner flyout arranges islands vertically. The default is <see cref="GridLength.Auto"/>.</value>
        public GridLength IslandHeight
        {
            get => (GridLength)GetValue(IslandHeightProperty);
            set => SetValue(IslandHeightProperty, value);
        }

        /// <summary>
        /// Initializes a new instance of <see cref="DesktopFlyoutIsland"/>.
        /// </summary>
        /// <remarks>
        /// The island applies its default style when it is loaded by the owning XAML framework.
        /// </remarks>
        public DesktopFlyoutIsland()
        {
            DefaultStyleKey = typeof(DesktopFlyoutIsland);
        }

        /// <inheritdoc/>
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            RootGrid = GetTemplateChild(PART_RootGrid) as Grid
                ?? throw new MissingFieldException($"Could not find {PART_RootGrid} in the given {nameof(DesktopFlyoutIsland)}'s style.");
#if WASDK
            BackdropTargetGrid = GetTemplateChild(PART_BackdropTargetGrid) as Grid
                ?? throw new MissingFieldException($"Could not find {PART_BackdropTargetGrid} in the given {nameof(DesktopFlyoutIsland)}'s style.");
#endif
            MainContentPresenter = GetTemplateChild(PART_MainContentPresenter) as ContentPresenter
                ?? throw new MissingFieldException($"Could not find {PART_MainContentPresenter} in the given {nameof(DesktopFlyoutIsland)}'s style.");

            _propertyChangedCallbackTokenForContentProperty = RegisterPropertyChangedCallback(ContentProperty, (s, e) => ((DesktopFlyoutIsland)s).OnContentChanged());
            _propertyChangedCallbackTokenForCornerRadiusProperty = RegisterPropertyChangedCallback(CornerRadiusProperty, (s, e) => ((DesktopFlyoutIsland)s).OnCornerRadiusChanged());

#if WASDK
            SizeChanged += DesktopFlyoutIsland_SizeChanged;
            MainContentPresenter.SizeChanged += MainContentPresenter_SizeChanged;
#endif
            Unloaded += DesktopFlyoutIsland_Unloaded;
        }

        internal void SetOwner(DesktopFlyout owner)
        {
            _owner = new(owner);
        }

        private static void OnIslandSizePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            if (dependencyObject is not DesktopFlyoutIsland island)
                return;

            if (island._owner is not null && island._owner.TryGetTarget(out var owner))
                owner.OnIslandSizeChanged();
        }

#if WASDK
        internal void UpdateBackdrop(bool isEnabled, bool coerce = false)
        {
            if (_owner is null || !_owner.TryGetTarget(out var owner) || owner.BackdropManager is null)
                return;

            if (isEnabled)
            {
                if (_isBackdropLinkAttached)
                {
                    if (coerce)
                    {
                        if (_backdropLink is null)
                            return;

                        owner.BackdropManager.RemoveLink(_backdropLink);
                        _backdropLink = null;
                        _isBackdropLinkAttached = false;
                    }
                    else return;
                }

                _backdropLink = owner.BackdropManager.CreateLink();
                _isBackdropLinkAttached = true;
                UpdateBackdropVisual();
            }
            else
            {
                if (_backdropLink is null) return;

                owner.BackdropManager.RemoveLink(_backdropLink);
                _backdropLink = null;
                _isBackdropLinkAttached = false;
            }
        }

        internal void UpdateBackdropVisual()
        {
            if (BackdropTargetGrid is null || _backdropLink is null || MainContentPresenter is null)
                return;

            _backdropLink.PlacementVisual.Size = new((float)ActualWidth, (float)ActualHeight);
            _backdropLink.PlacementVisual.Clip = _backdropLink.PlacementVisual.Compositor.CreateRectangleClip(
                0, 0, (float)MainContentPresenter.ActualWidth, (float)MainContentPresenter.ActualHeight,
                new(Convert.ToSingle(BackdropTargetGrid.CornerRadius.TopLeft - 1), Convert.ToSingle(BackdropTargetGrid.CornerRadius.TopLeft - 1)),
                new(Convert.ToSingle(BackdropTargetGrid.CornerRadius.TopRight - 1), Convert.ToSingle(BackdropTargetGrid.CornerRadius.TopRight - 1)),
                new(Convert.ToSingle(BackdropTargetGrid.CornerRadius.BottomRight - 1), Convert.ToSingle(BackdropTargetGrid.CornerRadius.BottomRight - 1)),
                new(Convert.ToSingle(BackdropTargetGrid.CornerRadius.BottomLeft - 1), Convert.ToSingle(BackdropTargetGrid.CornerRadius.BottomLeft - 1)));

            ElementCompositionPreview.SetElementChildVisual(BackdropTargetGrid, _backdropLink.PlacementVisual);
        }
#endif

        private void OnContentChanged()
        {
            if (Content is not FrameworkElement newContent || MainContentPresenter is null)
                return;

            if (MainContentPresenter.Content is FrameworkElement oldContent)
                oldContent.SizeChanged -= Content_SizeChanged;

            newContent.SizeChanged += Content_SizeChanged;
            MainContentPresenter.Content = Content;
        }

        private void OnCornerRadiusChanged()
        {
#if WASDK
            // Update backdrop visual to reflect the new corner radius
            UpdateBackdropVisual();
#endif
        }

        private void Content_SizeChanged(object sender, SizeChangedEventArgs e)
        {
#if WASDK
            UpdateBackdropVisual();
#endif
        }

#if WASDK
        private void DesktopFlyoutIsland_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateBackdropVisual();
        }

        private void MainContentPresenter_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateBackdropVisual();
        }
#endif

        private void DesktopFlyoutIsland_Unloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= DesktopFlyoutIsland_Unloaded;
#if WASDK
            SizeChanged -= DesktopFlyoutIsland_SizeChanged;

            if (MainContentPresenter is not null)
                MainContentPresenter.SizeChanged -= MainContentPresenter_SizeChanged;
#endif

            UnregisterPropertyChangedCallback(ContentProperty, _propertyChangedCallbackTokenForContentProperty);
            UnregisterPropertyChangedCallback(CornerRadiusProperty, _propertyChangedCallbackTokenForCornerRadiusProperty);
        }
    }
}
