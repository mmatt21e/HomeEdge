namespace HomeStock.Application.Common;

/// <summary>
/// Lightweight operation result used at the application boundary to convey success,
/// validation errors, and non-fatal warnings (e.g. duplicate serial/barcode) without
/// throwing for expected conditions.
/// </summary>
public class Result
{
    public bool Succeeded { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public static Result Success(IReadOnlyList<string>? warnings = null) =>
        new() { Succeeded = true, Warnings = warnings ?? Array.Empty<string>() };

    public static Result Failure(params string[] errors) =>
        new() { Succeeded = false, Errors = errors };

    public static Result Failure(IReadOnlyList<string> errors) =>
        new() { Succeeded = false, Errors = errors };
}

/// <summary>Operation result carrying a value on success.</summary>
public class Result<T> : Result
{
    public T? Value { get; init; }

    public static Result<T> Success(T value, IReadOnlyList<string>? warnings = null) =>
        new() { Succeeded = true, Value = value, Warnings = warnings ?? Array.Empty<string>() };

    public static new Result<T> Failure(params string[] errors) =>
        new() { Succeeded = false, Errors = errors };

    public static new Result<T> Failure(IReadOnlyList<string> errors) =>
        new() { Succeeded = false, Errors = errors };
}
