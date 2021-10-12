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

namespace SnapSecret.AzureFunctions
{
    public class SecretsFunctions
    {
        private const string Version = "1";

        private readonly ISnapSecretBusinessLogic _snapSecretBusinessLogic;

        public SecretsFunctions(ISnapSecretBusinessLogic snapSecretBusinessLogic)
        {
            _snapSecretBusinessLogic = snapSecretBusinessLogic;
        }

        [FunctionName("CreateSecret")]
        public async Task<IActionResult> CreateSecretAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = $"v{Version}/secrets")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Creating new secret");
            log.LogInformation($"{req.Scheme}://{req.Host}{req.Path}/foo");

            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var createSecretRequest = JsonSerializer.Deserialize<CreateSecretRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var (secretId, error) = await _snapSecretBusinessLogic.SubmitSecretAsync(createSecretRequest.ToShareableTextSecret());

            if (error != null)
            {
                return new ObjectResult(error.ToResponse())
                {
                    StatusCode = 500
                };
            }
            
            return new CreatedResult($"{req.Scheme}://{req.Host}{req.Path}/{secretId}", new
            {
                message = "Successfully created secret"
            });
        }

        [FunctionName("AccessSecret")]
        public async Task<IActionResult> AccessSecretAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = $"v{Version}/secrets/{{secretId}}")] HttpRequest req,
            Guid secretId,
            ILogger log)
        {
            log.LogInformation($"Attempting to access secret {secretId}");

            var (secret, error) = await _snapSecretBusinessLogic.AccessSecretAsync(secretId);

            if (error != null)
            {
                return new ObjectResult(error.ToResponse())
                {
                    StatusCode = 500
                };
            }

            return new OkObjectResult(new
            {
                message = "Secret accessed, it will not be accesible anymore",
                secret = secret.Text
            });
        }
    }

    public class CreateSecretRequest
    {
        public string? Prompt { get; set; }
        public string? Answer { get; set; }
        public string Text { get; set; }
        public TimeSpan ExpireIn { get; set; }

        public IShareableTextSecret ToShareableTextSecret()
        {
            return new ShareableTextSecret(Text)
                .WithPrompt(Prompt, Answer)
                .WithExpireIn(ExpireIn);
        }
    }
}
