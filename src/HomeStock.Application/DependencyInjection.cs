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
        return services;
    }
}
