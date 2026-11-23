namespace Api.Models;

public sealed class CheckoutRequest
{
    public IReadOnlyList<string> ItemIds { get; set; } = [];
}
