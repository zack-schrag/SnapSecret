using System;

namespace SnapSecret.Domain
{

    public class ShareableTextSecret : IShareableTextSecret
    {
        public string? Prompt { get; private set; }
        public string? Answer { get; private set; }
        public string Text { get; }

        public TimeSpan ExpireIn { get; private set; }

        public ShareableTextSecret(string text)
        {
            Text = text;
            ExpireIn = TimeSpan.FromHours(1);
        }

        public ShareableTextSecret WithExpireIn(TimeSpan timeSpan)
        {
            ExpireIn = timeSpan;
            return this;
        }

        public ShareableTextSecret WithPrompt(string? prompt, string? answer)
        {
            Prompt = prompt;
            Answer = answer;
            return this;
        }
    }
}
