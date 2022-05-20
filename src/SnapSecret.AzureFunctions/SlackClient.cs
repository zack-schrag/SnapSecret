using RestSharp;
using System;
using System.Threading.Tasks;

namespace SnapSecret.AzureFunctions
{
    public class SlackClient
    {
        private static readonly string SlackHost = "https://slack.com";
        private static readonly string SlackPostMessagePath = "/api/chat.postMessage";

        private readonly string _accessToken;
        private readonly string _channelId;

        public SlackClient(string accessToken, string channelId)
        {
            _accessToken = accessToken;
            _channelId = channelId;
        }

        public async Task SendMessageAsync(string message)
        {
            var client = new RestClient(SlackHost);

            var request = new RestRequest(SlackPostMessagePath, Method.Post)
                .AddHeader("Authorization", $"Bearer {_accessToken}")
                .AddHeader("Content-Type", "application/json")
                .AddJsonBody(new
                {
                    channel = _channelId,
                    text = message,
                    unfurl_links = false,
                    unfurl_media = false
                });

            var response = await client.ExecuteAsync<SlackResponse>(request);

            if (!response.IsSuccessful || !response.Data.Ok)
            {
                throw new Exception($"Failed to send Slack message: {response.Content}");
            }
        }
    }

    public class SlackResponse
    {
        public bool Ok { get; set; }
    }
}
