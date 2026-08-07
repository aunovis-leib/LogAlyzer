# LogAlyzer OneDrive Plugin

The plugin uses Microsoft Graph with delegated `Files.Read` permission.

1. Register a public client application in Microsoft Entra ID.
2. Enable public client flows for the application.
3. Create `%LOCALAPPDATA%\LogAlyzer\PluginData\logalyzer.onedrive\settings.json`.
4. Set the application client ID in that file:

```json
{
  "clientId": "YOUR_PUBLIC_CLIENT_ID",
  "tenantId": "common",
  "scopes": [ "Files.Read" ]
}
```

The first `Aktualisieren` action opens the Microsoft login in the system browser.
Remote `.log` and `.csv` files are copied into the configured LogAlyzer explorer
root under the `logalyzer.onedrive` archive folder. Files that disappear from
OneDrive are not deleted locally.