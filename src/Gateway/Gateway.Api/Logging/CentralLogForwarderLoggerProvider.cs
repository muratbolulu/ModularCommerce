using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Logging;

namespace Gateway.Api.Logging;

public sealed class CentralLogForwarderLoggerProvider : ILoggerProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _serviceName;
    private readonly string _endpoint;

    public CentralLogForwarderLoggerProvider(string serviceName, string endpoint)
    {
        _serviceName = serviceName;
        _endpoint = endpoint;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
    }

    public ILogger CreateLogger(string categoryName) => new ForwarderLogger(_httpClient, _serviceName, _endpoint, categoryName);

    public void Dispose() => _httpClient.Dispose();

    private sealed class ForwarderLogger : ILogger
    {
        private readonly HttpClient _httpClient;
        private readonly string _serviceName;
        private readonly string _endpoint;
        private readonly string _categoryName;

        public ForwarderLogger(HttpClient httpClient, string serviceName, string endpoint, string categoryName)
        {
            _httpClient = httpClient;
            _serviceName = serviceName;
            _endpoint = endpoint;
            _categoryName = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var payload = new LogEntryContract(
                ServiceName: _serviceName,
                Level: MapLevel(logLevel),
                Message: formatter(state, exception),
                OccurredAtUtc: DateTime.UtcNow,
                Metadata: new Dictionary<string, object?>
                {
                    ["Category"] = _categoryName,
                    ["EventId"] = eventId.Id
                },
                Exception: exception?.ToString());

            _ = Task.Run(async () =>
            {
                try
                {
                    await _httpClient.PostAsJsonAsync(_endpoint, payload);
                }
                catch
                {
                    // Never break the request flow because of central logging failures.
                }
            });
        }

        private static string MapLevel(LogLevel logLevel) => logLevel switch
        {
            LogLevel.Warning => "WARNING",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "CRITICAL",
            _ => "INFO"
        };
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose()
        {
        }
    }
}
