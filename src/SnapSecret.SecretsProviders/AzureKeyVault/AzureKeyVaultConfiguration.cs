namespace SnapSecret.SecretsProviders.AzureKeyVault
{
    public class AzureKeyVaultConfiguration
    {
        internal const string Section = "AzureKeyVaultSecretsProvider";
        public string? KeyVaultUri { get; set; }
    }
}
