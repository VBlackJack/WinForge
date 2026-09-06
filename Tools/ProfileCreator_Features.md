# Profile Creator

[Français](ProfileCreator_Features.fr.md)

Open `Tools/ProfileCreator.html` in a browser. The wizard runs locally without a web server and reads its standalone application bundle from `applications-data.js`.

## Workflow

1. Enter the profile name, description, and version.
2. Choose an optional parent profile, such as Base or Office.
3. Select bundled applications by category.
4. Add custom applications with a name, category, at least one installation source, and optional installation arguments.
5. Configure Explorer, taskbar, network, privacy, performance, and security options.
6. Review the generated JSON and download the profile.

Custom sources can use Winget, Chocolatey, Microsoft Store, or a direct URL. Remove unwanted entries before exporting. The preview and application counters reflect the current wizard selection.

## Example

For Postman, enter `Postman.Postman` as the Winget ID, `postman` as the Chocolatey package, and `Development` as the category. Verify identifiers against their sources before deployment.

Profiles contain `Name`, `Description`, `Version`, `Inherits`, `Applications`, and optional `SystemConfig`. The wizard can emit inline application definitions; the runtime catalog also supports ID references.

## Validate and deploy

Place the downloaded JSON under `Profiles/` using a distinct filename. Review custom detection and download-verification fields against the shipped schemas. Browser field validation does not prove installer authenticity or successful runtime detection.

```powershell
Import-Module .\Modules\JsonSchemaValidation.psm1 -Force
Test-JsonAgainstSchema -JsonPath 'Profiles/MyProfile.json' -SchemaPath 'Schemas/deployment-profile.schema.json'
.\Deploy-Win11Environment.ps1 -ProfileName 'MyProfile' -TestMode
```

Inspect the result before running a real deployment without `-TestMode`. Current security controls reject arbitrary command detection and direct downloads without the required trust metadata.

The wizard stores its working selection locally in the browser session; download the file to retain a profile. Its bundled catalog may differ from the main application database. See the [catalog reference](../Apps/README.md) and [user guide](../Docs/USER_GUIDE.md).
