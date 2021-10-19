using System;

namespace SnapSecret.Domain
{
    public interface IShareableTextSecret
    {
        string Id { get; }
        string? Prompt { get; }
        string? Answer { get; }
        string Text { get; }
        TimeSpan? ExpireIn { get; }
    }
}
