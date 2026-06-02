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

#nullable enable

namespace Win11Forge.GUI.Models;

/// <summary>
/// State machine for managing ApplicationStatus transitions.
/// Ensures only valid state transitions are allowed and provides
/// a centralized point for state change logic.
/// </summary>
public sealed class ApplicationStatusStateMachine
{
    /// <summary>
    /// Defines valid state transitions for ApplicationStatus.
    /// Key = current state, Value = allowed next states.
    /// </summary>
    private static readonly Dictionary<ApplicationStatus, HashSet<ApplicationStatus>> ValidTransitions = new()
    {
        [ApplicationStatus.Pending] = new HashSet<ApplicationStatus>
        {
            ApplicationStatus.Installing,
            ApplicationStatus.Skipped,
            ApplicationStatus.AlreadyInstalled
        },

        [ApplicationStatus.Installing] = new HashSet<ApplicationStatus>
        {
            ApplicationStatus.Installed,
            ApplicationStatus.Failed,
            ApplicationStatus.Skipped  // Can be skipped if cancelled mid-installation
        },

        [ApplicationStatus.Installed] = new HashSet<ApplicationStatus>
        {
            ApplicationStatus.UpdateAvailable,
            ApplicationStatus.Uninstalling
        },

        [ApplicationStatus.Failed] = new HashSet<ApplicationStatus>
        {
            ApplicationStatus.Installing,  // Retry
            ApplicationStatus.Pending,     // Reset for retry
            ApplicationStatus.Skipped      // Give up
        },

        [ApplicationStatus.Skipped] = new HashSet<ApplicationStatus>
        {
            ApplicationStatus.Pending  // Reset if user wants to try again
        },

        [ApplicationStatus.AlreadyInstalled] = new HashSet<ApplicationStatus>
        {
            ApplicationStatus.UpdateAvailable,
            ApplicationStatus.Uninstalling
        },

        [ApplicationStatus.Uninstalling] = new HashSet<ApplicationStatus>
        {
            ApplicationStatus.Uninstalled,
            ApplicationStatus.Failed,
            ApplicationStatus.Installed  // Uninstall failed, still installed
        },

        [ApplicationStatus.Uninstalled] = new HashSet<ApplicationStatus>
        {
            ApplicationStatus.Pending  // Reinstall
        },

        [ApplicationStatus.UpdateAvailable] = new HashSet<ApplicationStatus>
        {
            ApplicationStatus.Updating,
            ApplicationStatus.Installed,  // User dismisses update
            ApplicationStatus.Uninstalling
        },

        [ApplicationStatus.Updating] = new HashSet<ApplicationStatus>
        {
            ApplicationStatus.Installed,
            ApplicationStatus.Failed
        }
    };

    private ApplicationStatus _currentStatus;

    /// <summary>
    /// Creates a new state machine with the specified initial status.
    /// </summary>
    /// <param name="initialStatus">The initial status (defaults to Pending).</param>
    public ApplicationStatusStateMachine(ApplicationStatus initialStatus = ApplicationStatus.Pending)
    {
        _currentStatus = initialStatus;
    }

    /// <summary>
    /// Gets the current status.
    /// </summary>
    public ApplicationStatus CurrentStatus => _currentStatus;

    /// <summary>
    /// Event raised before a state transition occurs.
    /// </summary>
    public event EventHandler<StateTransitionEventArgs>? TransitioningFrom;

    /// <summary>
    /// Event raised after a state transition completes.
    /// </summary>
    public event EventHandler<StateTransitionEventArgs>? TransitionedTo;

    /// <summary>
    /// Event raised when a transition is denied.
    /// </summary>
    public event EventHandler<StateTransitionDeniedEventArgs>? TransitionDenied;

    /// <summary>
    /// Checks if a transition to the specified status is valid.
    /// </summary>
    /// <param name="newStatus">The target status.</param>
    /// <returns>True if the transition is valid.</returns>
    public bool CanTransitionTo(ApplicationStatus newStatus)
    {
        if (newStatus == _currentStatus)
            return false;

        return ValidTransitions.TryGetValue(_currentStatus, out HashSet<ApplicationStatus>? validTargets)
               && validTargets.Contains(newStatus);
    }

    /// <summary>
    /// Gets the valid target states from the current state.
    /// </summary>
    /// <returns>Collection of valid target states.</returns>
    public IReadOnlyCollection<ApplicationStatus> GetValidTransitions()
    {
        return ValidTransitions.TryGetValue(_currentStatus, out HashSet<ApplicationStatus>? validTargets)
            ? validTargets.ToList().AsReadOnly()
            : Array.Empty<ApplicationStatus>();
    }

    /// <summary>
    /// Attempts to transition to a new status.
    /// </summary>
    /// <param name="newStatus">The target status.</param>
    /// <param name="reason">Optional reason for the transition.</param>
    /// <returns>True if the transition succeeded.</returns>
    public bool TryTransitionTo(ApplicationStatus newStatus, string? reason = null)
    {
        if (!CanTransitionTo(newStatus))
        {
            TransitionDenied?.Invoke(this, new StateTransitionDeniedEventArgs(
                _currentStatus, newStatus, $"Transition from {_currentStatus} to {newStatus} is not allowed"));
            return false;
        }

        ApplicationStatus previousStatus = _currentStatus;

        // Raise pre-transition event
        TransitioningFrom?.Invoke(this, new StateTransitionEventArgs(previousStatus, newStatus, reason));

        // Perform transition
        _currentStatus = newStatus;

        // Raise post-transition event
        TransitionedTo?.Invoke(this, new StateTransitionEventArgs(previousStatus, newStatus, reason));

        return true;
    }

    /// <summary>
    /// Transitions to a new status, throwing if the transition is invalid.
    /// </summary>
    /// <param name="newStatus">The target status.</param>
    /// <param name="reason">Optional reason for the transition.</param>
    /// <exception cref="InvalidOperationException">Thrown if the transition is invalid.</exception>
    public void TransitionTo(ApplicationStatus newStatus, string? reason = null)
    {
        if (!TryTransitionTo(newStatus, reason))
        {
            throw new InvalidOperationException(
                $"Invalid state transition: Cannot transition from {_currentStatus} to {newStatus}. " +
                $"Valid transitions from {_currentStatus}: {string.Join(", ", GetValidTransitions())}");
        }
    }

    /// <summary>
    /// Resets the state machine to the initial state.
    /// </summary>
    /// <param name="status">The status to reset to (defaults to Pending).</param>
    public void Reset(ApplicationStatus status = ApplicationStatus.Pending)
    {
        ApplicationStatus previousStatus = _currentStatus;
        _currentStatus = status;
        TransitionedTo?.Invoke(this, new StateTransitionEventArgs(previousStatus, status, "Reset"));
    }

    /// <summary>
    /// Checks if the current state is a terminal state (no valid transitions).
    /// </summary>
    public bool IsTerminalState =>
        !ValidTransitions.TryGetValue(_currentStatus, out HashSet<ApplicationStatus>? transitions) || transitions.Count == 0;

    /// <summary>
    /// Checks if the current state indicates success.
    /// </summary>
    public bool IsSuccessState =>
        _currentStatus == ApplicationStatus.Installed ||
        _currentStatus == ApplicationStatus.AlreadyInstalled ||
        _currentStatus == ApplicationStatus.Uninstalled;

    /// <summary>
    /// Checks if the current state indicates failure.
    /// </summary>
    public bool IsFailureState => _currentStatus == ApplicationStatus.Failed;

    /// <summary>
    /// Checks if the current state indicates an operation is in progress.
    /// </summary>
    public bool IsInProgress =>
        _currentStatus == ApplicationStatus.Installing ||
        _currentStatus == ApplicationStatus.Uninstalling ||
        _currentStatus == ApplicationStatus.Updating;

    /// <summary>
    /// Gets whether this state can be retried.
    /// </summary>
    public bool CanRetry =>
        _currentStatus == ApplicationStatus.Failed ||
        _currentStatus == ApplicationStatus.Skipped;

    /// <summary>
    /// Creates a copy of this state machine.
    /// </summary>
    public ApplicationStatusStateMachine Clone()
    {
        return new ApplicationStatusStateMachine(_currentStatus);
    }

    public override string ToString() => _currentStatus.ToString();
}

/// <summary>
/// Event arguments for state transitions.
/// </summary>
public sealed class StateTransitionEventArgs : EventArgs
{
    public StateTransitionEventArgs(ApplicationStatus fromStatus, ApplicationStatus toStatus, string? reason = null)
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Reason = reason;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// The status before the transition.
    /// </summary>
    public ApplicationStatus FromStatus { get; }

    /// <summary>
    /// The status after the transition.
    /// </summary>
    public ApplicationStatus ToStatus { get; }

    /// <summary>
    /// Optional reason for the transition.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Timestamp when the transition occurred.
    /// </summary>
    public DateTime Timestamp { get; }
}

/// <summary>
/// Event arguments for denied state transitions.
/// </summary>
public sealed class StateTransitionDeniedEventArgs : EventArgs
{
    public StateTransitionDeniedEventArgs(ApplicationStatus fromStatus, ApplicationStatus toStatus, string reason)
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Reason = reason;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// The current status.
    /// </summary>
    public ApplicationStatus FromStatus { get; }

    /// <summary>
    /// The attempted target status.
    /// </summary>
    public ApplicationStatus ToStatus { get; }

    /// <summary>
    /// Reason for denial.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Timestamp when the denial occurred.
    /// </summary>
    public DateTime Timestamp { get; }
}
