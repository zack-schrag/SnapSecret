using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SnapSecret.Application.Abstractions;
using System.Text.Json;
using SnapSecret.Domain;
using System.ComponentModel.DataAnnotations;

namespace SnapSecret.AzureFunctions
{
    public class SecretsFunctions
    {
        private const string SecretsBasePath = "v1/secrets";

        private readonly ISnapSecretBusinessLogic _snapSecretBusinessLogic;
        private readonly ILogger<SecretsFunctions> _logger;

        public SecretsFunctions(ISnapSecretBusinessLogic snapSecretBusinessLogic, ILogger<SecretsFunctions> logger)
        {
            _snapSecretBusinessLogic = snapSecretBusinessLogic;
            _logger = logger;
        }

        [FunctionName("CreateSecret")]
        public async Task<IActionResult> CreateSecretAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/secrets")] HttpRequest req)
        {
            _logger.LogInformation("Creating new secret");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            var createSecretRequest = JsonSerializer.Deserialize<CreateSecretRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (createSecretRequest is null)
            {
                _logger.LogError($"Failed to deserialize request to {typeof(CreateSecretRequest)}");
                return new StatusCodeResult(500);
            }

            return await CreateSecretInternalAsync(req, createSecretRequest);
        }

        [FunctionName("SlackCreateSecret")]
        public async Task<IActionResult> SlackCreateSecretAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/secrets-slack")] HttpRequest req,
            [Queue("slack-create-secret")] IAsyncCollector<CreateSecretRequest> slackQueue)
        {
            _logger.LogInformation("Creating new secret");

            var formCollection = await req.ReadFormAsync();

            var createSecretRequest = new CreateSecretRequest
            {
                Text = formCollection["text"],
                BaseSecretsPath = $"{req.Scheme}://{req.Host}/api/v1/secrets/",
                SlackChannelId = formCollection["channel_id"],
                SlackTeamId = formCollection["team_id"]
            };

            await slackQueue.AddAsync(createSecretRequest);

            return new OkObjectResult(new
            {
                replace_original = true,
                text = "Generating your magic link :magic_wand:"
            });
        }

        [FunctionName("AccessSecret")]
        public async Task<IActionResult> AccessSecretAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/secrets/{secretId}")] HttpRequest req,
            ExecutionContext executionContext,
            Guid secretId)
        {
            _logger.LogInformation("Attempting to access secret {SecretId}", secretId);

            var (secret, error) = await _snapSecretBusinessLogic.AccessSecretAsync(secretId);

            if (error != null)
            {
                _logger.LogError("Failed to access secret {SecretId}: {Error}", secretId, error.ToResponse());

                return new ObjectResult(error.ToResponse())
                {
                    StatusCode = 500
                };
            }

            if (secret is null)
            {
                return new StatusCodeResult(500);
            }

            var path = Path.Combine(executionContext.FunctionDirectory, "../secret.html");
            var html = File.ReadAllText(path).Replace("{{SECRET}}", secret.Text);

            return new ContentResult
            {
                Content = html,
                ContentType = "text/html",
                StatusCode = 200
            };
        }

        private async Task<IActionResult> CreateSecretInternalAsync(
            HttpRequest req,
            CreateSecretRequest createSecretRequest)
        {
            var secret = createSecretRequest.ToShareableTextSecret();

            if (secret is null)
            {
                _logger.LogError($"Failed to convert {typeof(CreateSecretRequest)} request to {typeof(IShareableTextSecret)}");
                return new StatusCodeResult(500);
            }

            var (secretId, error) = await _snapSecretBusinessLogic.SubmitSecretAsync(secret);

            if (error != null)
            {
                return new ObjectResult(error.ToResponse())
                {
                    StatusCode = 500
                };
            }

            if (req.Path.ToString().Contains("-slack"))
            {
                var path = req.Path.ToString().Replace("-slack", string.Empty);

                return new OkObjectResult(new
                {
                    replace_original = true,
                    text = $"{req.Scheme}://{req.Host}{path}/{secretId}"
                });
            }
            else
            {
                return new CreatedResult($"{req.Scheme}://{req.Host}{req.Path}/{secretId}", new
                {
                    message = "Successfully created secret"
                });
            }
        }
    }

    // TODO - abstract away the Slack specific pieces
    public class CreateSecretRequestDto
    {
        public string? Prompt { get; set; }
        public string? Answer { get; set; }

        public string? Text { get; set; }

        public string? ExpireIn { get; set; }

        public string? BaseSecretsPath { get; set; }

        public string? SlackChannelId { get; set; }

        public string? SlackTeamId { get; set; }
    }

    public class CreateSecretRequest
    {
        public string? Prompt { get; set; }
        public string? Answer { get; set; }
        
        [Required]
        public string? Text { get; set; }

        public TimeSpan? ExpireIn { get; set; }

        public string? BaseSecretsPath { get; set; }

        public string? SlackChannelId { get; set; }

        public string? SlackTeamId { get; set; }

        public IShareableTextSecret? ToShareableTextSecret()
        {
            if (string.IsNullOrEmpty(Text))
            {
                return default;
            }

            var secret = new ShareableTextSecret(Text)
                .WithPrompt(Prompt, Answer);

            if (ExpireIn != null)
            {
                secret.WithExpireIn(ExpireIn.Value);
            }

            return secret;
                
        }

        public CreateSecretRequestDto ToDto()
        {
            return new CreateSecretRequestDto
            {
                Answer = Answer,
                BaseSecretsPath = BaseSecretsPath,
                ExpireIn = ExpireIn.ToString(),
                Prompt = Prompt,
                SlackChannelId = SlackChannelId,
                SlackTeamId = SlackTeamId,
                Text = Text
            };
        }

        public static CreateSecretRequest FromDto(CreateSecretRequestDto dto)
        {
            return new CreateSecretRequest
            {
                Answer = dto.Answer,
                BaseSecretsPath = dto.BaseSecretsPath,
                ExpireIn = TimeSpan.Parse(dto.ExpireIn),
                Prompt = dto.Prompt,
                SlackChannelId = dto.SlackChannelId,
                SlackTeamId = dto.SlackTeamId,
                Text = dto.Text
            };
        }
    }
}
