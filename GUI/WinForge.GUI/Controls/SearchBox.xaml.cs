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

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace Win11Forge.GUI.Controls;

/// <summary>
/// Represents a search suggestion item.
/// </summary>
public class SearchSuggestion
{
    public string Text { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Shortcut { get; set; }
    public SymbolRegular Icon { get; set; } = SymbolRegular.Search24;
    public bool IsRecent { get; set; }
}

/// <summary>
/// Advanced search box with auto-complete and recent searches.
/// </summary>
public partial class SearchBox : UserControl
{
    private const int MaxRecentSearches = 5;
    private const int MaxSuggestions = 8;

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(SearchBox),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

    public static readonly DependencyProperty SuggestionsSourceProperty =
        DependencyProperty.Register(nameof(SuggestionsSource), typeof(IEnumerable<string>), typeof(SearchBox),
            new PropertyMetadata(null));

    public static readonly DependencyProperty HasRecentSearchesProperty =
        DependencyProperty.Register(nameof(HasRecentSearches), typeof(bool), typeof(SearchBox),
            new PropertyMetadata(false));

    private readonly ObservableCollection<SearchSuggestion> _suggestions = [];
    private readonly List<string> _recentSearches = [];
    private bool _suppressTextChanged;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public IEnumerable<string>? SuggestionsSource
    {
        get => (IEnumerable<string>?)GetValue(SuggestionsSourceProperty);
        set => SetValue(SuggestionsSourceProperty, value);
    }

    public bool HasRecentSearches
    {
        get => (bool)GetValue(HasRecentSearchesProperty);
        set => SetValue(HasRecentSearchesProperty, value);
    }

    public event EventHandler<string>? SearchSubmitted;

    public SearchBox()
    {
        InitializeComponent();
        SuggestionsList.ItemsSource = _suggestions;
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SearchBox searchBox && !searchBox._suppressTextChanged)
        {
            searchBox.SearchTextBox.Text = e.NewValue as string ?? string.Empty;
        }
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressTextChanged) return;

        _suppressTextChanged = true;
        Text = SearchTextBox.Text;
        _suppressTextChanged = false;

        UpdateSuggestions();
    }

    private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                if (!SuggestionsPopup.IsOpen)
                {
                    UpdateSuggestions();
                }

                if (SuggestionsPopup.IsOpen && SuggestionsList.Items.Count > 0)
                {
                    if (SuggestionsList.SelectedIndex < 0)
                    {
                        SuggestionsList.SelectedIndex = 0;
                    }
                    else
                    {
                        SuggestionsList.SelectedIndex = Math.Min(SuggestionsList.SelectedIndex + 1, SuggestionsList.Items.Count - 1);
                    }
                    e.Handled = true;
                }
                break;

            case Key.Up:
                if (SuggestionsPopup.IsOpen && SuggestionsList.Items.Count > 0)
                {
                    if (SuggestionsList.SelectedIndex <= 0)
                    {
                        SuggestionsList.SelectedIndex = 0;
                    }
                    else
                    {
                        SuggestionsList.SelectedIndex--;
                    }
                    e.Handled = true;
                }
                break;

            case Key.Enter:
                if (SuggestionsPopup.IsOpen && SuggestionsList.SelectedItem is SearchSuggestion suggestion)
                {
                    SelectSuggestion(suggestion);
                }
                else if (!string.IsNullOrWhiteSpace(SearchTextBox.Text))
                {
                    AddToRecentSearches(SearchTextBox.Text);
                    SearchSubmitted?.Invoke(this, SearchTextBox.Text);
                }
                SuggestionsPopup.IsOpen = false;
                e.Handled = true;
                break;

            case Key.Escape:
                SuggestionsPopup.IsOpen = false;
                e.Handled = true;
                break;
        }
    }

    private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(SearchTextBox.Text) && _recentSearches.Count > 0)
        {
            ShowRecentSearches();
        }
    }

    private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        // Delay closing to allow click on suggestion
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!SuggestionsList.IsKeyboardFocusWithin)
            {
                SuggestionsPopup.IsOpen = false;
            }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void SuggestionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Keyboard arrows should only move focus in the suggestions list.
        // Selection confirmation happens on Enter or explicit mouse click.
    }

    private void SuggestionsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        // Click anywhere in a row to confirm the currently selected suggestion.
        ListBoxItem? container = ItemsControl.ContainerFromElement(SuggestionsList, source) as ListBoxItem;
        if (container?.DataContext is SearchSuggestion suggestion && !string.IsNullOrEmpty(suggestion.Text))
        {
            SelectSuggestion(suggestion);
            e.Handled = true;
        }
    }

    private void SelectSuggestion(SearchSuggestion suggestion)
    {
        _suppressTextChanged = true;
        SearchTextBox.Text = suggestion.Text;
        Text = suggestion.Text;
        _suppressTextChanged = false;

        SearchTextBox.CaretIndex = SearchTextBox.Text.Length;
        SuggestionsPopup.IsOpen = false;

        if (!suggestion.IsRecent)
        {
            AddToRecentSearches(suggestion.Text);
        }

        SearchSubmitted?.Invoke(this, suggestion.Text);
    }

    private void UpdateSuggestions()
    {
        _suggestions.Clear();

        string searchText = SearchTextBox.Text?.ToLowerInvariant() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(searchText))
        {
            if (_recentSearches.Count > 0)
            {
                ShowRecentSearches();
            }
            else
            {
                SuggestionsPopup.IsOpen = false;
            }
            return;
        }

        if (SuggestionsSource == null)
        {
            SuggestionsPopup.IsOpen = false;
            return;
        }

        IEnumerable<SearchSuggestion> matches = SuggestionsSource
            .Where(s => s.ToLowerInvariant().Contains(searchText))
            .Take(MaxSuggestions)
            .Select(s => new SearchSuggestion
            {
                Text = s,
                Icon = SymbolRegular.Apps24
            });

        foreach (SearchSuggestion? match in matches)
        {
            _suggestions.Add(match);
        }

        HasRecentSearches = false;
        SuggestionsPopup.IsOpen = _suggestions.Count > 0;
    }

    private void ShowRecentSearches()
    {
        _suggestions.Clear();

        foreach (string? recent in _recentSearches.Take(MaxRecentSearches))
        {
            _suggestions.Add(new SearchSuggestion
            {
                Text = recent,
                Icon = SymbolRegular.History24,
                IsRecent = true
            });
        }

        HasRecentSearches = _suggestions.Count > 0;
        SuggestionsPopup.IsOpen = _suggestions.Count > 0;
    }

    private void AddToRecentSearches(string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return;

        _recentSearches.Remove(search);
        _recentSearches.Insert(0, search);

        while (_recentSearches.Count > MaxRecentSearches)
        {
            _recentSearches.RemoveAt(_recentSearches.Count - 1);
        }
    }

    /// <summary>
    /// Clears all recent searches.
    /// </summary>
    public void ClearRecentSearches()
    {
        _recentSearches.Clear();
        HasRecentSearches = false;
    }
}
