using Pulumi;
using Pulumi.AzureNative.Insights;
using Pulumi.AzureNative.KeyVault;
using Pulumi.AzureNative.KeyVault.Inputs;
using Pulumi.AzureNative.Resources;
using Storage = Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Web;
using ManagedServiceIdentityType = Pulumi.AzureNative.Web.ManagedServiceIdentityType;
using Pulumi.AzureNative.Web.Inputs;
using Pulumi.Random;

namespace SnapSecret.Infrastructure.Core
{
    public class AzureSnapSecretStack : Stack
    {
        private static readonly Dictionary<string, string> StackNameCleaned = new Dictionary<string, string>
        {
            { "Production", "prod" }
        };

        public AzureSnapSecretStack()
        {
            var stackName = Pulumi.Deployment.Instance.StackName;
            var stackNameCleaned = StackNameCleaned.GetValueOrDefault(stackName) ?? stackName.ToLowerInvariant().Substring(0, 3);
            var location = new Config("azure-native").Require("location");
            var slackClientId = new Config("SnapSecret").Require("SlackClientId");
            var slackClientSecret = new Config("SnapSecret").Require("SlackClientSecret");

            var resourceGroup = new ResourceGroup("ResourceGroup", new ResourceGroupArgs
            {
                ResourceGroupName = $"rg-snap-{stackNameCleaned}",
                Location = location
            });

            var plan = new AppServicePlan("AppServicePlan", new AppServicePlanArgs()
            {
                ResourceGroupName = resourceGroup.Name,
                Location = resourceGroup.Location,
                Name = $"app-svc-plan-snap-{stackNameCleaned}",
                Reserved = true,
                Sku = new SkuDescriptionArgs
                {
                    Name = "Y1",
                    Tier = "Dynamic"
                }
            });

            var storageAccount = new Storage.StorageAccount("StorageAccount", new Storage.StorageAccountArgs
            {
                ResourceGroupName = resourceGroup.Name,
                AccountName = $"sasnap{stackNameCleaned}".ToLower(),
                Location = resourceGroup.Location,
                Sku = new Storage.Inputs.SkuArgs
                {
                    Name = Storage.SkuName.Standard_LRS
                },
                Kind = Storage.Kind.StorageV2
            });

            var container = new Storage.BlobContainer("zips-container", new Storage.BlobContainerArgs
            {
                AccountName = storageAccount.Name,
                PublicAccess = Storage.PublicAccess.None,
                ResourceGroupName = resourceGroup.Name,
            });

            var currentDir = Directory.GetCurrentDirectory();

            Console.WriteLine($"Current working directory: {currentDir}");

            string prefix = Path.Combine("..", "..", "src");

            if (currentDir.Contains("net6.0"))
            {
                prefix = Path.Combine("..", "..", "..", "..", "..", "src");
            }

            var filePath = Path.Combine(prefix, "SnapSecret.AzureFunctions", "bin", "Release", "net6.0");

            Console.WriteLine($"File path: {filePath}");

            var fileArchive = new FileArchive(filePath);

            var blob = new Storage.Blob("zip", new Storage.BlobArgs
            {
                AccountName = storageAccount.Name,
                ContainerName = container.Name,
                ResourceGroupName = resourceGroup.Name,
                Type = Storage.BlobType.Block,
                Source = fileArchive
            });

            var codeBlobUrl = SignedBlobReadUrl(blob, container, storageAccount, resourceGroup);

            var appInsights = new Component("AppInsightsComponent", new ComponentArgs
            {
                Location = resourceGroup.Location,
                ResourceGroupName = resourceGroup.Name,
                ResourceName = $"app-insights-snap-{stackNameCleaned}",
                ApplicationType = "web",
                Kind = "web"
            });

            var currentStack = $"zackschrag/{Pulumi.Deployment.Instance.ProjectName}/{Pulumi.Deployment.Instance.StackName}";
            var stackReference = new StackReference(currentStack);
            var keyVaultUriOutput = stackReference.GetOutput("KeyVaultUri");
            var slackRedirectUriOutput = stackReference.GetOutput("AppHostname");

            NeedsRerun = Output.Tuple(keyVaultUriOutput, slackRedirectUriOutput).Apply(t =>
            {
                var kvUri = t.Item1 as string;
                var slackUri = t.Item2 as string;

                return Output.Create(string.IsNullOrEmpty(kvUri) || string.IsNullOrEmpty(slackUri));
            });

            var keyVaultUri = keyVaultUriOutput?.Apply(s => $"{s}");
            var slackRedirectUri = slackRedirectUriOutput?.Apply(s => $"https://{s}/api/oauth2/slack");

            var siteConfig = new SiteConfigArgs
            {
                AppSettings = new List<NameValuePairArgs>
                    {
                        new NameValuePairArgs { Name = "FUNCTIONS_WORKER_RUNTIME", Value = "dotnet" },
                        new NameValuePairArgs { Name = "FUNCTIONS_EXTENSION_VERSION", Value = "~4" },
                        new NameValuePairArgs { Name = "AZURE_FUNCTIONS_ENVIRONMENT", Value = stackName },
                        new NameValuePairArgs { Name = "AzureWebJobsStorage", Value = GetConnectionString(resourceGroup.Name, storageAccount.Name) },
                        new NameValuePairArgs { Name = "APPINSIGHTS_INSTRUMENTATIONKEY", Value = appInsights.InstrumentationKey },
                        new NameValuePairArgs { Name = "WEBSITE_RUN_FROM_PACKAGE", Value = codeBlobUrl },
                        new NameValuePairArgs { Name = "AzureKeyVaultSecretsProvider__KeyVaultUri", Value = keyVaultUri ?? Output.Create(string.Empty) },
                        new NameValuePairArgs { Name = "Slack__ClientId", Value = slackClientId },
                        new NameValuePairArgs { Name = "Slack__ClientSecret", Value = slackClientSecret },
                        new NameValuePairArgs { Name = "Slack__RedirectUri", Value = slackRedirectUri ?? Output.Create(string.Empty) }
                    },
                FtpsState = "FtpsOnly"
            };

            var app = new WebApp("WebApp", new WebAppArgs
            {
                Name = $"function-snap-{stackNameCleaned}",
                Identity = new ManagedServiceIdentityArgs
                {
                    Type = ManagedServiceIdentityType.SystemAssigned
                },
                Kind = "functionapp",
                ResourceGroupName = resourceGroup.Name,
                SiteConfig = siteConfig,
                HttpsOnly = true,
                ServerFarmId = plan.Id,
                Location = resourceGroup.Location
            });

            var tenantId = app.Identity.Apply(func => func?.TenantId ?? string.Empty);

            var keyVaultNamePrefix = $"kv-snap-{stackNameCleaned}";

            var randomKeyVaultName = new RandomPet("KeyVaultRandomName", new RandomPetArgs
            {
                Prefix = keyVaultNamePrefix,
                Length = 1
            });

            var keyVault = new Vault("KeyVault", new VaultArgs
            {
                Location = resourceGroup.Location,
                ResourceGroupName = resourceGroup.Name,
                VaultName = randomKeyVaultName.Id,
                Properties = new VaultPropertiesArgs
                {
                    AccessPolicies = new[] {
                            new AccessPolicyEntryArgs
                            {
                                ObjectId = app.Identity.Apply(func => func?.PrincipalId ?? string.Empty),
                                Permissions = new PermissionsArgs
                                {
                                    Secrets = new List<Union<string, SecretPermissions>> { "get", "set", "delete", "purge" }
                                },
                                TenantId = tenantId
                            }
                        },
                    Sku = new SkuArgs
                    {
                        Family = "A",
                        Name = SkuName.Standard
                    },
                    TenantId = tenantId,
                    EnableSoftDelete = false
                }
            });

            KeyVaultUri = keyVault.Properties.Apply(p => p.VaultUri);

            var manifestTemplate = File.ReadAllText("slack_manifest.yml");

            SlackManifest = app.DefaultHostName.Apply(hostname => manifestTemplate.Replace("{{URL}}", hostname));
        }

        [Output]
        public Output<string?> KeyVaultUri { get; set; }

        [Output]
        public Output<string?> AppHostname { get; set; }

        [Output]
        public Output<string> SlackManifest { get; set; }

        [Output]
        public Output<bool> NeedsRerun { get; set; }

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
}
