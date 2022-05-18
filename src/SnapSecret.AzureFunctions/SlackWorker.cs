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
        private readonly ILogger<SlackWorker> _logger;

        public SlackWorker(
            ISnapSecretBusinessLogic snapSecretBusinessLogic,
            ISecretsProvider secretsProvider,
            ILogger<SlackWorker> logger)
        {
            _snapSecretBusinessLogic = snapSecretBusinessLogic;
            _secretsProvider = secretsProvider;
            _logger = logger;
        }

        [FunctionName("SlackWorker")]
        public async Task Run(
            [QueueTrigger("slack-create-secret")] CreateSecretRequest createSecretRequest,
            ExecutionContext context)
        {
            _logger.LogInformation("Received request to create secret. Slack Channel ID: {SlackChannelId}. Team ID: {TeamId}. Invocation ID: {InvocationId}", 
                createSecretRequest.SlackChannelId,
                createSecretRequest.SlackTeamId,
                context.InvocationId);

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
                _logger.LogError(msg);
                throw new ArgumentException(msg, nameof(createSecretRequest));
            }

            var (secretId, error) = await _snapSecretBusinessLogic.SubmitSecretAsync(secret);

            //if (error != null)
            //{
            //    var msg = error.UserMessage;

            //    _logger.LogError(error.UserMessage);

            //    var exception = new Exception(msg);

            //    if (error.Exceptions.Count > 0)
            //    {
            //        exception = new Exception(msg, error.Exceptions.First());
            //    }
                
            //    throw exception;
            //}

            //var (slackAccessToken, getSecretError) = await _secretsProvider.GetSecretAsync(createSecretRequest.SlackTeamId);

            //if (getSecretError != null || slackAccessToken is null)
            //{
            //    throw new Exception($"Failed to retrieve Slack access token for team id {createSecretRequest.SlackTeamId}");
            //}

            //var slackClient = new SlackClient(slackAccessToken.Text, createSecretRequest.SlackChannelId);

            //try
            //{
            //    await slackClient.SendMessageAsync($"Here's your link! {createSecretRequest.BaseSecretsPath}{secretId}");
            //}
            //catch (Exception e)
            //{
            //    _logger.LogError(e, "Failed to send slack message: {Message}", e.Message);
            //}
        }
    }
}
