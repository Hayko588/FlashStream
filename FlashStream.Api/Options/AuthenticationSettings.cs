namespace FlashStream.Api.Options;

public sealed class AuthenticationSettings
{
    public const string SectionName = "Authentication";

    public string Authority { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
}
