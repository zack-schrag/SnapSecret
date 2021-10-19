using SnapSecret.Domain;

namespace SnapSecret.Application
{
    public interface ISecretsProvider
    {
        Task<(string?, SnapSecretError?)> SetSecretAsync(IShareableTextSecret secret);
        Task<(IShareableTextSecret?, SnapSecretError?)> GetSecretAsync(string secretId);
        Task<SnapSecretError?> ExpireSecretAsync(string secretId);
    }
}