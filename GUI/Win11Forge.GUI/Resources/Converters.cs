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
/// </summary>
public class StatusToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush PendingBrush = new(Color.FromRgb(158, 158, 158)); // Gray
    private static readonly SolidColorBrush InstallingBrush = new(Color.FromRgb(33, 150, 243)); // Blue
    private static readonly SolidColorBrush InstalledBrush = new(Color.FromRgb(76, 175, 80)); // Green
    private static readonly SolidColorBrush FailedBrush = new(Color.FromRgb(244, 67, 54)); // Red
    private static readonly SolidColorBrush SkippedBrush = new(Color.FromRgb(255, 152, 0)); // Orange
    private static readonly SolidColorBrush AlreadyInstalledBrush = new(Color.FromRgb(139, 195, 74)); // Light Green

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            // ApplicationStatus
            ApplicationStatus.Pending => PendingBrush,
            ApplicationStatus.Installing => InstallingBrush,
            ApplicationStatus.Installed => InstalledBrush,
            ApplicationStatus.Failed => FailedBrush,
            ApplicationStatus.Skipped => SkippedBrush,
            ApplicationStatus.AlreadyInstalled => AlreadyInstalledBrush,
            // DeploymentResult
            DeploymentResult.Success => InstalledBrush,
            DeploymentResult.PartialSuccess => SkippedBrush,
            DeploymentResult.Failed => FailedBrush,
            DeploymentResult.Cancelled => PendingBrush,
            _ => PendingBrush
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
/// True = Green, False = Orange.
/// </summary>
public class BoolToInstalledColorConverter : IValueConverter
{
    private static readonly SolidColorBrush InstalledBrush = new(Color.FromRgb(76, 175, 80)); // Green
    private static readonly SolidColorBrush NotInstalledBrush = new(Color.FromRgb(255, 152, 0)); // Orange

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? InstalledBrush : NotInstalledBrush;
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
