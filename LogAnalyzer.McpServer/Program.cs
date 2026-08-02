using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LogAnalyzer.Models;
using LogAnalyzer.Services;
using LogAnalyzer.Services.Parsing;

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
                    name = "loganalyzer-mcp-server",
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
                        description = "Lists the parser profiles configured in the LogAnalyzer tool.",
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
                        description = "Parses a log file using the LogAnalyzer parser tooling and returns the already parsed log lines.",
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
