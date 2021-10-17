using Pulumi;
using Pulumi.AzureNative.Resources;
using Storage = Pulumi.AzureNative.Storage;
using SnapSecret.Application.Abstractions;
using SnapSecret.Infrastructure.Core;
using Pulumi.Automation;

namespace SnapSecret.Infrastructure.Cli
{
    public class AzureKeyVaultInfrastructureProvider : ISecretsInfrastructureProvider
    {
        private readonly AzureKeyVaultInfrastructureProviderSettings _settings;

        public AzureKeyVaultInfrastructureProvider(AzureKeyVaultInfrastructureProviderSettings settings)
        {
            _settings = settings;
        }

        public async Task BuildAsync()
        {
            var program = GetProgram(string.Empty);
            var stack = await GetStackAsync(program);

            var outputs = await stack.GetOutputsAsync();
            var outputKeyVaultUri = outputs.Any() ? (string)outputs["KeyVaultUri"].Value : string.Empty;

            var keyVaultUri = await BuildInternalAsync(outputKeyVaultUri);

            if (string.IsNullOrEmpty(outputKeyVaultUri))
            {
                await BuildInternalAsync(keyVaultUri);
            }
        }

        public async Task DestroyAsync()
        {
            var program = GetProgram(string.Empty);
            var stack = await GetStackAsync(program);

            var result = await stack.DestroyAsync();

            Console.WriteLine(result.StandardOutput);
        }

        private PulumiFn GetProgram(string keyVaultUri)
        {
            return PulumiFn.Create<AzureSnapSecretStack>();
        }

        private async Task<WorkspaceStack> GetStackAsync(PulumiFn program)
        {
            var stackArgs = new InlineProgramArgs(_settings.ProjectName, _settings.StackName, program);

            var stack = await LocalWorkspace.CreateOrSelectStackAsync(stackArgs);

            await stack.Workspace.InstallPluginAsync("azure-native", "v1.36.0");

            return stack;
        }

        private async Task<string> BuildInternalAsync(string keyVaultUri)
        {
            var program = GetProgram(keyVaultUri);

            var stack = await GetStackAsync(program);

            var upResult = await stack.UpAsync();

            var outputs = await stack.GetOutputsAsync();

            var outputKeyVaultUri = outputs.Any() ? (string)outputs["KeyVaultUri"].Value : string.Empty;

            Console.WriteLine(upResult.StandardOutput);

            return outputKeyVaultUri;
        }

        private static Output<string> SignedBlobReadUrl(Storage.Blob blob, Storage.BlobContainer container, Storage.StorageAccount account, ResourceGroup resourceGroup)
        {
            return Output.Tuple(
                blob.Name, container.Name, account.Name, resourceGroup.Name).Apply(t =>
                {
                    (string blobName, string containerName, string accountName, string resourceGroupName) = t;

                    var blobSAS = Storage.ListStorageAccountServiceSAS.InvokeAsync(new Storage.ListStorageAccountServiceSASArgs
                    {
                        AccountName = accountName,
                        Protocols = Storage.HttpProtocol.Https,
                        SharedAccessStartTime = "2021-01-01",
                        SharedAccessExpiryTime = "2030-01-01",
                        Resource = Storage.SignedResource.C,
                        ResourceGroupName = resourceGroupName,
                        Permissions = Storage.Permissions.R,
                        CanonicalizedResource = "/blob/" + accountName + "/" + containerName,
                        ContentType = "application/json",
                        CacheControl = "max-age=5",
                        ContentDisposition = "inline",
                        ContentEncoding = "deflate",
                    });
                    return Output.Format($"https://{accountName}.blob.core.windows.net/{containerName}/{blobName}?{blobSAS.Result.ServiceSasToken}");
                });
        }

        private static Output<string> GetConnectionString(Input<string> resourceGroupName, Input<string> accountName)
        {
            // Retrieve the primary storage account key.
            var storageAccountKeys = Output.All(resourceGroupName, accountName).Apply(t =>
            {
                var resourceGroupName = t[0];
                var accountName = t[1];
                return Storage.ListStorageAccountKeys.InvokeAsync(
                    new Storage.ListStorageAccountKeysArgs
                    {
                        ResourceGroupName = resourceGroupName,
                        AccountName = accountName
                    });
            });
            return storageAccountKeys.Apply(keys =>
            {
                var primaryStorageKey = keys.Keys[0].Value;

                // Build the connection string to the storage account.
                return Output.Format($"DefaultEndpointsProtocol=https;AccountName={accountName};AccountKey={primaryStorageKey}");
            });
        }
    }

    public class AzureKeyVaultInfrastructureProviderSettings
    {
        public string StackName { get; }
        public string ProjectName { get; }
        public string Location { get; }

        public AzureKeyVaultInfrastructureProviderSettings(string stackName, string location) :
            this(stackName, location, "SnapSecret")
        {
        }

        public AzureKeyVaultInfrastructureProviderSettings(string stackName, string location, string projectName)
        {
            StackName = stackName;
            ProjectName = projectName;
            Location = location;
        }
    }
}
