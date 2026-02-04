# Application Manager - User Guide

## Overview

The Application Manager is a powerful GUI feature in Win11Forge that allows you to manage the application database with full CRUD (Create, Read, Update, Delete) operations, undo/redo support, import/export capabilities, and package verification.

---

## Accessing the Application Manager

1. Launch Win11Forge GUI: `.\Start-Win11ForgeGUI.ps1`
2. Navigate to the **Apps** tab in the main navigation

---

## Main Interface

### Application List

The main view displays all applications in the database with the following columns:

| Column | Description |
|--------|-------------|
| **Name** | Application display name |
| **Category** | Application category (Browser, Development, etc.) |
| **Sources** | Available installation sources (Winget, Chocolatey, Store, etc.) |
| **Verified** | Package verification status (checkmark = verified) |

### Filtering and Search

- **Search Box**: Type to filter applications by name, ID, description, or tags
- **Category Dropdown**: Filter by specific category or "All Categories"
- **Clear Filters**: Reset all filters with one click

---

## Operations

### Adding a New Application

1. Click the **Add** button (+ icon) in the toolbar
2. Fill in the required fields in the editor dialog:
   - **App ID**: Unique identifier (letters, numbers, dots, dashes, underscores)
   - **Name**: Display name for the application
   - **Category**: Select existing or create new category
   - **Description**: Brief description of the application
3. Configure at least one installation source in the **Sources** tab
4. Optionally configure detection settings in the **Detection** tab
5. Click **Save** to add the application

### Editing an Application

1. Select an application in the list
2. Click the **Edit** button (pencil icon) or double-click the application
3. Modify the fields as needed
4. Click **Save** to apply changes

### Deleting an Application

1. Select an application in the list
2. Click the **Delete** button (trash icon)
3. Confirm the deletion in the dialog

### Duplicating an Application

1. Select an application in the list
2. Click the **Duplicate** button
3. The editor opens with a copy (ID and name appended with "_Copy")
4. Modify as needed and save

---

## Application Editor Dialog

### General Tab

| Field | Description | Required |
|-------|-------------|----------|
| **App ID** | Unique identifier (read-only for existing apps) | Yes |
| **Name** | Display name | Yes |
| **Category** | Application category | Yes |
| **Description** | Brief description | No |
| **Homepage** | Official website URL | No |
| **Priority** | Installation priority (1-100, lower = higher priority) | No |
| **Default Required** | Mark as required by default in profiles | No |

### Sources Tab

Configure one or more installation sources:

#### Winget Source
- **Package ID**: Winget package identifier (e.g., `Google.Chrome`)
- **Version**: Specific version (empty = latest)
- **Source**: Repository name (e.g., `winget`, `msstore`)

#### Chocolatey Source
- **Package Name**: Chocolatey package name (e.g., `googlechrome`)
- **Version**: Specific version (empty = latest)

#### Microsoft Store Source
- **Store ID**: 12-character alphanumeric ID (e.g., `9NBLGGH4NNS1`)

#### Direct Download Source
- **Download URL**: Direct link to installer
- **Installer Type**: exe, msi, msix, or zip
- **Silent Args**: Command-line arguments for silent installation
- **Checksum**: Optional file checksum (format: `sha256:hash`)

### Detection Tab

Configure how Win11Forge detects if the application is installed:

| Method | Path | Description |
|--------|------|-------------|
| **Registry** | Registry path | Check if registry key exists |
| **File** | File path | Check if file exists |
| **Command** | Command | Execute command and check exit code |
| **WindowsFeature** | Feature name | Check Windows feature status |

Additional options:
- **Version Key**: Registry value for version detection
- **Min Version**: Minimum required version

---

## Undo/Redo System

The Application Manager supports undo/redo for all operations:

### Keyboard Shortcuts
| Shortcut | Action |
|----------|--------|
| `Ctrl+Z` | Undo last action |
| `Ctrl+Y` | Redo last undone action |

### Toolbar Buttons
- **Undo** button (with tooltip showing next undo action)
- **Redo** button (with tooltip showing next redo action)

### Undoable Actions
- Adding applications
- Editing applications
- Deleting applications

The undo history stores up to 50 actions.

---

## Import/Export

### Exporting Applications

**Export Selected**:
1. Select one or more applications
2. Click **Export Selected** in the menu
3. Choose destination file (JSON format)

**Export All**:
1. Click **Export All** in the menu
2. Choose destination file

### Importing Applications

1. Click **Import** in the menu
2. Select a JSON file to import
3. Choose import mode:
   - **Merge**: Add new applications, skip existing
   - **Replace**: Add new and update existing
   - **Replace All**: Clear database and import

---

## Package Verification

Verify that configured package sources are valid and accessible.

### Verifying Single Application

1. Open the application editor
2. Click the **Verify** button in the footer
3. Results show which sources are valid

### Batch Verification

1. Click **Verify All** in the toolbar
2. Progress bar shows verification status
3. Applications are marked with verification status
4. Cancel verification at any time with the Cancel button

### Verification Checks

| Source | Verification Method |
|--------|---------------------|
| Winget | `winget show <package-id>` |
| Chocolatey | `choco info <package-name>` |
| Store | Store API lookup |
| Direct URL | HTTP HEAD request |

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+Z` | Undo |
| `Ctrl+Y` | Redo |
| `Ctrl+S` | Save (in editor) |
| `Escape` | Cancel (in editor) |
| `Delete` | Delete selected application |
| `F5` | Refresh list |

---

## Status Messages

The status bar at the bottom displays operation feedback:

| Status | Description |
|--------|-------------|
| `Loaded X applications` | Database loaded successfully |
| `Application added` | New application saved |
| `Application updated` | Existing application modified |
| `Application deleted` | Application removed |
| `Verification: X/Y valid` | Batch verification results |
| `Imported X added, Y updated` | Import results |
| `Exported X applications` | Export completed |

---

## Data Storage

Applications are stored in:
```
Apps/Database/applications.json
```

Backups are automatically created before modifications:
```
Apps/Database/Backups/applications_YYYYMMDD_HHMMSS.json
```

Backup rotation keeps the 10 most recent backups.

---

## Troubleshooting

### Application Won't Save

- Check that App ID is unique (for new applications)
- Verify all required fields are filled
- Ensure at least one installation source is configured
- Check the validation message in red at the bottom

### Verification Fails

- Ensure you have internet connectivity
- For Winget: Verify `winget` is installed and working
- For Chocolatey: Verify `choco` is installed and working
- For Direct URLs: Check that the URL is publicly accessible

### Import Fails

- Verify the JSON file is valid
- Check that the file follows the applications.json schema
- Look for specific errors in the import result dialog

---

## Best Practices

1. **Always verify sources** after adding or modifying applications
2. **Use meaningful App IDs** that describe the application
3. **Configure multiple sources** for redundancy
4. **Set detection methods** to enable accurate status checking
5. **Use categories** consistently for better organization
6. **Export backups** before major changes
7. **Use undo** instead of manual reversions

---

## Related Documentation

- [Applications Database Schema](../Apps/README.md)
- [Profile Management](GUI_README.md)
- [PowerShell Deployment](../README.md)
