using System.ComponentModel.DataAnnotations;

namespace FlashStream.Api.Models;

public sealed record CreateOrderRequest(
    [property: Required] Guid ProductId,
    [property: Required] int Quantity);
