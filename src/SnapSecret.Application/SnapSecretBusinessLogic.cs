using SnapSecret.Application.Abstractions;
using SnapSecret.Domain;

namespace SnapSecret.Application
{
    public class SnapSecretBusinessLogic : ISnapSecretBusinessLogic
    {
        private readonly ISecretsProvider _secretsProvider;

        public SnapSecretBusinessLogic(ISecretsProvider secretsProvider)
        {
            _secretsProvider = secretsProvider;
        }

        public async Task<(IShareableTextSecret?, SnapSecretError?)> AccessSecretAsync(string secretId)
        {
            var (secret, getError) = await _secretsProvider.GetSecretAsync(secretId);

            if (getError != null)
            {
                return (
                    default,
                    getError
                );
            }

            var expireError = await _secretsProvider.ExpireSecretAsync(secretId);

            if (expireError != null)
            {
                return (
                    default,
                    expireError
                );
            }

            return (
                secret,
                default
            );
        }

        public Task<(string?, SnapSecretError?)> SubmitSecretAsync(IShareableTextSecret secret)
        {
            return _secretsProvider.SetSecretAsync(secret);
        }
    }
}
