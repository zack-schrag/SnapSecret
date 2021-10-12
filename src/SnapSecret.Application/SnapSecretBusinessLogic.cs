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

        public async Task<(IShareableTextSecret?, SnapSecretError?)> AccessSecretAsync(Guid secretId)
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

        public Task<(Guid?, SnapSecretError?)> SubmitSecretAsync(IShareableTextSecret secret)
        {
            return _secretsProvider.SubmitSecretAsync(secret);
        }
    }
}
