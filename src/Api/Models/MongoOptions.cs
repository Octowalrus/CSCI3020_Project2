namespace Api.Models;

public sealed class MongoOptions
{
    public const string SectionName = "Mongo";
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "maui_system";
    public string CollectionName { get; set; } = "GameProducts";
}
