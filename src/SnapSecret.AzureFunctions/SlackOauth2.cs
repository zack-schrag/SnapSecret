using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
using SnapSecret.Application;
using Microsoft.Extensions.Options;
using SnapSecret.Domain;

namespace SnapSecret.AzureFunctions
{
    public class SlackOauth2
    {
        private const string SlackHost = "https://slack.com";
        private const string SlackTokenExchangePath = "/api/oauth.v2.access";
        private readonly IOptionsSnapshot<SlackConfiguration> _slackConfig;
        private readonly ISecretsProvider _secretsProvider;

        public SlackOauth2(
            IOptionsSnapshot<SlackConfiguration> slackConfig,
            ISecretsProvider secretsProvider)
        {
            _slackConfig = slackConfig;

            if (string.IsNullOrEmpty(_slackConfig?.Value?.ClientId) ||
                string.IsNullOrEmpty(_slackConfig?.Value?.ClientSecret) ||
                string.IsNullOrEmpty(_slackConfig?.Value?.RedirectUri))
            {
                throw new ArgumentException("Slack configuration is invalid");
            }

            _secretsProvider = secretsProvider;
        }

        [FunctionName("SlackOauth2")]
        public async Task<IActionResult> Run(
                    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "oauth2/slack")] HttpRequest req,
                    ExecutionContext executionContext,
                    ILogger log)
        {
            string code = req.Query["code"];

            var client = new RestClient(SlackHost);

            var request = new RestRequest(SlackTokenExchangePath, Method.Post)
                .AddParameter("code", code)
                .AddParameter("client_id", _slackConfig.Value.ClientId)
                .AddParameter("client_secret", _slackConfig.Value.ClientSecret)
                .AddParameter("redirect_uri", _slackConfig.Value.RedirectUri);

            var oauth2Response = await client.ExecuteAsync<SlackOauth2Response>(request);

            if (!oauth2Response.Data.Ok || string.IsNullOrEmpty(oauth2Response.Data.AccessToken))
            {
                return new BadRequestObjectResult($"Failed to authorize with Slack. Error from Slack: {oauth2Response.Content}");
            }

            var secret = new ShareableTextSecret(oauth2Response.Data.AccessToken)
                .WithId(oauth2Response.Data.Team.Id)
                .WithExpireIn(TimeSpan.FromDays(365 * 100));

            var (secretId, error) = await _secretsProvider.SetSecretAsync(secret);

            if (error != null)
            {
                return new ContentResult
                {
                    ContentType = "text/html",
                    Content = $"<!DOCTYPE html><html><body>Failed to add SnapSecret to Slack: {error.UserMessage}</body></html>"
                };
            }

            log.LogInformation("Successfully set secret for Slack access token for team id {SlackTeamId}", secretId);

            string path = Path.Combine(executionContext.FunctionDirectory, "../oauth2response.html");

            return new ContentResult
            {
                ContentType = "text/html",
                Content = File.ReadAllText(path)
            };
        }
    }

    public class SlackOauth2Response
    {
        public bool Ok { get; set; }

        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; }

        public string Scope { get; set; }

        [JsonProperty("bot_user_id")]
        public string BotUserId { get; set; }

        [JsonProperty("app_id")]
        public string AppId { get; set; }

        public SlackTeam Team { get; set; }

        public SlackTeam Enterprise { get; set; }

        [JsonProperty("authed_user")]
        public SlackAuthedUser AuthedUser { get; set; }
    }

    public class SlackTeam
    {
        public string Name { get; set; }
        public string Id { get; set; }
    }

    public class SlackAuthedUser
    {
        public string Id { get; set; }
        public string Scope { get; set; }
        public string AccessToken { get; set; }
        public string TokenType { get; set; }
    }
}
