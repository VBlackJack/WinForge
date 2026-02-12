# Win11Forge User Guide v3

## Quick Start
1. Extract the release archive.
2. Run `Win11Forge.cmd` or start the GUI executable.
3. Select a profile.
4. Start deployment and monitor progress.

## Profiles
- `Base`: Core apps and utilities.
- `Office`: Productivity-focused setup.
- `Gaming`: Games and communication tools.
- `Personnel`: Developer-oriented stack.
- `Enterprise`: Security-oriented configuration.

## Typical Workflow
1. Validate prerequisites.
2. Review selected apps.
3. Launch deployment.
4. Check logs if any step fails.
5. Use rollback if needed.

## Troubleshooting
- Verify admin rights for system-level operations.
- Ensure internet connectivity for package sources.
- Re-run checks with `Run-All-Checks.ps1`.
- Validate the app database with `Tools/Validate-AppDatabase.ps1`.

## Additional References
- API documentation: `Docs/API_DOCUMENTATION.md`
- Project README: `README.md`
