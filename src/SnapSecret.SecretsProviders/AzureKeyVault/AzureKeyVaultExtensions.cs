using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SnapSecret.Application;

namespace SnapSecret.SecretsProviders.AzureKeyVault
{
    public static class AzureKeyVaultExtensions
    {
        public static IServiceCollection AddAzureKeyVaultProvider(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddTransient<ISecretsProvider, AzureKeyVaultSecretsProvider>();
            services.Configure<AzureKeyVaultConfiguration>(configuration.GetSection(AzureKeyVaultConfiguration.Section));
            return services;
        }
    }
}
