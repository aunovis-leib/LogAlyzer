# LogAlyzer OneDrive Plugin

The plugin uses Microsoft Graph with delegated `Files.Read` permission and reads
a folder shared by another OneDrive owner. The signed-in account only needs
access to the share; it does not need to own the files.

1. Register a public client application in Microsoft Entra ID.
2. Enable public client flows for the application.
3. Create `%LOCALAPPDATA%\LogAlyzer\PluginData\logalyzer.onedrive\settings.json`.
4. Set the application client ID in that file:

```json
{
  "clientId": "YOUR_PUBLIC_CLIENT_ID",
  "tenantId": "common",
  "scopes": [ "Files.Read" ],
  "sharedFolderUrl": "https://YOUR-SHARED-FOLDER-URL"
}
```

`sharedFolderUrl` must be the HTTP or HTTPS link to the shared folder, not to a
single file. The plugin resolves this link through the Microsoft Graph Shares
API and then enumerates and downloads through the foreign drive that owns the
folder. It never assumes that the files are under the signed-in user's
`/me/drive` root.

The first `Aktualisieren` action opens the Microsoft login in the system browser.
Before downloading, the plugin shows new or changed remote files and asks for
confirmation. Declined files remain pending for the next refresh.
After confirmation, remote `.log` and `.csv` files are copied into the configured
LogAlyzer explorer root under the `logalyzer.onedrive` archive folder. Files that
disappear from OneDrive are not deleted locally.