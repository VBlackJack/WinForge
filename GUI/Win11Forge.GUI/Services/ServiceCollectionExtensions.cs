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
using Win11Forge.GUI.Services.Coordinators;
using Win11Forge.GUI.Services.Implementations;
using Win11Forge.GUI.Services.PowerShell;
using Win11Forge.GUI.Services.Resume;
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
        // Register logging service
        services.AddSingleton<ILoggerFactory, LoggerFactory>();
        services.AddSingleton<ILoggingService, LoggingService>();

        // Register detection service (used by application management)
        services.AddSingleton<IApplicationDetectionService, HybridDetectionService>();
        services.AddSingleton<CacheWarmingService>();

        // Register PowerShell support services (ISP compliant, specialized services)
        services.AddSingleton<IRepositoryPathService, RepositoryPathService>();
        services.AddSingleton<IPowerShellExecutionService, PowerShellExecutionService>();
        services.AddSingleton<IApplicationCacheService, ApplicationCacheService>();
        services.AddSingleton<IProfileMigrationService, ProfileMigrationService>();

        // Register focused service implementations (ISP compliant)
        services.AddSingleton<IVersionService, VersionServiceImpl>();
        services.AddSingleton<ISystemInfoService, SystemInfoServiceImpl>();
        services.AddSingleton<IProfileManagementService, ProfileManagementServiceImpl>();
        services.AddSingleton<IApplicationManagementService, ApplicationManagementServiceImpl>();
        services.AddSingleton<IPrerequisitesService, PrerequisitesServiceImpl>();

        // Register PowerShellBridge facade for backward compatibility
        // Components that still depend on IPowerShellBridge can use the facade
        // New components should depend on the focused interfaces directly
        services.AddSingleton<IPowerShellBridge, PowerShellBridgeFacade>();

        // Register other application services
        services.AddSingleton<IDeploymentHistoryService, DeploymentHistoryService>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IProfileExportService, ProfileExportService>();
        services.AddSingleton<IDeploymentStateService, DeploymentStateService>();
        services.AddSingleton<IScheduledDeploymentService, ScheduledDeploymentService>();
        services.AddSingleton<IUndoService, UndoService>();
        services.AddSingleton<IValidationService, ValidationService>();
        services.AddSingleton<IProfileValidationService, ProfileValidationService>();
        services.AddSingleton<IProfileBridge, ProfileBridge>();
        services.AddSingleton<IApplicationBridge, ApplicationBridge>();
        services.AddSingleton<IApplicationDatabaseService, ApplicationDatabaseService>();
        services.AddSingleton<IPackageVerificationService, PackageVerificationService>();
        services.AddSingleton<IPackageSearchService, PackageSearchService>();
        services.AddSingleton<ToastService>();
        services.AddSingleton<IToastService>(sp => sp.GetRequiredService<ToastService>());
        services.AddSingleton<IErrorHistoryService, ErrorHistoryService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<IApplicationEditorDialogService, ApplicationEditorDialogService>();
        services.AddSingleton<IApplicationLifetimeService, ApplicationLifetimeService>();
        services.AddSingleton<IProcessLauncher, ProcessLauncher>();
        services.AddSingleton<IPauseGate, PauseGate>();
        services.AddSingleton<IBatchResumeService, BatchResumeService>();
        services.AddSingleton<IAppScanCoordinator, AppScanCoordinator>();
        services.AddSingleton<IAppInstallationCoordinator, AppInstallationCoordinator>();
        services.AddSingleton<IAppUpdateCoordinator, AppUpdateCoordinator>();
        services.AddSingleton<IAppUninstallCoordinator, AppUninstallCoordinator>();

        // Register ViewModels as transient (new instance per request)
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<DeploymentViewModel>();
        services.AddTransient<AppsViewModel>();
        services.AddTransient<AppCatalogViewModel>();
        services.AddTransient<ApplicationEditorViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<PrerequisitesViewModel>();

        return services;
    }
}
