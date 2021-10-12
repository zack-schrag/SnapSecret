using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnapSecret.SecretsProviders.AzureKeyVault
{
    public class AzureKeyVaultConfiguration
    {
        internal const string Section = "AzureKeyVaultSecretsProvider";
        public string KeyVaultUri { get; set; }
    }
}
