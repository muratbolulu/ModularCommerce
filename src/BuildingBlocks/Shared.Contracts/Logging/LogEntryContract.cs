namespace Shared.Contracts.Logging;

public sealed record LogEntryContract(
    string ServiceName,
    string Level,
    string Message,
    DateTime OccurredAtUtc,
    Dictionary<string, object?>? Metadata);
