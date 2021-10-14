// See https://aka.ms/new-console-template for more information
using SnapSecret.SecretsProviders.AzureKeyVault;

Console.WriteLine("Hello, World!");

var settings = new AzureKeyVaultInfrastructureProviderSettings
{
    ProjectName = "ZackDevSnapSecret",
    StackName = "ZackDev",
    Location = "WestUS"
};

var provider  = new AzureKeyVaultInfrastructureProvider(settings);

await provider.BuildAsync();
//await provider.DestroyAsync();
