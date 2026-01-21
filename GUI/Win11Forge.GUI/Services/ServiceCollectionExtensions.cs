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

using Microsoft.Extensions.DependencyInjection;
using Win11Forge.GUI.ViewModels;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Extension methods for configuring dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Win11Forge services to the service collection.
    /// </summary>
    public static IServiceCollection AddWin11ForgeServices(this IServiceCollection services)
    {
        // Register detection service (used by PowerShellBridge)
        services.AddSingleton<IApplicationDetectionService, HybridDetectionService>();

        // Register services as singletons (shared state across the application)
        // PowerShellBridge is registered as the composite interface and its focused interfaces
        // This follows ISP - consumers can depend on the smallest interface they need
        services.AddSingleton<PowerShellBridge>();
        services.AddSingleton<IPowerShellBridge>(sp => sp.GetRequiredService<PowerShellBridge>());
        services.AddSingleton<IVersionService>(sp => sp.GetRequiredService<PowerShellBridge>());
        services.AddSingleton<IProfileManagementService>(sp => sp.GetRequiredService<PowerShellBridge>());
        services.AddSingleton<IApplicationManagementService>(sp => sp.GetRequiredService<PowerShellBridge>());
        services.AddSingleton<ISystemInfoService>(sp => sp.GetRequiredService<PowerShellBridge>());
        services.AddSingleton<IDeploymentHistoryService, DeploymentHistoryService>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IProfileExportService, ProfileExportService>();
        services.AddSingleton<IDeploymentStateService, DeploymentStateService>();
        services.AddSingleton<IUndoService, UndoService>();
        services.AddSingleton<IValidationService, ValidationService>();
        services.AddSingleton<IProfileValidationService, ProfileValidationService>();
        services.AddSingleton<IProfileBridge, ProfileBridge>();
        services.AddSingleton<IApplicationBridge, ApplicationBridge>();
        services.AddSingleton<IPrerequisitesService, PrerequisitesService>();
        services.AddSingleton<ToastService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IAccessibilityService, AccessibilityService>();

        // Register ViewModels as transient (new instance per request)
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<DeploymentViewModel>();
        services.AddTransient<AppsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<PrerequisitesViewModel>();

        return services;
    }
}
