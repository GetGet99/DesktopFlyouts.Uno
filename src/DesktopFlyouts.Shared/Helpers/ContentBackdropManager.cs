// Copyright (c) 0x5BFA. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;

#if WASDK
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Content;
using Microsoft.UI.Xaml;
#endif

namespace U5BFA.Libraries
{
#if WASDK
    internal partial class ContentBackdropManager : IDisposable
    {
        private ISystemBackdropControllerWithTargets? _backdropController;
        private SystemBackdropConfiguration? _configuration;
        private Compositor? _compositor;
        private readonly List<ContentExternalBackdropLink> _linkCollection = [];
        private bool _disposed;

        internal static ContentBackdropManager? Create(ISystemBackdropControllerWithTargets backdropController, Compositor compositor, ElementTheme elementTheme)
        {
            var configuration = new SystemBackdropConfiguration() { Theme = (SystemBackdropTheme)elementTheme };
            backdropController.SetSystemBackdropConfiguration(configuration);

            return DesktopAcrylicController.IsSupported()
                ? new ContentBackdropManager()
                {
                    _compositor = compositor,
                    _backdropController = backdropController,
                    _configuration = configuration,
                }
                : null;
        }

        internal ContentExternalBackdropLink? CreateLink()
        {
            if (_disposed || _backdropController is null || _compositor is null)
                return null;

            var backdropLink = ContentExternalBackdropLink.Create(_compositor);
            backdropLink.ExternalBackdropBorderMode = CompositionBorderMode.Soft;
            _backdropController.AddSystemBackdropTarget(backdropLink);
            _linkCollection.Add(backdropLink);
            return backdropLink;
        }

        internal void RemoveLink(ContentExternalBackdropLink backdropLink)
        {
            if (!_linkCollection.Remove(backdropLink))
                return;

            DetachAndDisposeLink(backdropLink);
        }

        private void DetachAndDisposeLink(ContentExternalBackdropLink backdropLink)
        {
            try
            {
                _backdropController?.RemoveSystemBackdropTarget(backdropLink);
            }
            catch
            {
            }

            try
            {
                backdropLink.Dispose();
            }
            catch
            {
            }
        }

        internal void UpdateTheme(ElementTheme elementTheme)
        {
            if (_configuration is null)
                return;

            _configuration.Theme = (SystemBackdropTheme)elementTheme;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Detach only the links we own. RemoveAllSystemBackdropTargets can throw
            // during app shutdown after WinAppSDK has already torn down some targets.
            foreach (ContentExternalBackdropLink contentExternalBackdropLink in _linkCollection.ToArray())
            {
                _linkCollection.Remove(contentExternalBackdropLink);
                DetachAndDisposeLink(contentExternalBackdropLink);
            }

            try
            {
                _backdropController?.Dispose();
            }
            catch
            {
            }

            _backdropController = null;
            _configuration = null;
            _compositor = null;
        }
    }
#endif
}
