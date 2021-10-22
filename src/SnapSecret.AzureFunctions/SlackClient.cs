using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnapSecret.AzureFunctions
{
    public class SlackClient
    {
        private static readonly string SlackPostMessageUrl = "https://slack.com/api/chat.postMessage";

        private readonly string _accessToken;
        private readonly string _channelId;

        public SlackClient(string accessToken, string channelId)
        {
            _accessToken = accessToken;
            _channelId = channelId;
        }

        public async Task SendMessageAsync(string message)
        {
            var client = new RestClient(SlackPostMessageUrl);

            IRestRequest request = new RestRequest(Method.POST)
                .AddHeader("Authorization", $"Bearer {_accessToken}")
                .AddHeader("Content-Type", "application/json")
                .AddJsonBody(new
                {
                    channel = _channelId,
                    text = message
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
