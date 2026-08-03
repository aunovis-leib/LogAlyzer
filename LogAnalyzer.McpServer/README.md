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

### `get_live_loaded_log_lines`

- Input:
  - `maxEntriesPerList` (integer, optional) – default `500`
  - `maxTotalEntries` (integer, optional) – default `5000`
  - `pipeName` (string, optional) – default `LogAnalyzer.LiveTools`
- Output:
  - Snapshot of currently loaded parsed entries from the **running LogAnalyzer app**.
  - Includes `generatedAtUtc`, `listCount`, `returnedEntryCount`, `truncated`, and `lists[]`.

This is the preferred tool when an agent should use the live program state instead of parsing files itself.

### `get_live_open_files`

- Input:
  - `pipeName` (string, optional) – default `LogAnalyzer.LiveTools`
- Output:
  - Per-list open/loaded files and a de-duplicated `distinctFiles` list.

### `get_live_selected_entry`

- Input:
  - `pipeName` (string, optional) – default `LogAnalyzer.LiveTools`
- Output:
  - Current selection snapshot (`hasSelection`, `listIndex`, `lineNumber`, entry payload).

### `select_live_entry` (action)

- Input:
  - `listIndex` (integer, required)
  - `lineNumber` (integer, required)
  - `pipeName` (string, optional) – default `LogAnalyzer.LiveTools`
- Output:
  - Action result (`success`, `listIndex`, `lineNumber`).

### `load_live_files` (action)

- Input:
  - `listIndex` (integer, required)
  - `filePaths` (string array, required)
  - `pipeName` (string, optional) – default `LogAnalyzer.LiveTools`
- Output:
  - Action result from running app (`success`, `listIndex`, `requestedFileCount`, `error`).

### `set_live_filter_text` (action)

- Input:
  - `listIndex` (integer, required)
  - `filterText` (string, required) – empty string clears the filter
  - `pipeName` (string, optional) – default `LogAnalyzer.LiveTools`
- Output:
  - Action result from running app (`success`, `listIndex`, `filterText`, `error`).

### `set_live_time_filter` (action)

- Input:
  - `listIndex` (integer, required)
  - `fromDate` / `toDate` (string, optional) – inclusive date bounds, e.g. `2026-08-03`; omit/null clears the bound
  - `fromTime` / `toTime` (string, optional) – inclusive time-of-day bounds, e.g. `05:40:00`; omit/null clears the bound
  - `pipeName` (string, optional) – default `LogAnalyzer.LiveTools`
- Output:
  - Action result from running app (`success`, `listIndex`, `fromDate`, `toDate`, `fromTime`, `toTime`, `error`).
- Note: `get_live_loaded_log_lines` returns entries from the **filtered/visible** view of each list,
  so combining `set_live_time_filter` (and/or `set_live_filter_text`) with `get_live_loaded_log_lines`
  is the preferred way to inspect a specific time range or gap without pulling all loaded entries.

## Run locally

```powershell
dotnet run --project LogAnalyzer.McpServer/LogAnalyzer.McpServer.csproj
```

The process is intended to be launched by an MCP client, not manually typed against.

## Live integration note

For all `get_live_*` and `select_live_entry` tools, the desktop app `LogAnalyzer` must be running.
The app hosts a local named pipe endpoint (`LogAnalyzer.LiveTools`) and returns
already parsed entries currently loaded in its UI lists.

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
