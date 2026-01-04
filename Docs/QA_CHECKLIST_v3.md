# Win11Forge v3.0.0 QA Checklist

**Version:** 3.0.0
**Date:** January 4, 2026
**Tester:** ________________
**Environment:** ________________

---

## Pre-Test Setup

- [ ] Windows 10 21H2+ or Windows 11 installed
- [ ] PowerShell 7.4+ available
- [ ] .NET 8.0 SDK installed (for development builds)
- [ ] Clean test environment (VM recommended)
- [ ] Internet connection available
- [ ] Run as Administrator when required

---

## 1. Installation & Launch

### 1.1 Release Package
- [ ] Download `Win11Forge_v3.0.0.zip`
- [ ] Extract to target folder
- [ ] Verify all files present:
  - [ ] `Win11Forge.GUI.exe`
  - [ ] `Start-Win11Forge.ps1`
  - [ ] `Win11Forge.ps1`
  - [ ] `Modules/` folder
  - [ ] `Core/` folder
  - [ ] `Apps/` folder
  - [ ] `Profiles/` folder
  - [ ] `Config/` folder

### 1.2 Application Startup
- [ ] Double-click `Win11Forge.GUI.exe` - launches GUI
- [ ] Execute `Start-Win11Forge.ps1` - launches GUI
- [ ] Execute `Start-Win11Forge.ps1 -CLI` - launches CLI
- [ ] Startup time < 3 seconds
- [ ] No error dialogs on first launch

---

## 2. Dashboard View

### 2.1 System Information
- [ ] Windows version displayed correctly
- [ ] Windows build number accurate
- [ ] Environment type detected (Physical/VM)
- [ ] Administrator status correct

### 2.2 Statistics Cards
- [ ] Apps installed count matches reality
- [ ] Profiles count accurate
- [ ] Recent deployments count correct

### 2.3 Recent Activity
- [ ] Shows deployment history entries
- [ ] Entries sorted by date (newest first)
- [ ] Maximum 5 entries displayed
- [ ] Empty state handled gracefully

### 2.4 Navigation
- [ ] Click "Deployment" navigates correctly
- [ ] Click "Apps" navigates correctly
- [ ] Click "Profiles" navigates correctly
- [ ] Click "Settings" navigates correctly

---

## 3. Deployment View

### 3.1 Profile Selection
- [ ] Dropdown lists all available profiles
- [ ] Default selection is "Base"
- [ ] Profile inheritance shown (e.g., "Gaming (inherits: Office)")

### 3.2 Application List
- [ ] Applications loaded from selected profile
- [ ] Checkboxes functional (select/deselect)
- [ ] "Select All" works
- [ ] "Deselect All" works
- [ ] Installation status indicators correct:
  - [ ] Green = Installed
  - [ ] Gray = Not installed

### 3.3 Deployment Execution
- [ ] "Deploy" button starts deployment
- [ ] Progress bar updates in real-time
- [ ] Percentage displayed correctly
- [ ] Parallel threads visualization accurate
- [ ] Log output streams in real-time
- [ ] Successful installations marked green
- [ ] Failed installations marked red

### 3.4 Pause/Resume
- [ ] "Pause" button visible during deployment
- [ ] Click "Pause" stops new installations
- [ ] Status shows "Paused"
- [ ] "Resume" button appears
- [ ] Click "Resume" continues deployment
- [ ] Progress continues from where it paused

### 3.5 Cancel
- [ ] "Cancel" button visible during deployment
- [ ] Click "Cancel" stops deployment
- [ ] Confirmation dialog shown
- [ ] Graceful cleanup occurs
- [ ] Can start new deployment after cancel

### 3.6 Completion
- [ ] Success message on completion
- [ ] Summary of installed/failed apps
- [ ] History entry created
- [ ] UI returns to interactive state

---

## 4. Application Manager

### 4.1 Application List
- [ ] All applications from database displayed
- [ ] Name, category, status columns visible
- [ ] Scrolling works for long lists

### 4.2 Search Functionality
- [ ] Search box accepts input
- [ ] Results filter in real-time
- [ ] Case-insensitive search
- [ ] Partial match works
- [ ] Clear search restores full list

### 4.3 Category Filter
- [ ] "All" shows all categories
- [ ] Individual category filters work
- [ ] Count accurate per category

### 4.4 Status Filter
- [ ] "All" shows all statuses
- [ ] "Installed" shows only installed
- [ ] "Available" shows only not installed

### 4.5 Scan Status
- [ ] "Scan" button initiates status check
- [ ] Progress shown during scan
- [ ] Status updates after scan completes
- [ ] Concurrent scan threads visible

### 4.6 Application Actions
- [ ] Click on app shows details
- [ ] "Install" button works for available apps
- [ ] Installation progress shown
- [ ] Status updates after installation

---

## 5. Profile Editor

### 5.1 Profile List
- [ ] All profiles displayed
- [ ] Current profile highlighted
- [ ] "New Profile" button visible

### 5.2 Create New Profile
- [ ] Click "New Profile" opens editor
- [ ] Name field accepts input
- [ ] Parent profile dropdown works
- [ ] Description field optional

### 5.3 Edit Existing Profile
- [ ] Select profile loads it in editor
- [ ] Inherited apps shown (read-only)
- [ ] Local apps shown (editable)
- [ ] Add application works
- [ ] Remove application works

### 5.4 Inheritance Visualization
- [ ] Parent profile clearly indicated
- [ ] Inherited apps visually distinct
- [ ] Cannot remove inherited apps
- [ ] Can override inherited apps

### 5.5 Save Profile
- [ ] "Save" creates/updates profile
- [ ] Validation errors shown
- [ ] Success message displayed
- [ ] Profile appears in list
- [ ] File saved to Profiles/ folder

---

## 6. Settings

### 6.1 Theme Switching
- [ ] Dark theme toggle visible
- [ ] Toggle switches theme immediately
- [ ] Theme persists after restart
- [ ] All views respect theme

### 6.2 Language Selection
- [ ] Language dropdown shows EN/FR
- [ ] Select different language
- [ ] "Apply" shows restart warning
- [ ] Language persists after restart
- [ ] All text changes to selected language

### 6.3 History Management
- [ ] History section visible
- [ ] Entry count displayed
- [ ] "Clear History" works
- [ ] Confirmation before clear
- [ ] History empty after clear

### 6.4 Settings Persistence
- [ ] Settings saved to `%LOCALAPPDATA%\Win11Forge\settings.json`
- [ ] File readable JSON
- [ ] Settings survive reinstall

---

## 7. Error Handling

### 7.1 Network Errors
- [ ] Graceful handling of no internet
- [ ] Error message displayed
- [ ] Retry option available
- [ ] Other operations still work

### 7.2 Permission Errors
- [ ] UAC prompt when needed
- [ ] Error if UAC declined
- [ ] Clear instruction to user

### 7.3 Invalid Data
- [ ] Corrupted profile handled
- [ ] Missing app database handled
- [ ] Invalid settings file reset

### 7.4 PowerShell Errors
- [ ] PowerShell exceptions caught
- [ ] User-friendly error message
- [ ] Detailed log available

---

## 8. Performance

### 8.1 Memory Usage
- [ ] Idle memory < 200 MB
- [ ] Memory during deployment < 500 MB
- [ ] Memory released after operations
- [ ] No memory leaks over extended use

### 8.2 Responsiveness
- [ ] UI remains responsive during deployment
- [ ] Smooth scrolling in lists
- [ ] No UI freezing
- [ ] Progress updates < 1s latency

### 8.3 Startup Time
- [ ] Cold start < 5 seconds
- [ ] Warm start < 3 seconds
- [ ] Settings applied before UI shows

---

## 9. Accessibility

### 9.1 Keyboard Navigation
- [ ] Tab order logical
- [ ] Enter activates focused button
- [ ] Escape closes dialogs
- [ ] Shortcuts work (if defined)

### 9.2 Screen Reader
- [ ] All buttons labeled
- [ ] Progress announced
- [ ] Error messages readable

### 9.3 Visual
- [ ] Sufficient contrast (both themes)
- [ ] Text readable at 100% DPI
- [ ] Works at 125% DPI
- [ ] Works at 150% DPI

---

## 10. CLI Compatibility

### 10.1 Basic Operations
- [ ] `.\Win11Forge.ps1` shows help
- [ ] Profile deployment works
- [ ] All CLI features functional
- [ ] No interference from GUI settings

### 10.2 Concurrent Use
- [ ] GUI and CLI can run simultaneously
- [ ] Shared profile changes visible
- [ ] No file locking issues

---

## 11. Edge Cases

### 11.1 Empty States
- [ ] No profiles - handled gracefully
- [ ] No history - shows empty state
- [ ] No apps in profile - message shown

### 11.2 Large Data
- [ ] 100+ apps in profile - performs well
- [ ] 50+ history entries - scrollable
- [ ] Long app names - truncated properly

### 11.3 Special Characters
- [ ] Profile names with spaces work
- [ ] App names with special chars display
- [ ] Paths with spaces handled

---

## 12. Regression Tests

### 12.1 Core Functionality
- [ ] Winget installations work
- [ ] Chocolatey installations work
- [ ] Direct URL downloads work
- [ ] Windows Features work
- [ ] Windows Capabilities work

### 12.2 Profile System
- [ ] Inheritance resolves correctly
- [ ] Profile merging works
- [ ] Environment restrictions applied

### 12.3 Existing Tests
- [ ] All 479 PowerShell tests pass
- [ ] All 36 GUI tests pass
- [ ] PSScriptAnalyzer: 0 warnings

---

## Test Results Summary

| Section | Pass | Fail | Skip | Notes |
|---------|------|------|------|-------|
| 1. Installation | | | | |
| 2. Dashboard | | | | |
| 3. Deployment | | | | |
| 4. Apps Manager | | | | |
| 5. Profile Editor | | | | |
| 6. Settings | | | | |
| 7. Error Handling | | | | |
| 8. Performance | | | | |
| 9. Accessibility | | | | |
| 10. CLI Compat | | | | |
| 11. Edge Cases | | | | |
| 12. Regression | | | | |
| **TOTAL** | | | | |

---

## Sign-Off

**QA Complete:** [ ] Yes  [ ] No

**Blocking Issues:**
-

**Notes:**
-

**Tester Signature:** ________________ **Date:** ________________

**Approver Signature:** ________________ **Date:** ________________
