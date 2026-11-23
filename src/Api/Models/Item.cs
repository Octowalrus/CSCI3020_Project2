using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Api.Models;

[BsonIgnoreExtraElements]
public sealed class Item
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("Name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("Description")]
    public string? Description { get; set; }

    [BsonElement("Price")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Price { get; set; }
}
