using SnapSecret.Domain;

namespace SnapSecret.Application.Abstractions
{
    public interface ISnapSecretBusinessLogic
    {
        Task<(string?, SnapSecretError?)> SubmitSecretAsync(IShareableTextSecret secret);
        Task<(IShareableTextSecret?, SnapSecretError?)> AccessSecretAsync(string secretId);
    }
}
