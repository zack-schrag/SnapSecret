using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using SnapSecret.Application;
using SnapSecret.Application.Abstractions;
using SnapSecret.Domain;

namespace SnapSecret.AzureFunctions
{
    public class SlackWorker
    {
        private readonly ISnapSecretBusinessLogic _snapSecretBusinessLogic;
        private readonly ISecretsProvider _secretsProvider;

        public SlackWorker(ISnapSecretBusinessLogic snapSecretBusinessLogic, ISecretsProvider secretsProvider)
        {
            _snapSecretBusinessLogic = snapSecretBusinessLogic;
            _secretsProvider = secretsProvider;
        }

        [FunctionName("SlackWorker")]
        public async Task Run(
            [QueueTrigger("slack-create-secret")] CreateSecretRequest createSecretRequest,
            ILogger log)
        {
            if (string.IsNullOrEmpty(createSecretRequest.SlackChannelId))
            {
                throw new ArgumentException("Slack channel id is empty", nameof(createSecretRequest.SlackChannelId));
            }

            if (string.IsNullOrEmpty(createSecretRequest.SlackTeamId))
            {
                throw new ArgumentException("Slack team id is empty", nameof(createSecretRequest.SlackTeamId));
            }

            var secret = createSecretRequest.ToShareableTextSecret();

            if (secret is null)
            {
                var msg = $"Failed to convert {typeof(CreateSecretRequest)} request to {typeof(IShareableTextSecret)}";
                log.LogError(msg);
                throw new ArgumentException(msg, nameof(createSecretRequest));
            }

            var (secretId, error) = await _snapSecretBusinessLogic.SubmitSecretAsync(secret);

            if (error != null)
            {
                var msg = error.UserMessage;

                log.LogError(error.UserMessage);

                var exception = new Exception(msg);

                if (error.Exceptions.Count > 0)
                {
                    exception = new Exception(msg, error.Exceptions.First());
                }
                
                throw exception;
            }

            var (slackAccessToken, getSecretError) = await _secretsProvider.GetSecretAsync(createSecretRequest.SlackTeamId);

            if (getSecretError != null || slackAccessToken is null)
            {
                throw new Exception($"Failed to retrieve Slack access token for team id {createSecretRequest.SlackTeamId}");
            }

            var notifier = new SlackNotifier(slackAccessToken.Text, createSecretRequest.SlackChannelId);

            await notifier.SendMessageAsync($"{createSecretRequest.BaseSecretsPath}{secretId}");
        }
    }
}
