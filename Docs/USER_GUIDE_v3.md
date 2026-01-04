# Win11Forge v3.0.0 - User Guide

This guide explains how to use the Win11Forge GUI application for automated Windows 11 deployment and application management.

## Table of Contents

1. [Getting Started](#getting-started)
2. [Dashboard](#dashboard)
3. [Deployment](#deployment)
4. [Application Manager](#application-manager)
5. [Profile Editor](#profile-editor)
6. [Settings](#settings)
7. [Troubleshooting](#troubleshooting)

---

## Getting Started

### System Requirements

- Windows 10/11 (64-bit)
- .NET 8.0 Runtime (included in self-contained builds)
- PowerShell 5.1 or later
- Administrator privileges (recommended for full functionality)

### Launching the Application

**Option 1: GUI Mode (Recommended)**
```powershell
.\Start-Win11Forge.ps1
```

**Option 2: CLI Mode**
```powershell
.\Start-Win11Forge.ps1 -CLI
```

**Option 3: Direct Launch**
```powershell
.\Win11Forge.GUI.exe
```

---

## Dashboard

The Dashboard provides an overview of your Win11Forge environment and recent activity.

### Stats Cards

At the top of the Dashboard, you'll find four statistics cards:

| Card | Description |
|------|-------------|
| **Profiles** | Number of available deployment profiles |
| **Applications** | Total applications in the database |
| **Deployments** | Number of completed deployments |
| **Version** | Current Win11Forge version |

### System Information

The System Information panel displays:

- **Hostname**: Your computer name
- **OS**: Windows version (e.g., Windows 11 Pro)
- **Build**: Windows build number
- **Administrator**: Whether the app is running with admin privileges
- **Winget**: Winget availability and version
- **Chocolatey**: Chocolatey availability and version

> **Tip**: For full functionality, run Win11Forge as Administrator. Some applications require elevated privileges for installation.

### Recent Activity

This section shows your last 5 deployment sessions, including:

- Profile name used
- Date and time
- Result (Success, Partial Success, Failed, Cancelled)
- Number of applications installed

Click the **Refresh** button to update the dashboard data.

---

## Deployment

The Deployment view is where you execute application installations.

### Selecting a Profile

1. Use the **Profile** dropdown to select a deployment profile
2. The profile summary will display:
   - Total applications in the profile
   - Required applications count
   - Inheritance chain (if any)

### Application Selection

- **Select All**: Select all applications for installation
- **Select None**: Deselect all optional applications (required apps remain selected)
- Individual checkboxes allow fine-grained selection

The application list shows:
- Application name and category
- Priority level
- Required status
- Current installation status

### Simulation Mode

> **Important**: Simulation Mode is enabled by default for safety.

When **Simulation Mode** is ON:
- No actual installations are performed
- The deployment process is simulated
- Useful for testing profiles and configurations

When **Simulation Mode** is OFF:
- Real installations are performed
- Applications are downloaded and installed
- Changes are made to your system

### Starting Deployment

1. Select your desired applications
2. Toggle Simulation Mode as needed
3. Click **Start Deployment**

During deployment:
- A progress indicator shows completion status (X/Y)
- Each application shows its current status:
  - **Pending**: Waiting to be installed
  - **Installing**: Currently being installed
  - **Installed**: Successfully installed
  - **Already Installed**: Was already present on the system
  - **Failed**: Installation failed (see error message)
  - **Skipped**: Skipped due to cancellation

### Cancelling Deployment

Click the **Cancel** button to stop the current deployment. Applications already installed will remain, but pending installations will be skipped.

---

## Application Manager

The Application Manager allows you to browse all available applications and check their installation status.

### Browsing Applications

- Use the **Search** box to filter by name
- Use the **Category** dropdown to filter by category
- The counter shows: `filtered/total | X installed`

### Scanning Installed Applications

Click **Scan Installed** to check which applications are currently installed on your system. This process:

1. Queries Winget for installed packages
2. Queries Chocolatey for installed packages
3. Checks Windows features and capabilities
4. Updates the status of each application

### Installing Individual Applications

For any application not yet installed:
1. Find the application in the list
2. Click the **Install** button in the Actions column
3. The application will be installed immediately

---

## Profile Editor

The Profile Editor allows you to create and modify deployment profiles.

### Creating a New Profile

1. Navigate to **Profile Editor** in the sidebar
2. Enter a **Profile Name** (alphanumeric, hyphens, underscores only)
3. Optionally select a **Parent Profile** to inherit applications from
4. Add a **Description** (optional)
5. Add applications using the **+** button
6. Click **Save**

### Profile Inheritance

Profiles can inherit from other profiles:

- **Inherited Applications** (locked): Shown with a lock icon, inherited from parent profile
- **Added Applications**: Applications you add to this profile

Example hierarchy:
```
base.json
  -> developer.json (adds dev tools)
    -> frontend.json (adds web dev tools)
```

### Adding Applications

1. Click the **+** button in the "Added Applications" section
2. A dialog appears with available applications
3. Search or browse to find the application
4. Click **Add Selected**

### Removing Applications

Click the **trash** icon next to any application you've added to remove it from the profile.

> **Note**: You cannot remove inherited applications. To exclude them, create a new profile without inheriting from that parent.

---

## Settings

The Settings view allows you to customize the application.

### Appearance

**Theme**: Toggle between Light and Dark mode
- Changes are applied immediately
- Your preference is saved automatically

### Language

**Language Selection**: Choose between:
- English (Default)
- French (Francais)

> **Note**: Language changes require an application restart to take effect fully.

To change language:
1. Select the new language from the dropdown
2. Click **Apply**
3. Restart the application

### Data

**Clear History**: Remove all deployment history entries
- Click **Clear** to delete all history
- This action cannot be undone

---

## Troubleshooting

### Common Issues

#### "Winget not available"

Winget is required for most application installations. To install:
1. Open Microsoft Store
2. Search for "App Installer"
3. Install or update the package

#### "Chocolatey not available"

Some applications require Chocolatey. To install:
```powershell
Set-ExecutionPolicy Bypass -Scope Process -Force
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072
iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
```

#### "Administrator privileges required"

Run Win11Forge as Administrator:
1. Right-click on `Win11Forge.GUI.exe`
2. Select "Run as administrator"

#### Installation Fails

If an application fails to install:
1. Check the error message in the deployment view
2. Verify your internet connection
3. Try running as Administrator
4. Check if the package ID is correct in the Apps database

### Log Files

Deployment logs are stored in:
```
%LOCALAPPDATA%\Win11Forge\history.json
```

### Getting Help

- GitHub Issues: [https://github.com/VBlackJack/Win11Forge/issues](https://github.com/VBlackJack/Win11Forge/issues)
- Documentation: See `Docs/` folder

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+1` | Navigate to Dashboard |
| `Ctrl+2` | Navigate to Deployment |
| `Ctrl+3` | Navigate to Applications |
| `Ctrl+4` | Navigate to Profile Editor |
| `Ctrl+5` | Navigate to Settings |

---

## Version History

### v3.0.0 (Current)
- New WPF GUI with Material Design
- Deployment history tracking
- Profile editor with inheritance
- Theme switching (Light/Dark)
- Multi-language support (EN/FR)
- Application scanning

### v2.x
- PowerShell CLI only
- Profile-based deployment
- Parallel installation support

---

*Win11Forge v3.0.0 - Automated Windows 11 Deployment*
*Licensed under Apache 2.0*
