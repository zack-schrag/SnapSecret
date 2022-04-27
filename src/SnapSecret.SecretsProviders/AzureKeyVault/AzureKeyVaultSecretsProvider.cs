using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SnapSecret.Application;
using SnapSecret.Domain;

namespace SnapSecret.SecretsProviders.AzureKeyVault
{
    public class AzureKeyVaultSecretsProvider : ISecretsProvider
    {
        private readonly IOptionsSnapshot<AzureKeyVaultConfiguration> _configuration;
        private readonly ILogger<AzureKeyVaultSecretsProvider> _logger;
        private readonly SecretClient _secretClient;

        public AzureKeyVaultSecretsProvider(
            IOptionsSnapshot<AzureKeyVaultConfiguration> configuration,
            ILogger<AzureKeyVaultSecretsProvider> logger)
        {
            _configuration = configuration;

            if (_configuration?.Value?.KeyVaultUri is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            _logger = logger;
            _secretClient = new SecretClient(
                vaultUri: new Uri(_configuration.Value.KeyVaultUri),
                credential: new DefaultAzureCredential());
        }

        public async Task<SnapSecretError?> ExpireSecretAsync(string secretId)
        {
            try
            {
                _logger.LogInformation("Expiring secret {SecretId}", secretId);

                var deleteOperation = await _secretClient.StartDeleteSecretAsync(Convert.ToString(secretId));

                await deleteOperation.WaitForCompletionAsync();

                var deletedSecret = deleteOperation.Value;

                await _secretClient.PurgeDeletedSecretAsync(deletedSecret.Name);

                _logger.LogInformation("Successfully removed secret {SecretId}", secretId);
            }
            catch (RequestFailedException e)
            {
                _logger.LogError(e, "Failed to expire secret {SecretId} using provider {Provider}.", secretId, GetType());

                return new SnapSecretError(SnapSecretErrorType.ProviderRequestError)
                    .WithException(e)
                    .WithUserMessage("Failed to expire secret");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to expire secret {SecretId} using provider {Provider}.", secretId, GetType());

                return new SnapSecretError(SnapSecretErrorType.Unknown)
                    .WithException(e)
                    .WithUserMessage($"Failed to expire secret due to an unknown secrets provider error");
            }

            return default;
        }

        public async Task<(IShareableTextSecret?, SnapSecretError?)> GetSecretAsync(string secretId)
        {
            try
            {
                var secret = await _secretClient.GetSecretAsync(secretId);

                var expiresOn = secret.Value.Properties.ExpiresOn.GetValueOrDefault(DateTimeOffset.UtcNow.AddYears(999));

                if (expiresOn.CompareTo(DateTimeOffset.UtcNow) < 0)
                {
                    _logger.LogError("Failed to get secret {SecretId} using provider {Provider}. Secret is expired", secretId, GetType());

                    return (
                        default,
                        new SnapSecretError(SnapSecretErrorType.SecretExpiredOrNotFound)
                        .WithUserMessage($"Failed to get secret {secretId}. Secret is either expired or does not exist.")
                    );
                }

                return (
                    new ShareableTextSecret(secret.Value.Value),
                    default
                );
            }
            catch (RequestFailedException e)
            {
                _logger.LogError(e, "Failed to get secret {SecretId} using provider {Provider}. Reason: {Exception}", secretId, GetType(), e.Message);

                return (
                    default,
                    new SnapSecretError(SnapSecretErrorType.ProviderRequestError)
                    .WithException(e)
                    .WithUserMessage($"Failed to get secret {secretId}")
                );
            }
        }

        public async Task<(string?, SnapSecretError?)> SetSecretAsync(IShareableTextSecret secret)
        {
            var secretId = secret.Id;

            try
            {
                var keyVaultSecret = new KeyVaultSecret(secretId, secret.Text);

                KeyVaultSecret newSecret = await _secretClient.SetSecretAsync(keyVaultSecret);

                var expiresIn = secret.ExpireIn.GetValueOrDefault(TimeSpan.FromDays(5));

                var secretProperties = new SecretProperties(newSecret.Id)
                {
                    ContentType = "text/plain",
                    Enabled = true,
                    ExpiresOn = DateTimeOffset.UtcNow.Add(expiresIn)
                };

                _logger.LogDebug("Created secret {SecretId}", newSecret.Name);
                SecretProperties newSecretProperties = await _secretClient.UpdateSecretPropertiesAsync(secretProperties);
                _logger.LogDebug("Updated secret {SecretId} properties. ExpiresIn: {ExpiresIn}", newSecret.Name, expiresIn.ToString());
                return (
                    secretId,
                    default
                );
            }
            catch (RequestFailedException e)
            {
                _logger.LogError(e, "Failed to set secret using provider {Provider}.", GetType());

                return (
                    default,
                    new SnapSecretError(SnapSecretErrorType.ProviderRequestError)
                    .WithException(e)
                    .WithUserMessage($"Failed to create secret due to a secrets provider error")
                );
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to set secret using provider {Provider}.", GetType());

                return (
                    default,
                    new SnapSecretError(SnapSecretErrorType.Unknown)
                    .WithException(e)
                    .WithUserMessage($"Failed to create secret due to an unknown secrets provider error")
                );
            }
        }
    }
}