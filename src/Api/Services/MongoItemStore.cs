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

    public async Task<PagedResult<Item>> SearchAsync(
        string? search,
        bool? hasDescription,
        string sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var filters = new List<FilterDefinition<Item>>();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchFilter = Builders<Item>.Filter.Or(
                Builders<Item>.Filter.Regex(item => item.Name,
                    new MongoDB.Bson.BsonRegularExpression(search, "i")),
                Builders<Item>.Filter.Regex(item => item.Description,
                    new MongoDB.Bson.BsonRegularExpression(search, "i")));
            filters.Add(searchFilter);
        }

        if (hasDescription.HasValue)
        {
            var descriptionFilter = hasDescription.Value
                ? Builders<Item>.Filter.Ne(item => item.Description, null)
                : Builders<Item>.Filter.Eq(item => item.Description, null);
            filters.Add(descriptionFilter);
        }

        var filter = filters.Count == 0
            ? FilterDefinition<Item>.Empty
            : Builders<Item>.Filter.And(filters);

        var totalCount = await _items.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var query = _items.Find(filter);
        query = sort switch
        {
            "name-desc" => query.SortByDescending(item => item.Name),
            "oldest" => query.SortBy(item => item.Id),
            "newest" => query.SortByDescending(item => item.Id),
            _ => query.SortBy(item => item.Name)
        };

        var items = await query
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Item>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

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
        return result.MatchedCount == 1;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var result = await _items.DeleteOneAsync(item => item.Id == id, cancellationToken);
        return result.DeletedCount == 1;
    }

    public async Task<CheckoutResult> CheckoutAsync(
        IReadOnlyList<string> itemIds,
        CancellationToken cancellationToken)
    {
        var distinctIds = itemIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();
        if (distinctIds.Count == 0)
            return new CheckoutResult();

        var filter = Builders<Item>.Filter.In(item => item.Id, distinctIds);
        var items = await _items.Find(filter).ToListAsync(cancellationToken);
        var total = items.Sum(item => item.Price);
        var result = await _items.DeleteManyAsync(filter, cancellationToken);

        return new CheckoutResult
        {
            RemovedCount = (int)result.DeletedCount,
            Total = total
        };
    }
}
