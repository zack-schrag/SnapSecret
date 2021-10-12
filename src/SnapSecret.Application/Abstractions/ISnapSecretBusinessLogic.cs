using SnapSecret.Domain;

namespace SnapSecret.Application.Abstractions
{
    public interface ISnapSecretBusinessLogic
    {
        Task<(Guid?, SnapSecretError?)> SubmitSecretAsync(IShareableTextSecret secret);
        Task<(IShareableTextSecret?, SnapSecretError?)> AccessSecretAsync(Guid secretId);
    }
}
