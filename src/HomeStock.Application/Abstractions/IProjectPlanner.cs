using HomeStock.Application.Common;
using HomeStock.Application.Models;

namespace HomeStock.Application.Abstractions;

/// <summary>
/// Turns a plain-language project description into a proposed bill of materials (tools +
/// materials with quantities). Only the description is sent to the model — matching against the
/// user's actual inventory happens locally, so real locations are never hallucinated.
/// Implemented over any OpenAI-compatible text endpoint (Infrastructure).
/// </summary>
public interface IProjectPlanner
{
    bool IsConfigured { get; }
    string ProviderLabel { get; }

    Task<Result<IReadOnlyList<NeededItem>>> ProposeItemsAsync(string description, CancellationToken ct = default);
}
