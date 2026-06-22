/*
 * Copyright 2026 Julien Bombled
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;

namespace WinForge.GUI.Tests.TestInfrastructure;

/// <summary>
/// Test-only WPF application scope for behavioral tests that need <see cref="Application.Current"/>.
/// </summary>
internal sealed class WpfApplicationScope : IDisposable
{
    private static readonly FieldInfo AppCreatedInThisAppDomainField =
        GetApplicationField("_appCreatedInThisAppDomain");

    private static readonly FieldInfo AppInstanceField =
        GetApplicationField("_appInstance");

    private static readonly FieldInfo IsShuttingDownField =
        GetApplicationField("_isShuttingDown");

    private readonly Application _application;
    private readonly ResourceDictionary _originalResources;
    private bool _disposed;

    private WpfApplicationScope(Application application)
    {
        _application = application;
        _originalResources = application.Resources;
        _application.Resources = CreateTestResources();
    }

    /// <summary>
    /// Gets the scoped application instance.
    /// </summary>
    public Application Application => _application;

    /// <summary>
    /// Runs an action on a dedicated STA thread and rethrows any captured exception.
    /// </summary>
    /// <param name="action">Action to run on the STA thread.</param>
    public static void RunOnStaThread(Action action)
    {
        ExceptionDispatchInfo? exception = null;
        Thread thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ExceptionDispatchInfo.Capture(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        exception?.Throw();
    }

    /// <summary>
    /// Creates a scope around the current WPF application or a new test application.
    /// </summary>
    /// <returns>A disposable application scope.</returns>
    public static WpfApplicationScope Create()
    {
        Application? app = Application.Current;

        if (app is not null && app.Dispatcher != Dispatcher.CurrentDispatcher)
        {
            ShutdownApplicationFromOwningDispatcher(app);
            ResetApplicationSingletonState();
            app = null;
        }

        app ??= new Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };

        return new WpfApplicationScope(app);
    }

    /// <summary>
    /// Adds a high contrast resource dictionary marker matching production detection.
    /// </summary>
    public void AddHighContrastDictionaryMarker()
    {
        AddMergedDictionaryMarker(
            "pack://application:,,,/WinForge.GUI;component/Resources/HighContrastTheme.xaml");
    }

    /// <summary>
    /// Adds a merged dictionary with the provided source URI.
    /// </summary>
    /// <param name="source">Dictionary source URI.</param>
    public void AddMergedDictionaryMarker(string source)
    {
        _application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(source, UriKind.Absolute)
        });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (_application.Dispatcher == Dispatcher.CurrentDispatcher)
            {
                RestoreResourcesAndShutdown();
            }
            else
            {
                ShutdownApplicationFromOwningDispatcher(_application);
            }
        }
        finally
        {
            // WPF keeps Application singleton state after Shutdown; reset it so legacy
            // tests can still exercise paths where Application.Current is null.
            ResetApplicationSingletonState();
            _disposed = true;
        }
    }

    private void RestoreResourcesAndShutdown()
    {
        _application.Resources = _originalResources;
        _application.Shutdown();
    }

    private static ResourceDictionary CreateTestResources()
    {
        return new ResourceDictionary
        {
            ["FontSizeMicro"] = 11.0,
            ["FontSizeCaption"] = 12.0,
            ["FontSizeBody"] = 14.0,
            ["FontSizeBodyLarge"] = 16.0,
            ["FontSizeLarge"] = 18.0,
            ["FontSizeSubtitle"] = 20.0,
            ["FontSizeTitle"] = 24.0,
            ["FontSizeHeader"] = 28.0,
            ["FontSizeDisplay"] = 32.0,
            ["FontSizeHero"] = 48.0,
            ["RadiusSmall"] = new CornerRadius(4),
            ["RadiusMedium"] = new CornerRadius(8),
            ["RadiusLarge"] = new CornerRadius(12),
            ["RadiusXLarge"] = new CornerRadius(16)
        };
    }

    private static void ShutdownApplicationFromOwningDispatcher(Application app)
    {
        _ = app;

        // A previous WPF test can leave an Application instance owned by another
        // STA thread whose dispatcher is no longer pumping. Synchronously invoking
        // that dispatcher can hang the suite; resetting the singleton fields is
        // enough for the next scoped test application.
    }

    private static void ResetApplicationSingletonState()
    {
        AppInstanceField.SetValue(null, null);
        IsShuttingDownField.SetValue(null, false);
        AppCreatedInThisAppDomainField.SetValue(null, false);
    }

    private static FieldInfo GetApplicationField(string name)
    {
        return typeof(Application).GetField(
            name,
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(Application).FullName, name);
    }
}
