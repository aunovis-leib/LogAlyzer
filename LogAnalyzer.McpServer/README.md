# LogAnalyzer MCP Server (Starter)

This project provides a minimal MCP server over `stdio` for local AI-agent integration.

## Implemented MCP methods

- `initialize`
- `tools/list`
- `tools/call`
- `ping`

## Tools

The server does **not** analyze logs on its own. It delegates parsing to the existing
`LogAnalyzer` parser tooling and exposes the already parsed log lines.

### `list_parser_profiles`

- Input: `{}`
- Output: the parser profiles configured in the LogAnalyzer tool
  (name, dateFormat, splitter, contextDate settings, time offset).

### `get_parsed_log_lines`

- Input:
  - `filePath` (string, required) – absolute path to the log file
  - `profileName` (string, optional) – parser profile to use; omitted = legacy parser
  - `maxEntries` (integer, optional) – limit for returned entries (default `1000`)
- Output: JSON with
  - `filePath`, `profile`, `returnedEntryCount`, `truncated`
  - `entries[]` with `date`, `isTimeOnlyTimestamp`, `type`, `text`, `detail`, `rawLine`

Dates come from the parsed log **line content** (via the existing parsers), not from the file name.

## Run locally

```powershell
dotnet run --project LogAnalyzer.McpServer/LogAnalyzer.McpServer.csproj
```

The process is intended to be launched by an MCP client, not manually typed against.

## Example MCP client registration

Use your client’s MCP config format and point it to:

- command: `dotnet`
- args:
  - `run`
  - `--project`
  - `D:/Git/Projekte/LogAnalyzer/LogAnalyzer.McpServer/LogAnalyzer.McpServer.csproj`

## Next extension ideas

- Add path allow-list validation for `get_parsed_log_lines`.
- Add filtering by log type or time range on top of the parsed entries.
- Add structured tool outputs instead of text-wrapped JSON for richer clients.
