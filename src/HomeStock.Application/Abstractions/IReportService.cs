using HomeStock.Application.Models;

namespace HomeStock.Application.Abstractions;

public interface IReportService
{
    /// <summary>Builds an inventory/insurance report for the given filter (category/location).</summary>
    Task<ReportDto> GenerateAsync(ReportFilter filter, CancellationToken ct = default);
}
