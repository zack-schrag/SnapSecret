using Pulumi;
using Pulumi.Automation;
using Pulumi.AzureNative.Insights;
using Pulumi.AzureNative.KeyVault;
using Pulumi.AzureNative.KeyVault.Inputs;
using Pulumi.AzureNative.Resources;
using Storage = Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Web;
using ManagedServiceIdentityType = Pulumi.AzureNative.Web.ManagedServiceIdentityType;
using Pulumi.AzureNative.Web.Inputs;
using SnapSecret.Application.Abstractions;
using SnapSecret.Infrastructure.Core;

namespace SnapSecret.SecretsProviders.AzureKeyVault
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

        //private Dictionary<string, object?> CreateResources(string keyVaultUri)
        //{
        //    var resourceGroup = new ResourceGroup("ResourceGroup", new ResourceGroupArgs
        //    {
        //        ResourceGroupName = "SnapSecret",
        //        Location = _settings.Location
        //    });

        //    var plan = new AppServicePlan("AppServicePlan", new AppServicePlanArgs()
        //    {
        //        ResourceGroupName = resourceGroup.Name,
        //        Location = resourceGroup.Location,
        //        Name = "SnapSecretAppServicePlan",
        //        Reserved = true,
        //        Sku = new SkuDescriptionArgs
        //        {
        //            Name = "Y1",
        //            Tier = "Dynamic"
        //        }
        //    });

        //    var storageAccount = new Storage.StorageAccount("StorageAccount", new Storage.StorageAccountArgs
        //    {
        //        ResourceGroupName = resourceGroup.Name,
        //        AccountName = $"{_settings.ProjectName}{_settings.StackName}".ToLower(),
        //        Location = resourceGroup.Location,
        //        Sku = new Storage.Inputs.SkuArgs
        //        {
        //            Name = Storage.SkuName.Standard_LRS
        //        },
        //        Kind = Storage.Kind.StorageV2
        //    });

        //    var container = new Storage.BlobContainer("zips-container", new Storage.BlobContainerArgs
        //    {
        //        AccountName = storageAccount.Name,
        //        PublicAccess = Storage.PublicAccess.None,
        //        ResourceGroupName = resourceGroup.Name,
        //    });

        //    var currentDir = Directory.GetCurrentDirectory();

        //    string prefix = "../";

        //    if (currentDir.Contains("net6.0"))
        //    {
        //        prefix = "../../../../";
        //    }

        //    var blob = new Storage.Blob("zip", new Storage.BlobArgs
        //    {
        //        AccountName = storageAccount.Name,
        //        ContainerName = container.Name,
        //        ResourceGroupName = resourceGroup.Name,
        //        Type = Storage.BlobType.Block,
        //        Source = new FileArchive($"{prefix}SnapSecret.AzureFunctions/bin/Debug/net6.0")
        //    });

        //    var codeBlobUrl = SignedBlobReadUrl(blob, container, storageAccount, resourceGroup);

        //    var appInsights = new Component("AppInsightsComponent", new ComponentArgs
        //    {
        //        Location = resourceGroup.Location,
        //        ResourceGroupName = resourceGroup.Name,
        //        ResourceName = "SnapSecretAppInsights",
        //        ApplicationType = "web",
        //        Kind = "web"
        //    });

        //    var siteConfig = new SiteConfigArgs
        //    {
        //        AppSettings = new List<NameValuePairArgs>
        //            {
        //                new NameValuePairArgs { Name = "FUNCTIONS_WORKER_RUNTIME", Value = "dotnet" },
        //                new NameValuePairArgs { Name = "FUNCTIONS_EXTENSION_VERSION", Value = "~4" },
        //                new NameValuePairArgs { Name = "AZURE_FUNCTIONS_ENVIRONMENT", Value = _settings.StackName },
        //                new NameValuePairArgs { Name = "AzureWebJobsStorage", Value = GetConnectionString(resourceGroup.Name, storageAccount.Name) },
        //                new NameValuePairArgs { Name = "APPLICATIONINSIGHTS_CONNECTION_STRING", Value = Output.Format($"InstrumentationKey={appInsights.InstrumentationKey}") },
        //                new NameValuePairArgs { Name = "WEBSITE_RUN_FROM_PACKAGE", Value = codeBlobUrl },
        //                new NameValuePairArgs { Name = "AzureKeyVaultSecretsProvider__KeyVaultUri", Value = keyVaultUri}
        //            },
        //        FtpsState = "FtpsOnly"
        //    };

        //    var app = new WebApp("WebApp", new WebAppArgs
        //    {
        //        Name = "SnapSecretFunctionApp",
        //        Identity = new ManagedServiceIdentityArgs
        //        {
        //            Type = ManagedServiceIdentityType.SystemAssigned
        //        },
        //        Kind = "functionapp",
        //        ResourceGroupName = resourceGroup.Name,
        //        SiteConfig = siteConfig,
        //        HttpsOnly = true,
        //        ServerFarmId = plan.Id,
        //        Location = resourceGroup.Location
        //    });

        //    var tenantId = app.Identity.Apply(func => func.TenantId);

        //    var keyVault = new Vault("KeyVault", new VaultArgs
        //    {
        //        Location = resourceGroup.Location,
        //        ResourceGroupName = resourceGroup.Name,
        //        VaultName = "SnapSecretKeyVault",
        //        Properties = new VaultPropertiesArgs
        //        {
        //            AccessPolicies = new[] {
        //                    new AccessPolicyEntryArgs
        //                    {
        //                        ObjectId = app.Identity.Apply(func => func.PrincipalId),
        //                        Permissions = new PermissionsArgs
        //                        {
        //                            Secrets = new List<Union<string, SecretPermissions>> { "get", "set", "delete", "purge" }
        //                        },
        //                        TenantId = tenantId
        //                    }
        //                },
        //            Sku = new SkuArgs
        //            {
        //                Family = "A",
        //                Name = SkuName.Standard
        //            },
        //            TenantId = tenantId,
        //            EnableSoftDelete = true
        //        }
        //    });

        //    return new Dictionary<string, object?>
        //    {
        //        { "KeyVaultUri", keyVault.Properties.Apply(p => p.VaultUri) }
        //    };
        //}
        private static Output<string> SignedBlobReadUrl(Storage.Blob blob, Storage.BlobContainer container, Storage.StorageAccount account, ResourceGroup resourceGroup)
        {
            return Output.Tuple<string, string, string, string>(
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
            var storageAccountKeys = Output.All<string>(resourceGroupName, accountName).Apply(t =>
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
        public string StackName { get; set; }
        public string ProjectName { get; set; } = "SnapSecret";
        //public string AzureClientId { get; set; }
        //public string AzureClientSecret {  get; set; }
        //public string AzureTenantId { get; set;  }
        public string Location { get; set; }
    }
}
