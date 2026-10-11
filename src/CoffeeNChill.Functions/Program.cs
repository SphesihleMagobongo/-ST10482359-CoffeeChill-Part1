using CoffeeNChill.Functions.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddSingleton<IMenuItemRepository, MenuItemRepository>();
        services.AddSingleton<IOrderRepository, OrderRepository>();
        services.AddSingleton<IOrderQueueProducer, OrderQueueProducer>();
        services.AddSingleton<IStaffDocumentStore>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var mode = config["Storage:Mode"] ?? "Azurite";

            return mode.Equals("AzureFiles", StringComparison.OrdinalIgnoreCase)
                ? new AzureFilesDocumentStore(config, loggerFactory.CreateLogger<AzureFilesDocumentStore>())
                : (IStaffDocumentStore)new AzuriteDocumentStore(config, loggerFactory.CreateLogger<AzuriteDocumentStore>());
        });
    })
    .Build();

await host.RunAsync();
