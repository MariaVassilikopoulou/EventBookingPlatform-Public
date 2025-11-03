//using Microsoft.Extensions.Hosting;
//using Microsoft.Azure.Functions.Worker.Extensions.ServiceBus;
//using Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore;
//using Microsoft.Azure.Functions.Worker;
//using Microsoft.Extensions.DependencyInjection;

//var host = Host.CreateDefaultBuilder(args)

//    .ConfigureFunctionsWorkerDefaults((IFunctionsWorkerApplicationBuilder workerApplicationBuilder) =>
//    {

//    })

//    .ConfigureFunctionsWebApplication()


//    .ConfigureServices(services =>
//    {

//    })
//    .Build();

//host.Run();

using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

var keyVaultName = "KeyVault-GoEvent";
var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");

var host = new HostBuilder()
    .ConfigureAppConfiguration((context, config) =>
    {
        // Load local settings (for development)
        config.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
        config.AddEnvironmentVariables();

        // Build what we have so far
        var builtConfig = config.Build();

        // ✅ Add Key Vault only if running in Azure (environment variable check)
        if (!context.HostingEnvironment.IsDevelopment())
        {
            config.AddAzureKeyVault(keyVaultUri, new DefaultAzureCredential());
        }
    })
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Optional: telemetry, logging, etc.
    })
    .Build();

host.Run();
