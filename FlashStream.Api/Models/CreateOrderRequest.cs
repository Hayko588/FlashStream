using System.ComponentModel.DataAnnotations;

namespace FlashStream.Api.Models;

public sealed record CreateOrderRequest(
    Guid UserId,
    [param: Required] Guid ProductId,
    [param: Required] int Quantity);
