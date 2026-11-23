namespace Api.Models;

public sealed class CheckoutResult
{
    public int RemovedCount { get; init; }
    public decimal Total { get; init; }
}
