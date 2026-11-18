using Api.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Api.Services;

public sealed class MongoItemStore
{
    private readonly IMongoCollection<Item> _items;

    public MongoItemStore(IOptions<MongoOptions> options)
    {
        var settings = options.Value;
        var client = new MongoClient(settings.ConnectionString);
        var database = client.GetDatabase(settings.DatabaseName);
        _items = database.GetCollection<Item>(settings.CollectionName);
    }

    public async Task<IReadOnlyList<Item>> GetAllAsync(CancellationToken cancellationToken) =>
        await _items.Find(FilterDefinition<Item>.Empty)
            .ToListAsync(cancellationToken);

    public async Task<Item?> GetAsync(string id, CancellationToken cancellationToken) =>
        await _items.Find(item => item.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<Item> CreateAsync(Item item, CancellationToken cancellationToken)
    {
        await _items.InsertOneAsync(item, cancellationToken: cancellationToken);
        return item;
    }

    public async Task<bool> UpdateAsync(string id, Item item, CancellationToken cancellationToken)
    {
        item.Id = id;
        var result = await _items.ReplaceOneAsync(
            existing => existing.Id == id,
            item,
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var result = await _items.DeleteOneAsync(item => item.Id == id, cancellationToken);
        return result.DeletedCount == 1;
    }
}
