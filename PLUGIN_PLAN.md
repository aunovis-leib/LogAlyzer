# LogAlyzer Plugin Plan

This file is the persistent handover for future chat sessions.

## Current status

The optional plugin system and the OneDrive shared-folder plugin are implemented and committed.

- Branch: `feature/plugin-system`
- Foundation commit: `1e5e3ca`
- OneDrive implementation commit: `4a0a63f`
- Latest validation: 100 tests passed and the Release solution build succeeded

## Completed changes

- Added versioned, WPF-independent plugin contracts.
- Added plugin discovery, manifest validation, collectible AssemblyLoadContext isolation, failure isolation, and lifecycle handling.
- Added host services for remote synchronization and loading synchronized files into the first local log list.
- Added remote synchronization with UTC timestamp, ETag, and size comparison.
- Added atomic `.part-*` downloads and persisted synchronization state.
- Kept locally archived files when they disappear from the remote source.
- Added the separate `LogAlyzer.OneDrive` project and `logalyzer.onedrive` manifest.
- Added initial Microsoft Graph folder and log-file enumeration with MSAL authentication.
- The production target is a folder shared by another OneDrive owner; the signed-in user
  does not need to own the remote files.
- Added lazy OneDrive authentication so an unconfigured plugin can still load.
- Added the generic plugin panel and the OneDrive `Aktualisieren` action to Settings.
- Added remote-file preview and explicit confirmation before new or changed files are downloaded.
- Added `sharedFolderUrl` configuration and Microsoft Graph Shares API resolution.
- Added foreign `driveId`/`itemId` routing for shared-folder enumeration and downloads.
- Added deployment of the plugin assembly, manifest, README, and runtime dependencies.
- Added unit, loader, deployment, synchronization, share-resolution, and declined-download tests.

## Open tasks

1. Run the application with the same Microsoft account that has access to the share and
  verify interactive login including the configured two-factor authentication.
2. Verify initial download of `.log` and `.csv` files into the configured explorer root.
3. Verify that a newer remote timestamp causes an update.
4. Verify that unchanged files are not downloaded again.
5. Verify that remote deletion increments the missing count but does not delete the local archive.
6. Verify the delegated permission required for shared-item resolution. Microsoft Graph currently
  documents `Files.ReadWrite` as the least-privileged delegated permission for the Shares API;
  keep the read-only `Files.Read` scope unless the shared-folder flow demonstrably requires more.
7. Record any Graph permissions, tenant, cross-tenant sharing, MFA, or path issues discovered
   during the manual run.

## Configuration

Create `settings.json` with:

```json
{
  "clientId": "YOUR_PUBLIC_CLIENT_ID",
  "tenantId": "common",
  "scopes": [ "Files.Read" ],
  "sharedFolderUrl": "https://YOUR-SHARED-FOLDER-URL"
}
```

The account used for the interactive login must have read access to the shared folder;
it does not need to be the folder owner. MFA is completed in the Microsoft browser login
and is not stored in the plugin settings.

The application deploys the plugin under:

`LogAlyzer/bin/<Configuration>/net10.0-windows/Plugins/LogAlyzer.OneDrive/`

## Useful validation commands

```powershell
dotnet test .\LogAlyzer.Tests\LogAlyzer.Tests.csproj -c Release --no-restore
dotnet build .\LogAlyzer.slnx -c Release --no-restore
git diff --check
```

## Handover entry point

Start with the shared-folder configuration and Graph shared-item resolution. Do not
assume that the files are in the signed-in user's own OneDrive root. Do not change the
remote deletion behavior: remote files that disappear must remain in the local archive.
