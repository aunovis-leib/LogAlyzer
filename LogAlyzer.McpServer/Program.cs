using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO.Pipes;
using System.Globalization;
using LogAlyzer.Models;
using LogAlyzer.Services;
using LogAlyzer.Services.Parsing;

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

var input = Console.OpenStandardInput();
var output = Console.OpenStandardOutput();
using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true)
{
    NewLine = "\r\n"
};

while (TryReadMessage(input, out var payload))
{
    JsonNode? message;
    try
    {
        message = JsonNode.Parse(payload);
    }
    catch
    {
        continue;
    }

    if (message is not JsonObject jsonMessage)
    {
        continue;
    }

    var method = jsonMessage["method"]?.GetValue<string>();
    var id = jsonMessage["id"];

    if (string.IsNullOrWhiteSpace(method) || id is null)
    {
        continue;
    }

    switch (method)
    {
        case "initialize":
            WriteResult(writer, id, new
            {
                protocolVersion = "2024-11-05",
                capabilities = new
                {
                    tools = new
                    {
                        listChanged = false
                    }
                },
                serverInfo = new
                {
                    name = "logalyzer-mcp-server",
                    version = "0.2.0"
                }
            }, jsonOptions);
            break;

        case "tools/list":
            WriteResult(writer, id, new
            {
                tools = new object[]
                {
                    new
                    {
                        name = "list_parser_profiles",
                        description = "Lists the parser profiles configured in the LogAlyzer tool.",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new { },
                            required = Array.Empty<string>()
                        }
                    },
                    new
                    {
                        name = "get_parsed_log_lines",
                        description = "Parses a log file using the LogAlyzer parser tooling and returns the already parsed log lines.",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                filePath = new
                                {
                                    type = "string",
                                    description = "Absolute path to the log file to parse."
                                },
                                profileName = new
                                {
                                    type = "string",
                                    description = "Optional parser profile name. If omitted, the legacy parser is used."
                                },
                                maxEntries = new
                                {
                                    type = "integer",
                                    description = "Optional maximum number of parsed entries to return (default 1000)."
                                }
                            },
                            required = new[] { "filePath" }
                        }
                    },
                    new
                    {
                        name = "get_live_loaded_log_lines",
                        description = "Reads currently loaded parsed log lines from the running LogAlyzer app via named pipe.",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                maxEntriesPerList = new
                                {
                                    type = "integer",
                                    description = "Optional maximum entries returned per list (default 500)."
                                },
                                maxTotalEntries = new
                                {
                                    type = "integer",
                                    description = "Optional maximum total entries returned (default 5000)."
                                },
                                pipeName = new
                                {
                                    type = "string",
                                    description = "Optional pipe name. Default is 'LogAlyzer.LiveTools'."
                                }
                            },
                            required = Array.Empty<string>()
                        }
                    },
                    new
                    {
                        name = "get_live_open_files",
                        description = "Reads currently open/loaded log files from the running LogAlyzer app via named pipe.",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                pipeName = new
                                {
                                    type = "string",
                                    description = "Optional pipe name. Default is 'LogAlyzer.LiveTools'."
                                }
                            },
                            required = Array.Empty<string>()
                        }
                    },
                    new
                    {
                        name = "get_live_selected_entry",
                        description = "Reads the currently selected log entry from the running LogAlyzer app via named pipe.",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                pipeName = new
                                {
                                    type = "string",
                                    description = "Optional pipe name. Default is 'LogAlyzer.LiveTools'."
                                }
                            },
                            required = Array.Empty<string>()
                        }
                    },
                    new
                    {
                        name = "select_live_entry",
                        description = "Prepares action support by selecting an entry in the running LogAlyzer app (listIndex + lineNumber).",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                listIndex = new
                                {
                                    type = "integer",
                                    description = "Zero-based list index."
                                },
                                lineNumber = new
                                {
                                    type = "integer",
                                    description = "Line number of the loaded entry in that list."
                                },
                                pipeName = new
                                {
                                    type = "string",
                                    description = "Optional pipe name. Default is 'LogAlyzer.LiveTools'."
                                }
                            },
                            required = new[] { "listIndex", "lineNumber" }
                        }
                    },
                    new
                    {
                        name = "load_live_files",
                        description = "Loads one or more files into a target list in the running LogAlyzer app.",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                listIndex = new
                                {
                                    type = "integer",
                                    description = "Zero-based list index to load files into."
                                },
                                filePaths = new
                                {
                                    type = "array",
                                    description = "Absolute log file paths to load.",
                                    items = new
                                    {
                                        type = "string"
                                    }
                                },
                                pipeName = new
                                {
                                    type = "string",
                                    description = "Optional pipe name. Default is 'LogAlyzer.LiveTools'."
                                }
                            },
                            required = new[] { "listIndex", "filePaths" }
                        }
                    },
                    new
                    {
                        name = "set_live_filter_text",
                        description = "Sets the filter text of a log list in the running LogAlyzer app.",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                listIndex = new
                                {
                                    type = "integer",
                                    description = "Zero-based list index whose filter text should be set."
                                },
                                filterText = new
                                {
                                    type = "string",
                                    description = "Filter text to apply. Empty string clears the filter."
                                },
                                pipeName = new
                                {
                                    type = "string",
                                    description = "Optional pipe name. Default is 'LogAlyzer.LiveTools'."
                                }
                            },
                            required = new[] { "listIndex", "filterText" }
                        }
                    },
                    new
                    {
                        name = "set_live_time_filter",
                        description = "Sets the date/time range filter of a log list in the running LogAlyzer app. Combined with get_live_loaded_log_lines, this returns only entries within the given range instead of all loaded entries.",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                listIndex = new
                                {
                                    type = "integer",
                                    description = "Zero-based list index whose time filter should be set."
                                },
                                fromDate = new
                                {
                                    type = "string",
                                    description = "Optional inclusive start date, e.g. '2026-08-03'. Omit or null to clear."
                                },
                                toDate = new
                                {
                                    type = "string",
                                    description = "Optional inclusive end date, e.g. '2026-08-03'. Omit or null to clear."
                                },
                                fromTime = new
                                {
                                    type = "string",
                                    description = "Optional inclusive start time of day, e.g. '05:40:00'. Omit or null to clear."
                                },
                                toTime = new
                                {
                                    type = "string",
                                    description = "Optional inclusive end time of day, e.g. '10:35:00'. Omit or null to clear."
                                },
                                pipeName = new
                                {
                                    type = "string",
                                    description = "Optional pipe name. Default is 'LogAlyzer.LiveTools'."
                                }
                            },
                            required = new[] { "listIndex" }
                        }
                    }
                }
            }, jsonOptions);
            break;

        case "tools/call":
            await HandleToolCallAsync(writer, jsonMessage, id, jsonOptions);
            break;

        case "ping":
            WriteResult(writer, id, new { }, jsonOptions);
            break;

        default:
            WriteError(writer, id, -32601, $"Method '{method}' not found.", jsonOptions);
            break;
    }
}

return;

static async Task HandleToolCallAsync(StreamWriter writer, JsonObject message, JsonNode id, JsonSerializerOptions jsonOptions)
{
    var paramsObject = message["params"] as JsonObject;
    var toolName = paramsObject?["name"]?.GetValue<string>();
    var arguments = paramsObject?["arguments"] as JsonObject;

    switch (toolName)
    {
        case "list_parser_profiles":
            WriteToolResult(writer, id, ListParserProfiles(), jsonOptions);
            break;

        case "get_parsed_log_lines":
            await GetParsedLogLinesAsync(writer, id, arguments, jsonOptions);
            break;

        case "get_live_loaded_log_lines":
            await GetLiveLoadedLogLinesAsync(writer, id, arguments, jsonOptions);
            break;

        case "get_live_open_files":
            await GetLiveOpenFilesAsync(writer, id, arguments, jsonOptions);
            break;

        case "get_live_selected_entry":
            await GetLiveSelectedEntryAsync(writer, id, arguments, jsonOptions);
            break;

        case "select_live_entry":
            await SelectLiveEntryAsync(writer, id, arguments, jsonOptions);
            break;

        case "load_live_files":
            await LoadLiveFilesAsync(writer, id, arguments, jsonOptions);
            break;

        case "set_live_filter_text":
            await SetLiveFilterTextAsync(writer, id, arguments, jsonOptions);
            break;

        case "set_live_time_filter":
            await SetLiveTimeFilterAsync(writer, id, arguments, jsonOptions);
            break;

        default:
            WriteError(writer, id, -32602, "Unknown tool name.", jsonOptions);
            break;
    }
}

static object ListParserProfiles()
{
    var profiles = AppSettingsManager.Instance.ParserProfiles;

    return new
    {
        profiles = profiles.Select(profile => new
        {
            name = profile.Name,
            dateFormat = profile.DateFormat,
            splitter = profile.Splitter,
            contextDatePrefix = profile.ContextDatePrefix,
            contextDateFormat = profile.ContextDateFormat,
            timeOffsetHours = profile.TimeOffsetHours,
            timeOffsetMinutes = profile.TimeOffsetMinutes
        }).ToArray()
    };
}

static async Task GetParsedLogLinesAsync(StreamWriter writer, JsonNode id, JsonObject? arguments, JsonSerializerOptions jsonOptions)
{
    var filePath = arguments?["filePath"]?.GetValue<string>();
    if (string.IsNullOrWhiteSpace(filePath))
    {
        WriteError(writer, id, -32602, "Tool argument 'filePath' is required.", jsonOptions);
        return;
    }

    if (!File.Exists(filePath))
    {
        WriteError(writer, id, -32602, $"File not found: {filePath}", jsonOptions);
        return;
    }

    var profileName = arguments?["profileName"]?.GetValue<string>();
    var maxEntries = 1000;
    if (arguments?["maxEntries"] is JsonNode maxNode && int.TryParse(maxNode.ToString(), out var requestedMax) && requestedMax > 0)
    {
        maxEntries = requestedMax;
    }

    ILogParser parser;
    if (!string.IsNullOrWhiteSpace(profileName))
    {
        var profile = AppSettingsManager.Instance.ParserProfiles
            .FirstOrDefault(p => string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase));

        if (profile is null)
        {
            WriteError(writer, id, -32602, $"Parser profile not found: {profileName}", jsonOptions);
            return;
        }

        parser = new ProfileLogParser(profile);
    }
    else
    {
        parser = new LegacyLogParser();
    }

    var loader = new LogFileChunkLoader(parser);
    var entries = new List<object>(Math.Min(maxEntries, 4096));
    var truncated = false;

    await foreach (var chunk in loader.LoadAsync([filePath], chunkSize: 512))
    {
        foreach (var entry in chunk.Entries)
        {
            if (entries.Count >= maxEntries)
            {
                truncated = true;
                break;
            }

            entries.Add(new
            {
                date = entry.Date,
                isTimeOnlyTimestamp = entry.IsTimeOnlyTimestamp,
                type = entry.Type.ToString(),
                text = entry.Text,
                detail = entry.Detail,
                rawLine = entry.RawLine
            });
        }

        if (truncated)
        {
            break;
        }
    }

    var result = new
    {
        filePath,
        profile = string.IsNullOrWhiteSpace(profileName) ? "Legacy" : profileName,
        returnedEntryCount = entries.Count,
        truncated,
        entries
    };

    WriteToolResult(writer, id, result, jsonOptions);
}

static void WriteToolResult(StreamWriter writer, JsonNode id, object result, JsonSerializerOptions jsonOptions)
{
    WriteResult(writer, id, new
    {
        content = new[]
        {
            new
            {
                type = "text",
                text = JsonSerializer.Serialize(result, jsonOptions)
            }
        },
        isError = false
    }, jsonOptions);
}

static async Task GetLiveLoadedLogLinesAsync(StreamWriter writer, JsonNode id, JsonObject? arguments, JsonSerializerOptions jsonOptions)
{
    var maxEntriesPerList = 500;
    var maxTotalEntries = 5000;
    var pipeName = "LogAlyzer.LiveTools";

    if (arguments?["maxEntriesPerList"] is JsonNode perListNode
        && int.TryParse(perListNode.ToString(), out var requestedPerList)
        && requestedPerList > 0)
    {
        maxEntriesPerList = requestedPerList;
    }

    if (arguments?["maxTotalEntries"] is JsonNode totalNode
        && int.TryParse(totalNode.ToString(), out var requestedTotal)
        && requestedTotal > 0)
    {
        maxTotalEntries = requestedTotal;
    }

    if (arguments?["pipeName"] is JsonNode pipeNameNode)
    {
        var requestedPipeName = pipeNameNode.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(requestedPipeName))
        {
            pipeName = requestedPipeName;
        }
    }

    var response = await SendLivePipeRequestAsync(new
    {
        command = "get_loaded_entries",
        maxEntriesPerList,
        maxTotalEntries
    }, pipeName, jsonOptions);

    HandleLivePipeResponse(writer, id, response, jsonOptions);
}

static async Task LoadLiveFilesAsync(StreamWriter writer, JsonNode id, JsonObject? arguments, JsonSerializerOptions jsonOptions)
{
    if (arguments?["listIndex"] is not JsonNode listIndexNode || !int.TryParse(listIndexNode.ToString(), out var listIndex))
    {
        WriteError(writer, id, -32602, "Tool argument 'listIndex' is required.", jsonOptions);
        return;
    }

    var filePaths = arguments?["filePaths"] as JsonArray;
    if (filePaths is null)
    {
        WriteError(writer, id, -32602, "Tool argument 'filePaths' is required.", jsonOptions);
        return;
    }

    var normalizedPaths = filePaths
        .Select(node => node?.GetValue<string>())
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .ToArray();

    if (normalizedPaths.Length == 0)
    {
        WriteError(writer, id, -32602, "Tool argument 'filePaths' must contain at least one path.", jsonOptions);
        return;
    }

    var pipeName = GetPipeName(arguments);
    var response = await SendLivePipeRequestAsync(new
    {
        command = "load_files",
        listIndex,
        filePaths = normalizedPaths
    }, pipeName, jsonOptions);

    HandleLivePipeResponse(writer, id, response, jsonOptions);
}

static async Task GetLiveOpenFilesAsync(StreamWriter writer, JsonNode id, JsonObject? arguments, JsonSerializerOptions jsonOptions)
{
    var pipeName = GetPipeName(arguments);
    var response = await SendLivePipeRequestAsync(new
    {
        command = "get_open_files"
    }, pipeName, jsonOptions);

    HandleLivePipeResponse(writer, id, response, jsonOptions);
}

static async Task GetLiveSelectedEntryAsync(StreamWriter writer, JsonNode id, JsonObject? arguments, JsonSerializerOptions jsonOptions)
{
    var pipeName = GetPipeName(arguments);
    var response = await SendLivePipeRequestAsync(new
    {
        command = "get_selected_entry"
    }, pipeName, jsonOptions);

    HandleLivePipeResponse(writer, id, response, jsonOptions);
}

static async Task SelectLiveEntryAsync(StreamWriter writer, JsonNode id, JsonObject? arguments, JsonSerializerOptions jsonOptions)
{
    if (arguments?["listIndex"] is not JsonNode listIndexNode || !int.TryParse(listIndexNode.ToString(), out var listIndex))
    {
        WriteError(writer, id, -32602, "Tool argument 'listIndex' is required.", jsonOptions);
        return;
    }

    if (arguments?["lineNumber"] is not JsonNode lineNumberNode || !int.TryParse(lineNumberNode.ToString(), out var lineNumber))
    {
        WriteError(writer, id, -32602, "Tool argument 'lineNumber' is required.", jsonOptions);
        return;
    }

    var pipeName = GetPipeName(arguments);
    var response = await SendLivePipeRequestAsync(new
    {
        command = "select_entry",
        listIndex,
        lineNumber
    }, pipeName, jsonOptions);

    HandleLivePipeResponse(writer, id, response, jsonOptions);
}

static async Task SetLiveFilterTextAsync(StreamWriter writer, JsonNode id, JsonObject? arguments, JsonSerializerOptions jsonOptions)
{
    if (arguments?["listIndex"] is not JsonNode listIndexNode || !int.TryParse(listIndexNode.ToString(), out var listIndex))
    {
        WriteError(writer, id, -32602, "Tool argument 'listIndex' is required.", jsonOptions);
        return;
    }

    if (arguments?["filterText"] is not JsonNode filterTextNode)
    {
        WriteError(writer, id, -32602, "Tool argument 'filterText' is required.", jsonOptions);
        return;
    }

    var filterText = filterTextNode.GetValue<string>();

    var pipeName = GetPipeName(arguments);
    var response = await SendLivePipeRequestAsync(new
    {
        command = "set_filter_text",
        listIndex,
        filterText
    }, pipeName, jsonOptions);

    HandleLivePipeResponse(writer, id, response, jsonOptions);
}

static async Task SetLiveTimeFilterAsync(StreamWriter writer, JsonNode id, JsonObject? arguments, JsonSerializerOptions jsonOptions)
{
    if (arguments?["listIndex"] is not JsonNode listIndexNode || !int.TryParse(listIndexNode.ToString(), out var listIndex))
    {
        WriteError(writer, id, -32602, "Tool argument 'listIndex' is required.", jsonOptions);
        return;
    }

    if (!TryParseOptionalDate(arguments, "fromDate", out var fromDate, out var fromDateError))
    {
        WriteError(writer, id, -32602, fromDateError!, jsonOptions);
        return;
    }

    if (!TryParseOptionalDate(arguments, "toDate", out var toDate, out var toDateError))
    {
        WriteError(writer, id, -32602, toDateError!, jsonOptions);
        return;
    }

    if (!TryParseOptionalTime(arguments, "fromTime", out var fromTime, out var fromTimeError))
    {
        WriteError(writer, id, -32602, fromTimeError!, jsonOptions);
        return;
    }

    if (!TryParseOptionalTime(arguments, "toTime", out var toTime, out var toTimeError))
    {
        WriteError(writer, id, -32602, toTimeError!, jsonOptions);
        return;
    }

    var pipeName = GetPipeName(arguments);
    var response = await SendLivePipeRequestAsync(new
    {
        command = "set_time_filter",
        listIndex,
        fromDate,
        toDate,
        fromTime,
        toTime
    }, pipeName, jsonOptions);

    HandleLivePipeResponse(writer, id, response, jsonOptions);
}

static bool TryParseOptionalDate(JsonObject? arguments, string propertyName, out DateTime? value, out string? error)
{
    value = null;
    error = null;

    if (arguments?[propertyName] is not JsonNode node || node.GetValueKind() == JsonValueKind.Null)
    {
        return true;
    }

    var text = node.GetValue<string>();
    if (string.IsNullOrWhiteSpace(text))
    {
        return true;
    }

    if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
    {
        error = $"Tool argument '{propertyName}' is not a valid date.";
        return false;
    }

    value = parsed;
    return true;
}

static bool TryParseOptionalTime(JsonObject? arguments, string propertyName, out TimeOnly? value, out string? error)
{
    value = null;
    error = null;

    if (arguments?[propertyName] is not JsonNode node || node.GetValueKind() == JsonValueKind.Null)
    {
        return true;
    }

    var text = node.GetValue<string>();
    if (string.IsNullOrWhiteSpace(text))
    {
        return true;
    }

    if (!TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
    {
        error = $"Tool argument '{propertyName}' is not a valid time.";
        return false;
    }

    value = parsed;
    return true;
}

static async Task<JsonObject?> SendLivePipeRequestAsync(object request, string pipeName, JsonSerializerOptions jsonOptions)
{
    try
    {
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(1500);

        using var requestWriter = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true
        };
        using var responseReader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);

        await requestWriter.WriteLineAsync(JsonSerializer.Serialize(request, jsonOptions));

        var responseLine = await responseReader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(responseLine))
        {
            return new JsonObject
            {
                ["success"] = false,
                ["error"] = "No response from running LogAlyzer app."
            };
        }

        return JsonNode.Parse(responseLine) as JsonObject;
    }
    catch (TimeoutException)
    {
        return new JsonObject
        {
            ["success"] = false,
            ["error"] = "Could not connect to running LogAlyzer app (named pipe timeout)."
        };
    }
    catch (IOException ex)
    {
        return new JsonObject
        {
            ["success"] = false,
            ["error"] = $"Live IPC I/O error: {ex.Message}"
        };
    }
}

static void HandleLivePipeResponse(StreamWriter writer, JsonNode id, JsonObject? responseObject, JsonSerializerOptions jsonOptions)
{
    if (responseObject is null)
    {
        WriteError(writer, id, -32000, "Invalid response from running LogAlyzer app.", jsonOptions);
        return;
    }

    var success = responseObject["success"]?.GetValue<bool>() == true;
    if (!success)
    {
        var errorMessage = responseObject["error"]?.GetValue<string>() ?? "Unknown live IPC error.";
        WriteError(writer, id, -32001, errorMessage, jsonOptions);
        return;
    }

    WriteToolResult(writer, id, responseObject["data"] ?? new JsonObject(), jsonOptions);
}

static string GetPipeName(JsonObject? arguments)
{
    var pipeName = "LogAlyzer.LiveTools";
    if (arguments?["pipeName"] is JsonNode pipeNameNode)
    {
        var requestedPipeName = pipeNameNode.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(requestedPipeName))
        {
            pipeName = requestedPipeName;
        }
    }

    return pipeName;
}

static bool TryReadMessage(Stream input, out string payload)
{
    payload = string.Empty;
    var contentLength = 0;

    while (true)
    {
        var line = ReadHeaderLine(input);
        if (line is null)
        {
            return false;
        }

        if (line.Length == 0)
        {
            break;
        }

        const string header = "Content-Length:";
        if (line.StartsWith(header, StringComparison.OrdinalIgnoreCase))
        {
            var value = line[header.Length..].Trim();
            if (!int.TryParse(value, out contentLength) || contentLength <= 0)
            {
                return false;
            }
        }
    }

    if (contentLength <= 0)
    {
        return false;
    }

    var buffer = new byte[contentLength];
    var totalRead = 0;
    while (totalRead < contentLength)
    {
        var read = input.Read(buffer, totalRead, contentLength - totalRead);
        if (read == 0)
        {
            return false;
        }

        totalRead += read;
    }

    payload = Encoding.UTF8.GetString(buffer);
    return true;
}

static string? ReadHeaderLine(Stream input)
{
    var bytes = new List<byte>();

    while (true)
    {
        var value = input.ReadByte();
        if (value < 0)
        {
            return bytes.Count == 0 ? null : Encoding.ASCII.GetString(bytes.ToArray()).TrimEnd('\r');
        }

        if (value == '\n')
        {
            break;
        }

        bytes.Add((byte)value);
    }

    return Encoding.ASCII.GetString(bytes.ToArray()).TrimEnd('\r');
}

static void WriteResult(StreamWriter writer, JsonNode id, object result, JsonSerializerOptions jsonOptions)
{
    var response = new
    {
        jsonrpc = "2.0",
        id,
        result
    };

    WriteMessage(writer, JsonSerializer.Serialize(response, jsonOptions));
}

static void WriteError(StreamWriter writer, JsonNode id, int code, string message, JsonSerializerOptions jsonOptions)
{
    var response = new
    {
        jsonrpc = "2.0",
        id,
        error = new
        {
            code,
            message
        }
    };

    WriteMessage(writer, JsonSerializer.Serialize(response, jsonOptions));
}

static void WriteMessage(StreamWriter writer, string json)
{
    var contentBytes = Encoding.UTF8.GetByteCount(json);
    writer.Write($"Content-Length: {contentBytes}\r\n\r\n");
    writer.Write(json);
    writer.Flush();
}
