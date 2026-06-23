<!--
Copyright 2026 Julien Bombled

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
-->

# WinForge User Guide

Current framework display version: `2026062301`.

## Quick Start
1. Extract the release archive.
2. Run `WinForge.cmd` or start the GUI executable.
3. Select a profile or adjust the application selection manually.
4. Start deployment and monitor progress.

## Profiles
- `Base`: Core apps and utilities.
- `Office`: Productivity-focused setup.
- `Gaming`: Games and communication tools.
- `Personnel`: Developer-oriented stack.
- `Enterprise`: Security-oriented configuration.

Profiles can inherit from other profiles. For example, `Gaming` builds on
`Office`, and `Office` builds on `Base`.

### Edit an Existing Profile

1. Open `Applications`.
2. Select the profile in the `Profile` selector.
3. Check or uncheck applications in the grid.
4. Click `Update profile`.

The profile file is updated with the current checked applications. If the
profile inherits from another profile, inherited applications remain selected
because they are owned by the parent profile. Edit the parent profile to remove
those inherited applications, or save a new profile without that parent.

### Create a New Profile

1. Select the applications you want.
2. Click `Save Profile`.
3. Choose a new profile name and optional parent profile.
4. Save.

## Typical Workflow
1. Validate prerequisites.
2. Review selected apps.
3. Launch deployment.
4. Check logs if any step fails.
5. Use rollback if needed.

## Application Catalog
- Browse and edit the application database from the GUI.
- Edits preserve verification metadata when the application payload does not change.
- Use `Tools/Validate-AppDatabase.ps1` after database changes.

## Troubleshooting
- Verify admin rights for system-level operations.
- Ensure internet connectivity for package sources.
- Re-run checks with `Run-All-Checks.ps1`.
- Validate the app database with `Tools/Validate-AppDatabase.ps1`.
- Run the opt-in GUI smoke with `Tools/Invoke-WinsightSmoke.ps1` when changing desktop UI workflows.

## Additional References
- API documentation: `Docs/API_DOCUMENTATION.md`
- Project README: `README.md`
