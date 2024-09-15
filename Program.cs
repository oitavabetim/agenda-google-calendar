using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OitavaAgenda.Options;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddOptions<GoogleCalendarOitavaBetimOptions>()
            .Configure<IConfiguration>((settings, configuration) =>
            {
                configuration.GetSection("GoogleCalendar:OitavaBetim:Credential").Bind(settings);
            });
        services.AddOptions<GoogleCalendarOitavaBetimSpacesOptions>()
            .Configure<IConfiguration>((settings, configuration) =>
            {
                configuration.GetSection("GoogleCalendar:OitavaBetim:Spaces").Bind(settings);
            });
    })
    .Build();

host.Run();
