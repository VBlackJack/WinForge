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

namespace WinForge.GUI.Services;

/// <summary>
/// Service for managing navigation between views with history support.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Current navigation index.
    /// </summary>
    int CurrentIndex { get; }

    /// <summary>
    /// Whether navigation back is possible.
    /// </summary>
    bool CanGoBack { get; }

    /// <summary>
    /// Event raised when navigation state changes.
    /// </summary>
    event EventHandler? NavigationChanged;

    /// <summary>
    /// Navigates to the specified view index.
    /// </summary>
    void NavigateTo(int viewIndex);

    /// <summary>
    /// Navigates back to the previous view.
    /// </summary>
    void GoBack();

    /// <summary>
    /// Clears the navigation history.
    /// </summary>
    void ClearHistory();
}

/// <summary>
/// Implementation of navigation service with history stack.
/// </summary>
public class NavigationService : INavigationService
{
    private readonly Stack<int> _navigationHistory = new();
    private int _currentIndex;
    private bool _isNavigating;

    /// <inheritdoc/>
    public int CurrentIndex => _currentIndex;

    /// <inheritdoc/>
    public bool CanGoBack => _navigationHistory.Count > 0;

    /// <inheritdoc/>
    public event EventHandler? NavigationChanged;

    /// <inheritdoc/>
    public void NavigateTo(int viewIndex)
    {
        if (_isNavigating) return;
        if (viewIndex == _currentIndex) return;

        _isNavigating = true;
        try
        {
            // Push current view to history before navigating
            if (_currentIndex >= 0)
            {
                _navigationHistory.Push(_currentIndex);

                // Limit history size to prevent memory issues
                if (_navigationHistory.Count > 50)
                {
                    int[] temp = _navigationHistory.ToArray();
                    _navigationHistory.Clear();
                    for (int i = 0; i < 25; i++)
                    {
                        _navigationHistory.Push(temp[temp.Length - 1 - i]);
                    }
                }
            }

            _currentIndex = viewIndex;
            NavigationChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _isNavigating = false;
        }
    }

    /// <inheritdoc/>
    public void GoBack()
    {
        if (!CanGoBack) return;
        if (_isNavigating) return;

        _isNavigating = true;
        try
        {
            _currentIndex = _navigationHistory.Pop();
            NavigationChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _isNavigating = false;
        }
    }

    /// <inheritdoc/>
    public void ClearHistory()
    {
        _navigationHistory.Clear();
        NavigationChanged?.Invoke(this, EventArgs.Empty);
    }
}
