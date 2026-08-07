using System.IO.Pipes;
using System.IO;
using System.Text;
using System.Text.Json;

namespace LogAlyzer.Services.LiveIpc;

public sealed class LiveToolPipeServer : IDisposable
{
    public const string DefaultPipeName = "LogAlyzer.LiveTools";

    private readonly string _pipeName;
    private readonly Func<LivePipeRequest, Task<object>> _requestHandler;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private CancellationTokenSource? _cts;
    private Task? _serverTask;

    public LiveToolPipeServer(Func<LivePipeRequest, Task<object>> requestHandler, string pipeName = DefaultPipeName)
    {
        _requestHandler = requestHandler ?? throw new ArgumentNullException(nameof(requestHandler));
        _pipeName = string.IsNullOrWhiteSpace(pipeName) ? DefaultPipeName : pipeName;
    }

    public void Start()
    {
        if (_serverTask is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _serverTask = Task.Run(() => RunServerAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (_cts is null || _serverTask is null)
        {
            return;
        }

        _cts.Cancel();

        try
        {
            await _serverTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _serverTask = null;
        }
    }

    private async Task RunServerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var pipe = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                await HandleConnectionAsync(pipe, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
            }
        }
    }

    private async Task HandleConnectionAsync(System.IO.Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new System.IO.StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        using var writer = new System.IO.StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true
        };

        var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(line))
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(LivePipeResponse.CreateError("Empty request."), _jsonOptions)).ConfigureAwait(false);
            return;
        }

        LivePipeRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<LivePipeRequest>(line, _jsonOptions);
        }
        catch
        {
            request = null;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Command))
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(LivePipeResponse.CreateError("Invalid request."), _jsonOptions)).ConfigureAwait(false);
            return;
        }

        try
        {
            var result = await _requestHandler(request).ConfigureAwait(false);
            await writer.WriteLineAsync(JsonSerializer.Serialize(LivePipeResponse.CreateSuccess(result), _jsonOptions)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(LivePipeResponse.CreateError(ex.Message), _jsonOptions)).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        if (_serverTask is null)
        {
            _cts?.Dispose();
            _cts = null;
            return;
        }

        StopAsync().GetAwaiter().GetResult();
    }
}

public sealed class LivePipeRequest
{
    public string Command { get; set; } = string.Empty;
    public int? MaxEntriesPerList { get; set; }
    public int? MaxTotalEntries { get; set; }
    public int? ListIndex { get; set; }
    public int? LineNumber { get; set; }
    public string[]? FilePaths { get; set; }
    public string? FilterText { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public TimeOnly? FromTime { get; set; }
    public TimeOnly? ToTime { get; set; }
}

public sealed class LivePipeResponse
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public object? Data { get; init; }

    public static LivePipeResponse CreateSuccess(object? data) => new()
    {
        Success = true,
        Data = data
    };

    public static LivePipeResponse CreateError(string error) => new()
    {
        Success = false,
        Error = error
    };
}
