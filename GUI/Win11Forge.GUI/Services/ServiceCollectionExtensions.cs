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
        // Register services as singletons (shared state across the application)
        services.AddSingleton<IPowerShellBridge, PowerShellBridge>();
        services.AddSingleton<IDeploymentHistoryService, DeploymentHistoryService>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IProfileExportService, ProfileExportService>();
        services.AddSingleton<IDeploymentStateService, DeploymentStateService>();
        services.AddSingleton<IUndoService, UndoService>();
        services.AddSingleton<IValidationService, ValidationService>();
        services.AddSingleton<ToastService>();
        services.AddSingleton<INavigationService, NavigationService>();

        // Register ViewModels as transient (new instance per request)
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<DeploymentViewModel>();
        services.AddTransient<AppsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<PrerequisitesViewModel>();

        return services;
    }
}
