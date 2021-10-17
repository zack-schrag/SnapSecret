// See https://aka.ms/new-console-template for more information
using SnapSecret.Infrastructure.Cli;

Console.WriteLine("Hello, World!");

var settings = new AzureKeyVaultInfrastructureProviderSettings("ZackDev", "WestUS", "ZackDevSnapSecret");

var provider  = new AzureKeyVaultInfrastructureProvider(settings);

//await provider.BuildAsync();
await provider.DestroyAsync();
