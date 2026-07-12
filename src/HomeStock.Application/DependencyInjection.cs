using HomeStock.Application.Abstractions;
using HomeStock.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HomeStock.Application;

/// <summary>Registers application-layer services. Infrastructure supplies the implementations
/// of the abstractions (DbContext, file storage, code generator, current user).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<IItemService, ItemService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IItemHistoryService, ItemHistoryService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<IImportExportService, ImportExportService>();
        services.AddScoped<ILoanService, LoanService>();
        services.AddScoped<IInventoryLedgerService, InventoryLedgerService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IBackupRestoreService, BackupRestoreService>();
        return services;
    }
}
