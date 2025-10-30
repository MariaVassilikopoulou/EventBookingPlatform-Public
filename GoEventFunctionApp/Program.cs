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

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults() // Handles triggers like Service Bus, Timer, etc.
    .ConfigureServices(services =>
    {
        // Add Application Insights if needed
        // services.AddApplicationInsightsTelemetryWorkerService();
    })
    .Build();

host.Run();
