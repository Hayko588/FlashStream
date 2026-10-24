namespace FlashStream.Application.Configuration;

public sealed class OrderPolicySettings
{
    public const string SectionName = "OrderPolicy";

    public int MaximumQuantity { get; init; }
}
