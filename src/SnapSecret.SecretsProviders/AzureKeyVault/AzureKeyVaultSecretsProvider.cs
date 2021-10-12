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
            _logger = logger;
            _secretClient = new SecretClient(
                vaultUri: new Uri(_configuration.Value.KeyVaultUri),
                credential: new DefaultAzureCredential());
        }

        public async Task<SnapSecretError?> ExpireSecretAsync(Guid secretId)
        {
            try
            {
                var deleteOperation = await _secretClient.StartDeleteSecretAsync(Convert.ToString(secretId));

                await deleteOperation.WaitForCompletionAsync();

                var deletedSecret = deleteOperation.Value;

                await _secretClient.PurgeDeletedSecretAsync(deletedSecret.Name);
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

        public async Task<(IShareableTextSecret?, SnapSecretError?)> GetSecretAsync(Guid secretId)
        {
            try
            {
                var secret = await _secretClient.GetSecretAsync(Convert.ToString(secretId));

                return (
                    new ShareableTextSecret(secret.Value.Value),
                    default
                );
            }
            catch (RequestFailedException e)
            {
                _logger.LogError(e, "Failed to get secret {SecretId} using provider {Provider}.", secretId, GetType());

                return (
                    default,
                    new SnapSecretError(SnapSecretErrorType.ProviderRequestError)
                    .WithException(e)
                    .WithUserMessage($"Failed to get secret {secretId}")
                );
            }
        }

        public async Task<(Guid?, SnapSecretError?)> SubmitSecretAsync(IShareableTextSecret secret)
        {
            var secretId = Guid.NewGuid();
            var secretName = Convert.ToString(secretId);

            try
            {
                var keyVaultSecret = new KeyVaultSecret(secretName, secret.Text);

                KeyVaultSecret newSecret = await _secretClient.SetSecretAsync(keyVaultSecret);

                var secretProperties = new SecretProperties(newSecret.Id)
                {
                    ContentType = "text/plain",
                    Enabled = true,
                    ExpiresOn = DateTimeOffset.UtcNow + secret.ExpireIn
                };

                SecretProperties newSecretProperties = await _secretClient.UpdateSecretPropertiesAsync(secretProperties);

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