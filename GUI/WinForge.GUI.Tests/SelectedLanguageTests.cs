// Copyright 2026 Julien Bombled. Licensed under the Apache License, Version 2.0.
using System.Globalization;
using WinForge.GUI.Resources;
using ResourceStrings = WinForge.GUI.Resources.Resources;

namespace WinForge.GUI.Tests;

[Collection("WpfApplication")]
public sealed class SelectedLanguageTests
{
    [Theory]
    [InlineData("en", "fr-FR")]
    [InlineData("fr", "en-US")]
    public async Task LazyResourceLookupUsesSelectedLanguageAcrossAwait(string selected, string ambient)
    {
        CultureInfo? previous = ResourceStrings.Culture;
        CultureInfo previousUi = CultureInfo.CurrentUICulture;
        try
        {
            ResourceStrings.Culture = CultureInfo.GetCultureInfo(selected);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(ambient);
            await Task.Yield();
            Assert.Equal(ResourceStrings.Settings_Title, LocalizationProvider.Instance["Settings_Title"]);
            Assert.Equal("[MissingAuditKey]", LocalizationProvider.Instance["MissingAuditKey"]);
        }
        finally
        {
            ResourceStrings.Culture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }
}
