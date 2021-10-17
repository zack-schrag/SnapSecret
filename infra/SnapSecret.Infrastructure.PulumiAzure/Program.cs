using System.Threading.Tasks;
using Pulumi;
using SnapSecret.Infrastructure.Core;

class Program
{
    static Task<int> Main() => Deployment.RunAsync<AzureSnapSecretStack>();
}
