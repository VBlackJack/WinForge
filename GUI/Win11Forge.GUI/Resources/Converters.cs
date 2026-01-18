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

using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.ViewModels;

namespace Win11Forge.GUI.Resources;

/// <summary>
/// Converts boolean to Visibility (true = Visible, false = Collapsed).
/// </summary>
public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility.Visible;
    }
}

/// <summary>
/// Converts boolean to Visibility (true = Collapsed, false = Visible).
/// </summary>
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility.Collapsed;
    }
}

/// <summary>
/// Inverts a boolean value.
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }
}

/// <summary>
/// Converts nullable/empty string to Visibility (non-null/non-empty = Visible).
/// </summary>
public class NullableToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            return string.IsNullOrWhiteSpace(str) ? Visibility.Collapsed : Visibility.Visible;
        }
        return value is null ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts nullable/empty string to Visibility (null/empty = Visible, non-empty = Collapsed).
/// Inverse of NullableToVisibilityConverter.
/// </summary>
public class InverseNullableToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            return string.IsNullOrWhiteSpace(str) ? Visibility.Visible : Visibility.Collapsed;
        }
        return value is null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts boolean (IsRequired) to Material Design icon kind.
/// </summary>
public class BoolToRequiredIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? PackIconKind.Star : PackIconKind.StarOutline;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts boolean (IsRequired) to color brush.
/// </summary>
public class BoolToRequiredColorConverter : IValueConverter
{
    private static readonly SolidColorBrush RequiredBrush = new(Color.FromRgb(255, 193, 7)); // Amber
    private static readonly SolidColorBrush OptionalBrush = new(Color.FromRgb(158, 158, 158)); // Gray

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? RequiredBrush : OptionalBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts ApplicationStatus or DeploymentResult to Material Design icon kind.
/// </summary>
public class StatusToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            // ApplicationStatus
            ApplicationStatus.Pending => PackIconKind.Clock,
            ApplicationStatus.Installing => PackIconKind.ProgressDownload,
            ApplicationStatus.Installed => PackIconKind.CheckCircle,
            ApplicationStatus.Failed => PackIconKind.AlertCircle,
            ApplicationStatus.Skipped => PackIconKind.SkipNext,
            ApplicationStatus.AlreadyInstalled => PackIconKind.CheckAll,
            ApplicationStatus.Uninstalling => PackIconKind.ProgressUpload,
            ApplicationStatus.Uninstalled => PackIconKind.CheckCircleOutline,
            ApplicationStatus.UpdateAvailable => PackIconKind.Update,
            ApplicationStatus.Updating => PackIconKind.ProgressClock,
            // DeploymentResult
            DeploymentResult.Success => PackIconKind.CheckCircle,
            DeploymentResult.PartialSuccess => PackIconKind.AlertCircleCheck,
            DeploymentResult.Failed => PackIconKind.CloseCircle,
            DeploymentResult.Cancelled => PackIconKind.Cancel,
            _ => PackIconKind.HelpCircle
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts ApplicationStatus or DeploymentResult to color brush.
/// Uses consolidated color resources from App.xaml for WCAG compliance.
/// </summary>
public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Try to get brushes from App.xaml resources first, fallback to hardcoded
        var app = Application.Current;

        return value switch
        {
            // ApplicationStatus
            ApplicationStatus.Pending => app.TryFindResource("StatusPendingBrush") ?? new SolidColorBrush(Color.FromRgb(158, 158, 158)),
            ApplicationStatus.Installing => app.TryFindResource("StatusInstallingBrush") ?? new SolidColorBrush(Color.FromRgb(33, 150, 243)),
            ApplicationStatus.Installed => app.TryFindResource("StatusInstalledBrush") ?? new SolidColorBrush(Color.FromRgb(76, 175, 80)),
            ApplicationStatus.Failed => app.TryFindResource("StatusFailedBrush") ?? new SolidColorBrush(Color.FromRgb(244, 67, 54)),
            ApplicationStatus.Skipped => app.TryFindResource("StatusSkippedBrush") ?? new SolidColorBrush(Color.FromRgb(255, 152, 0)),
            ApplicationStatus.AlreadyInstalled => app.TryFindResource("StatusAlreadyInstalledBrush") ?? new SolidColorBrush(Color.FromRgb(139, 195, 74)),
            ApplicationStatus.Uninstalling => app.TryFindResource("StatusUninstallingBrush") ?? new SolidColorBrush(Color.FromRgb(156, 39, 176)),
            ApplicationStatus.Uninstalled => app.TryFindResource("StatusUninstalledBrush") ?? new SolidColorBrush(Color.FromRgb(121, 134, 203)),
            ApplicationStatus.UpdateAvailable => app.TryFindResource("SecondaryHueMidBrush") ?? new SolidColorBrush(Color.FromRgb(255, 152, 0)),
            ApplicationStatus.Updating => app.TryFindResource("StatusInstallingBrush") ?? new SolidColorBrush(Color.FromRgb(33, 150, 243)),
            // DeploymentResult
            DeploymentResult.Success => app.TryFindResource("StatusInstalledBrush") ?? new SolidColorBrush(Color.FromRgb(76, 175, 80)),
            DeploymentResult.PartialSuccess => app.TryFindResource("StatusSkippedBrush") ?? new SolidColorBrush(Color.FromRgb(255, 152, 0)),
            DeploymentResult.Failed => app.TryFindResource("StatusFailedBrush") ?? new SolidColorBrush(Color.FromRgb(244, 67, 54)),
            DeploymentResult.Cancelled => app.TryFindResource("StatusPendingBrush") ?? new SolidColorBrush(Color.FromRgb(158, 158, 158)),
            _ => app.TryFindResource("StatusPendingBrush") ?? new SolidColorBrush(Color.FromRgb(158, 158, 158))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts zero count to Visibility (0 = Visible, non-zero = Collapsed).
/// Used for empty state indicators.
/// </summary>
public class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts boolean (IsInstalled) to Material Design icon kind.
/// True = CheckCircle, False = AlertCircle.
/// </summary>
public class BoolToInstalledIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? PackIconKind.CheckCircle : PackIconKind.AlertCircle;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts boolean (IsInstalled) to color brush.
/// True = Green (Installed), False = Orange (Not Installed).
/// Uses theme-aware resources from App.xaml for proper dark/light theme support.
/// </summary>
public class BoolToInstalledColorConverter : IValueConverter
{
    private static readonly SolidColorBrush FallbackInstalledBrush = new(Color.FromRgb(76, 175, 80));
    private static readonly SolidColorBrush FallbackNotInstalledBrush = new(Color.FromRgb(255, 152, 0));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var app = Application.Current;
        if (value is true)
        {
            return app?.TryFindResource("StatusInstalledBrush") ?? FallbackInstalledBrush;
        }
        return app?.TryFindResource("StatusSkippedBrush") ?? FallbackNotInstalledBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts boolean (IsPaused) to Material Design icon kind.
/// True = Pause, False = Play (running).
/// </summary>
public class BoolToPauseIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? PackIconKind.Pause : PackIconKind.Play;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts StatusFilterOption enum to localized string.
/// </summary>
public class StatusFilterToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            StatusFilterOption.All => Resources.Apps_StatusAll,
            StatusFilterOption.Installed => Resources.Apps_StatusInstalled,
            StatusFilterOption.NotInstalled => Resources.Apps_StatusNotInstalled,
            StatusFilterOption.Selected => Resources.Apps_StatusSelected,
            StatusFilterOption.Favorites => Resources.Apps_StatusFavorites,
            StatusFilterOption.HasUpdates => Resources.Apps_StatusHasUpdates,
            _ => value?.ToString() ?? string.Empty
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts boolean (IsFavorite) to Material Design icon kind.
/// True = Star (filled), False = StarOutline.
/// </summary>
public class BoolToFavoriteIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? PackIconKind.Star : PackIconKind.StarOutline;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts boolean (IsFavorite) to color brush.
/// True = Gold, False = Gray.
/// </summary>
public class BoolToFavoriteColorConverter : IValueConverter
{
    private static readonly SolidColorBrush FavoriteBrush = new(Color.FromRgb(255, 215, 0)); // Gold
    private static readonly SolidColorBrush NotFavoriteBrush = new(Color.FromRgb(158, 158, 158)); // Gray

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? FavoriteBrush : NotFavoriteBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts ApplicationStatus to row background color brush.
/// Uses consolidated color resources from App.xaml for consistency.
/// </summary>
public class StatusToRowBackgroundConverter : IValueConverter
{
    private static readonly SolidColorBrush TransparentBrush = new(Colors.Transparent);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var app = Application.Current;

        return value switch
        {
            ApplicationStatus.Installed => app.TryFindResource("RowInstalledBackground") ?? new SolidColorBrush(Color.FromArgb(30, 76, 175, 80)),
            ApplicationStatus.AlreadyInstalled => app.TryFindResource("RowInstalledBackground") ?? new SolidColorBrush(Color.FromArgb(30, 76, 175, 80)),
            ApplicationStatus.UpdateAvailable => app.TryFindResource("RowUpdateAvailableBackground") ?? new SolidColorBrush(Color.FromArgb(40, 255, 152, 0)),
            ApplicationStatus.Updating => app.TryFindResource("RowInstallingBackground") ?? new SolidColorBrush(Color.FromArgb(30, 33, 150, 243)),
            ApplicationStatus.Failed => app.TryFindResource("RowFailedBackground") ?? new SolidColorBrush(Color.FromArgb(30, 244, 67, 54)),
            ApplicationStatus.Installing => app.TryFindResource("RowInstallingBackground") ?? new SolidColorBrush(Color.FromArgb(30, 33, 150, 243)),
            ApplicationStatus.Uninstalling => app.TryFindResource("RowInstallingBackground") ?? new SolidColorBrush(Color.FromArgb(30, 33, 150, 243)),
            ApplicationStatus.Uninstalled => app.TryFindResource("RowUninstalledBackground") ?? new SolidColorBrush(Color.FromArgb(30, 121, 134, 203)),
            _ => TransparentBrush
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts DeploymentResult enum to localized string.
/// </summary>
public class DeploymentResultToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            DeploymentResult.Success => Resources.Summary_Success,
            DeploymentResult.PartialSuccess => Resources.Summary_PartialSuccess,
            DeploymentResult.Failed => Resources.Summary_Failed,
            DeploymentResult.Cancelled => Resources.Summary_Cancelled,
            _ => string.Empty
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts non-zero count to Visibility (non-zero = Visible, 0 = Collapsed).
/// Used for showing elements when there are items needing attention.
/// </summary>
public class NonZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts non-zero count to boolean (non-zero = true, 0 = false).
/// Used for conditional styling based on count.
/// </summary>
public class NonZeroToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count > 0;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts boolean (is current phase) to check icon.
/// True (current) = Loading, False (past/future) = Check/Circle based on phase.
/// </summary>
public class BooleanToCheckIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // If it's the current active phase, show loading spinner style
        // Otherwise show checkmark for completed phases
        return value is true ? PackIconKind.CircleOutline : PackIconKind.CheckCircle;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts boolean (is current phase) to color.
/// True (current) = Primary color, False = Success or Gray.
/// </summary>
public class BooleanToPhaseColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var app = Application.Current;
        if (value is true)
        {
            return app.TryFindResource("PrimaryHueMidBrush") ?? new SolidColorBrush(Color.FromRgb(33, 150, 243));
        }
        return app.TryFindResource("StatusSuccessBrush") ?? new SolidColorBrush(Color.FromRgb(76, 175, 80));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts boolean (is current phase) to opacity.
/// True (current) = 1.0, False (completed) = 0.6.
/// </summary>
public class BooleanToPhaseOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? 1.0 : 0.6;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Returns the appropriate accent brush based on current theme.
/// Dark theme = Secondary (lime), Light theme = Primary (purple).
/// Used for theme-adaptive button styling.
/// </summary>
public class ThemeAdaptiveBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();
        var isDark = theme.GetBaseTheme() == BaseTheme.Dark;

        var app = Application.Current;
        if (isDark)
        {
            return app.TryFindResource("SecondaryHueMidBrush") ?? new SolidColorBrush(Color.FromRgb(205, 220, 57));
        }
        return app.TryFindResource("PrimaryHueMidBrush") ?? new SolidColorBrush(Color.FromRgb(103, 58, 183));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
