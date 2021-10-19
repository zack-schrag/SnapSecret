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
            var secretIdAsString = secretId.ToString();

            var (secret, getError) = await _secretsProvider.GetSecretAsync(secretIdAsString);

            if (getError != null)
            {
                return (
                    default,
                    getError
                );
            }

            var expireError = await _secretsProvider.ExpireSecretAsync(secretIdAsString);

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
