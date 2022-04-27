﻿using System;

namespace SnapSecret.Domain
{

    public class ShareableTextSecret : IShareableTextSecret
    {
        public string Id { get; private set; }
        public string? Prompt { get; private set; }
        public string? Answer { get; private set; }
        public string Text { get; }

        public TimeSpan? ExpireIn { get; private set; }

        public ShareableTextSecret(string text)
        {
            Text = text;
            Id = Guid.NewGuid().ToString();
        }

        public ShareableTextSecret WithId(string id)
        {
            Id = id;
            return this;
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
