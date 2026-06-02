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
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests verifying the ISP-compliant focused interfaces are properly designed.
/// </summary>
public class FocusedInterfacesTests
{
    /// <summary>
    /// IVersionService should have exactly the version-related method.
    /// </summary>
    [Fact]
    public void IVersionService_Should_Have_Single_Responsibility()
    {
        // Arrange
        Type interfaceType = typeof(IVersionService);

        // Act
        List<MethodInfo> methods = interfaceType.GetMethods()
            .Where(m => !m.IsSpecialName) // Exclude property accessors
            .ToList();

        // Assert
        Assert.Single(methods);
        Assert.Equal("GetWin11ForgeVersionAsync", methods[0].Name);
    }

    /// <summary>
    /// IProfileManagementService should have only profile-related methods.
    /// </summary>
    [Fact]
    public void IProfileManagementService_Should_Have_Profile_Methods_Only()
    {
        // Arrange
        Type interfaceType = typeof(IProfileManagementService);

        // Act
        List<string> methods = interfaceType.GetMethods()
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .ToList();

        // Assert - Should have profile management methods
        Assert.Contains("GetAvailableProfilesAsync", methods);
        Assert.Contains("LoadProfileAsync", methods);
        Assert.Contains("GetRawProfileAsync", methods);
        Assert.Contains("GetResolvedProfileAsync", methods);
        Assert.Contains("SaveProfileAsync", methods);
        Assert.Equal(5, methods.Count);
    }

    /// <summary>
    /// IApplicationManagementService should have only app-related methods.
    /// </summary>
    [Fact]
    public void IApplicationManagementService_Should_Have_App_Methods_Only()
    {
        // Arrange
        Type interfaceType = typeof(IApplicationManagementService);

        // Act
        List<string> methods = interfaceType.GetMethods()
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .ToList();

        // Assert - Should have app lifecycle methods
        Assert.Contains("GetAllApplicationsAsync", methods);
        Assert.Contains("GetApplicationStatusAsync", methods);
        Assert.Contains("GetBatchApplicationStatusAsync", methods);
        Assert.Contains("InstallApplicationAsync", methods);
        Assert.Contains("UninstallApplicationAsync", methods);
        Assert.Contains("CheckApplicationUpdateAsync", methods);
        Assert.Contains("UpdateApplicationAsync", methods);
        Assert.Contains("LaunchApplicationAsync", methods);
        Assert.Equal(8, methods.Count);
    }

    /// <summary>
    /// IApplicationBridge should not expose the unused legacy batch installation API.
    /// </summary>
    [Fact]
    public void IApplicationBridge_Should_Not_Expose_Unused_BatchInstall_Api()
    {
        // Arrange
        Type interfaceType = typeof(IApplicationBridge);

        // Act
        List<string> methods = interfaceType.GetMethods()
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .ToList();

        List<string> serviceTypeNames = interfaceType.Assembly.GetTypes()
            .Select(t => t.Name)
            .ToList();

        // Assert
        Assert.DoesNotContain("InstallApplicationsAsync", methods);
        Assert.DoesNotContain("BatchInstallResult", serviceTypeNames);
        Assert.DoesNotContain("BatchInstallProgress", serviceTypeNames);
    }

    /// <summary>
    /// ISystemInfoService should have system info related members.
    /// </summary>
    [Fact]
    public void ISystemInfoService_Should_Have_SystemInfo_Members()
    {
        // Arrange
        Type interfaceType = typeof(ISystemInfoService);

        // Act
        List<string> methods = interfaceType.GetMethods()
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .ToList();

        List<string> properties = interfaceType.GetProperties()
            .Select(p => p.Name)
            .ToList();

        // Assert
        Assert.Single(methods);
        Assert.Equal("GetSystemInfoAsync", methods[0]);
        Assert.Single(properties);
        Assert.Equal("RepositoryRoot", properties[0]);
    }

    /// <summary>
    /// IPowerShellBridge should inherit from all focused interfaces.
    /// </summary>
    [Fact]
    public void IPowerShellBridge_Should_Inherit_From_All_Focused_Interfaces()
    {
        // Arrange
        Type compositeType = typeof(IPowerShellBridge);

        // Act
        Type[] implementedInterfaces = compositeType.GetInterfaces();

        // Assert
        Assert.Contains(typeof(IVersionService), implementedInterfaces);
        Assert.Contains(typeof(IProfileManagementService), implementedInterfaces);
        Assert.Contains(typeof(IApplicationManagementService), implementedInterfaces);
        Assert.Contains(typeof(ISystemInfoService), implementedInterfaces);
    }

    /// <summary>
    /// IPowerShellBridge should still have prerequisites methods directly.
    /// </summary>
    [Fact]
    public void IPowerShellBridge_Should_Have_Prerequisites_Methods()
    {
        // Arrange
        Type interfaceType = typeof(IPowerShellBridge);

        // Act
        List<string> methods = interfaceType.GetMethods()
            .Where(m => !m.IsSpecialName && m.DeclaringType == interfaceType)
            .Select(m => m.Name)
            .ToList();

        // Assert - Direct methods on IPowerShellBridge (not inherited)
        Assert.Contains("CheckPrerequisitesAsync", methods);
        Assert.Contains("InstallPrerequisitesAsync", methods);
    }

    /// <summary>
    /// Interface Segregation: a class needing only version info can depend only on IVersionService.
    /// </summary>
    [Fact]
    public void ISP_Compliance_Version_Only_Dependency()
    {
        // This test documents that consumers can depend on just IVersionService
        // instead of the full IPowerShellBridge

        // Arrange - A hypothetical consumer
        Type versionServiceType = typeof(IVersionService);

        // Assert - The interface is small and focused
        int memberCount = versionServiceType.GetMembers()
            .Where(m => m.MemberType == System.Reflection.MemberTypes.Method)
            .Count();

        Assert.True(memberCount <= 2, "IVersionService should have minimal members for ISP compliance");
    }

    /// <summary>
    /// All focused interfaces should be public.
    /// </summary>
    [Fact]
    public void All_Focused_Interfaces_Should_Be_Public()
    {
        // Arrange
        Type[] interfaces = new[]
        {
            typeof(IVersionService),
            typeof(IProfileManagementService),
            typeof(IApplicationManagementService),
            typeof(ISystemInfoService)
        };

        // Assert
        Assert.All(interfaces, t => Assert.True(t.IsPublic, $"{t.Name} should be public"));
    }
}
