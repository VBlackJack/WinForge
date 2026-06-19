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

namespace Win11Forge.GUI.Configuration;

/// <summary>
/// Centralized file and directory names used by path resolution services.
/// </summary>
internal static class Win11ForgePathNames
{
    public const string ProductDirectoryName = "Win11Forge";
    public const string AppsDirectoryName = "Apps";
    public const string DatabaseDirectoryName = "Database";
    public const string ApplicationsDatabaseFileName = "applications.json";
    public const string ConfigDirectoryName = "Config";
    public const string DetectionAllowlistFileName = "detection-allowlist.json";
    public const string DetectionRegistryPolicyFileName = "detection-registry-policy.json";
    public const string VersionFileName = "version.json";
    public const string ModulesDirectoryName = "Modules";
    public const string InstallationEngineModuleFileName = "InstallationEngine.psm1";
    public const string LogsDirectoryName = "Logs";
    public const string JsonLogsDirectoryName = "json";
    public const string JsonFileExtension = ".json";
    public const string SettingsFileName = "settings.json";
    public const string DeploymentHistoryFileName = "history.json";
    public const string ProfilesDirectoryName = "Profiles";
    public const string DefaultProfilesDirectoryName = "Defaults";
    public const string ProfileMigrationSentinelFileName = ".migrated";
    public const string LegacyProfileConflictSuffix = ".legacy";
    public const string StartupLogFileName = "Win11Forge_startup.log";
    public const string WriteProbeFilePrefix = ".write-test-";
    public const int ProfileMigrationVersion = 1;
}
