using SnapSecret.Domain;

namespace SnapSecret.Application
{
    public interface ISecretsProvider
    {
        Task<(Guid?, SnapSecretError?)> SubmitSecretAsync(IShareableTextSecret secret);
        Task<(IShareableTextSecret?, SnapSecretError?)> GetSecretAsync(Guid secretId);
        Task<SnapSecretError?> ExpireSecretAsync(Guid secretId);
    }
}