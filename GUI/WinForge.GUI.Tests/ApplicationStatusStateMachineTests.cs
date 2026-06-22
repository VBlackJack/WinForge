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

using WinForge.GUI.Models;
using Xunit;

namespace WinForge.GUI.Tests;

/// <summary>
/// Unit tests for ApplicationStatusStateMachine.
/// </summary>
public class ApplicationStatusStateMachineTests
{
    [Fact]
    public void Constructor_DefaultsToPending()
    {
        ApplicationStatusStateMachine sm = new ApplicationStatusStateMachine();
        Assert.Equal(ApplicationStatus.Pending, sm.CurrentStatus);
    }

    [Fact]
    public void Constructor_AcceptsInitialStatus()
    {
        ApplicationStatusStateMachine sm = new ApplicationStatusStateMachine(ApplicationStatus.Installed);
        Assert.Equal(ApplicationStatus.Installed, sm.CurrentStatus);
    }

    [Theory]
    [InlineData(ApplicationStatus.Pending, ApplicationStatus.Installing, true)]
    [InlineData(ApplicationStatus.Pending, ApplicationStatus.Skipped, true)]
    [InlineData(ApplicationStatus.Pending, ApplicationStatus.AlreadyInstalled, true)]
    [InlineData(ApplicationStatus.Pending, ApplicationStatus.Installed, false)]
    [InlineData(ApplicationStatus.Pending, ApplicationStatus.Failed, false)]
    [InlineData(ApplicationStatus.Installing, ApplicationStatus.Installed, true)]
    [InlineData(ApplicationStatus.Installing, ApplicationStatus.Failed, true)]
    [InlineData(ApplicationStatus.Installing, ApplicationStatus.Pending, false)]
    [InlineData(ApplicationStatus.Installed, ApplicationStatus.UpdateAvailable, true)]
    [InlineData(ApplicationStatus.Installed, ApplicationStatus.Uninstalling, true)]
    [InlineData(ApplicationStatus.Installed, ApplicationStatus.Pending, false)]
    [InlineData(ApplicationStatus.Failed, ApplicationStatus.Installing, true)]
    [InlineData(ApplicationStatus.Failed, ApplicationStatus.Pending, true)]
    [InlineData(ApplicationStatus.Failed, ApplicationStatus.Skipped, true)]
    public void CanTransitionTo_ValidatesCorrectly(ApplicationStatus from, ApplicationStatus to, bool expected)
    {
        ApplicationStatusStateMachine sm = new ApplicationStatusStateMachine(from);
        Assert.Equal(expected, sm.CanTransitionTo(to));
    }

    [Fact]
    public void CanTransitionTo_ReturnsFalse_ForSameState()
    {
        ApplicationStatusStateMachine sm = new ApplicationStatusStateMachine(ApplicationStatus.Pending);
        Assert.False(sm.CanTransitionTo(ApplicationStatus.Pending));
    }

    [Fact]
    public void TryTransitionTo_ReturnsTrue_ForValidTransition()
    {
        ApplicationStatusStateMachine sm = new ApplicationStatusStateMachine(ApplicationStatus.Pending);
        bool result = sm.TryTransitionTo(ApplicationStatus.Installing);

        Assert.True(result);
        Assert.Equal(ApplicationStatus.Installing, sm.CurrentStatus);
    }

    [Fact]
    public void TryTransitionTo_ReturnsFalse_ForInvalidTransition()
    {
        ApplicationStatusStateMachine sm = new ApplicationStatusStateMachine(ApplicationStatus.Pending);
        bool result = sm.TryTransitionTo(ApplicationStatus.Installed);

        Assert.False(result);
        Assert.Equal(ApplicationStatus.Pending, sm.CurrentStatus);
    }

    [Fact]
    public void TransitionTo_Throws_ForInvalidTransition()
    {
        ApplicationStatusStateMachine sm = new ApplicationStatusStateMachine(ApplicationStatus.Pending);

        Assert.Throws<InvalidOperationException>(() =>
            sm.TransitionTo(ApplicationStatus.Installed));
    }

    [Fact]
    public void TransitionTo_Succeeds_ForValidTransition()
    {
        ApplicationStatusStateMachine sm = new ApplicationStatusStateMachine(ApplicationStatus.Pending);
        sm.TransitionTo(ApplicationStatus.Installing);

        Assert.Equal(ApplicationStatus.Installing, sm.CurrentStatus);
    }

    [Fact]
    public void TransitioningFrom_EventRaised_BeforeTransition()
    {
        ApplicationStatusStateMachine sm = new ApplicationStatusStateMachine(ApplicationStatus.Pending);
        ApplicationStatus? capturedFrom = null;
        ApplicationStatus? capturedTo = null;

        sm.TransitioningFrom += (_, e) =>
        {
            capturedFrom = e.FromStatus;
            capturedTo = e.ToStatus;
        };

        sm.TryTransitionTo(ApplicationStatus.Installing);

        Assert.Equal(ApplicationStatus.Pending, capturedFrom);
        Assert.Equal(ApplicationStatus.Installing, capturedTo);
    }

    [Fact]
    public void TransitionedTo_EventRaised_AfterTransition()
    {
        ApplicationStatusStateMachine sm = new ApplicationStatusStateMachine(ApplicationStatus.Pending);
        bool eventRaised = false;
        ApplicationStatus? capturedFrom = null;
        ApplicationStatus? capturedTo = null;

        sm.TransitionedTo += (_, e) =>
        {
            eventRaised = true;
            capturedFrom = e.FromStatus;
            capturedTo = e.ToStatus;
        };

        sm.TryTransitionTo(ApplicationStatus.Installing);

        Assert.True(eventRaised);
        Assert.Equal(ApplicationStatus.Pending, capturedFrom);
        Assert.Equal(ApplicationStatus.Installing, capturedTo);
    }

    [Fact]
    public void TransitionDenied_EventRaised_ForInvalidTransition()
    {
        ApplicationStatusStateMachine sm = new ApplicationStatusStateMachine(ApplicationStatus.Pending);
        bool eventRaised = false;

        sm.TransitionDenied += (_, e) =>
        {
            eventRaised = true;
            Assert.Equal(ApplicationStatus.Pending, e.FromStatus);
            Assert.Equal(ApplicationStatus.Installed, e.ToStatus);
        };

        sm.TryTransitionTo(ApplicationStatus.Installed);

        Assert.True(eventRaised);
    }

    [Fact]
    public void GetValidTransitions_ReturnsCorrectTransitions()
    {
        ApplicationStatusStateMachine sm = new ApplicationStatusStateMachine(ApplicationStatus.Pending);
        IReadOnlyCollection<ApplicationStatus> transitions = sm.GetValidTransitions();

        Assert.Contains(ApplicationStatus.Installing, transitions);
        Assert.Contains(ApplicationStatus.Skipped, transitions);
        Assert.Contains(ApplicationStatus.AlreadyInstalled, transitions);
        Assert.DoesNotContain(ApplicationStatus.Installed, transitions);
    }

    [Fact]
    public void Reset_ChangesStatusToPending()
    {
        ApplicationStatusStateMachine sm = new ApplicationStatusStateMachine(ApplicationStatus.Installed);
        sm.Reset();

        Assert.Equal(ApplicationStatus.Pending, sm.CurrentStatus);
    }

    [Fact]
    public void Reset_ChangesStatusToSpecifiedValue()
    {
        ApplicationStatusStateMachine sm = new ApplicationStatusStateMachine(ApplicationStatus.Pending);
        sm.Reset(ApplicationStatus.AlreadyInstalled);

        Assert.Equal(ApplicationStatus.AlreadyInstalled, sm.CurrentStatus);
    }

    [Theory]
    [InlineData(ApplicationStatus.Installed, true)]
    [InlineData(ApplicationStatus.AlreadyInstalled, true)]
    [InlineData(ApplicationStatus.Uninstalled, true)]
    [InlineData(ApplicationStatus.Pending, false)]
    [InlineData(ApplicationStatus.Failed, false)]
    [InlineData(ApplicationStatus.Installing, false)]
    public void IsSuccessState_ReturnsCorrectValue(ApplicationStatus status, bool expected)
    {
        ApplicationStatusStateMachine sm = new ApplicationStatusStateMachine(status);
        Assert.Equal(expected, sm.IsSuccessState);
    }

    [Theory]
    [InlineData(ApplicationStatus.Failed, true)]
    [InlineData(ApplicationStatus.Installed, false)]
    [InlineData(ApplicationStatus.Pending, false)]
    public void IsFailureState_ReturnsCorrectValue(ApplicationStatus status, bool expected)
    {
        ApplicationStatusStateMachine sm = new ApplicationStatusStateMachine(status);
        Assert.Equal(expected, sm.IsFailureState);
    }

    [Theory]
    [InlineData(ApplicationStatus.Installing, true)]
    [InlineData(ApplicationStatus.Uninstalling, true)]
    [InlineData(ApplicationStatus.Updating, true)]
    [InlineData(ApplicationStatus.Pending, false)]
    [InlineData(ApplicationStatus.Installed, false)]
    public void IsInProgress_ReturnsCorrectValue(ApplicationStatus status, bool expected)
    {
        ApplicationStatusStateMachine sm = new ApplicationStatusStateMachine(status);
        Assert.Equal(expected, sm.IsInProgress);
    }

    [Theory]
    [InlineData(ApplicationStatus.Failed, true)]
    [InlineData(ApplicationStatus.Skipped, true)]
    [InlineData(ApplicationStatus.Installed, false)]
    [InlineData(ApplicationStatus.Pending, false)]
    public void CanRetry_ReturnsCorrectValue(ApplicationStatus status, bool expected)
    {
        ApplicationStatusStateMachine sm = new ApplicationStatusStateMachine(status);
        Assert.Equal(expected, sm.CanRetry);
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        ApplicationStatusStateMachine original = new ApplicationStatusStateMachine(ApplicationStatus.Installing);
        ApplicationStatusStateMachine clone = original.Clone();

        clone.TryTransitionTo(ApplicationStatus.Installed);

        Assert.Equal(ApplicationStatus.Installing, original.CurrentStatus);
        Assert.Equal(ApplicationStatus.Installed, clone.CurrentStatus);
    }

    [Fact]
    public void FullInstallationWorkflow_Succeeds()
    {
        ApplicationStatusStateMachine sm = new ApplicationStatusStateMachine();

        // Pending -> Installing
        Assert.True(sm.TryTransitionTo(ApplicationStatus.Installing));
        Assert.Equal(ApplicationStatus.Installing, sm.CurrentStatus);

        // Installing -> Installed
        Assert.True(sm.TryTransitionTo(ApplicationStatus.Installed));
        Assert.Equal(ApplicationStatus.Installed, sm.CurrentStatus);

        Assert.True(sm.IsSuccessState);
    }

    [Fact]
    public void FailAndRetryWorkflow_Succeeds()
    {
        ApplicationStatusStateMachine sm = new ApplicationStatusStateMachine();

        // Pending -> Installing
        sm.TryTransitionTo(ApplicationStatus.Installing);

        // Installing -> Failed
        sm.TryTransitionTo(ApplicationStatus.Failed);
        Assert.True(sm.IsFailureState);
        Assert.True(sm.CanRetry);

        // Failed -> Installing (retry)
        Assert.True(sm.TryTransitionTo(ApplicationStatus.Installing));

        // Installing -> Installed
        sm.TryTransitionTo(ApplicationStatus.Installed);
        Assert.True(sm.IsSuccessState);
    }

    [Fact]
    public void UpdateWorkflow_Succeeds()
    {
        ApplicationStatusStateMachine sm = new ApplicationStatusStateMachine(ApplicationStatus.Installed);

        // Installed -> UpdateAvailable
        Assert.True(sm.TryTransitionTo(ApplicationStatus.UpdateAvailable));

        // UpdateAvailable -> Updating
        Assert.True(sm.TryTransitionTo(ApplicationStatus.Updating));
        Assert.True(sm.IsInProgress);

        // Updating -> Installed
        Assert.True(sm.TryTransitionTo(ApplicationStatus.Installed));
        Assert.True(sm.IsSuccessState);
    }

    [Fact]
    public void UninstallWorkflow_Succeeds()
    {
        ApplicationStatusStateMachine sm = new ApplicationStatusStateMachine(ApplicationStatus.Installed);

        // Installed -> Uninstalling
        Assert.True(sm.TryTransitionTo(ApplicationStatus.Uninstalling));
        Assert.True(sm.IsInProgress);

        // Uninstalling -> Uninstalled
        Assert.True(sm.TryTransitionTo(ApplicationStatus.Uninstalled));
        Assert.True(sm.IsSuccessState);

        // Uninstalled -> Pending (reinstall)
        Assert.True(sm.TryTransitionTo(ApplicationStatus.Pending));
    }

    [Fact]
    public void ToString_ReturnsCurrentStatus()
    {
        ApplicationStatusStateMachine sm = new ApplicationStatusStateMachine(ApplicationStatus.Installing);
        Assert.Equal("Installing", sm.ToString());
    }
}
