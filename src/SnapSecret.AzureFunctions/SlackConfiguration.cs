namespace SnapSecret.AzureFunctions
{
    public class SlackConfiguration
    {
        internal const string Section = "Slack";
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string? RedirectUri { get; set; }
    }
}
