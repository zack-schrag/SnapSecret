using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SnapSecret.Application;
using SnapSecret.Application.Abstractions;
using SnapSecret.AzureFunctions;
using SnapSecret.SecretsProviders.AzureKeyVault;


[assembly: FunctionsStartup(typeof(Startup))]
namespace SnapSecret.AzureFunctions
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var configuration = builder.GetContext().Configuration;

            Serilog.Debugging.SelfLog.Enable(System.Console.Error);

            var logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();
            builder.Services
                .AddLogging(loggingBuilder => loggingBuilder.AddSerilog(logger))
                .AddTransient<ISnapSecretBusinessLogic, SnapSecretBusinessLogic>()
                .AddAzureKeyVaultProvider(configuration);

            builder.Services
                .Configure<SlackConfiguration>(configuration.GetSection(SlackConfiguration.Section));
        }
    }
}
