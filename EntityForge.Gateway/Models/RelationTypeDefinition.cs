namespace EntityForge.Gateway.Models;

public class RelationTypeDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Entity1 { get; set; } = string.Empty;
    public string Entity2 { get; set; } = string.Empty;
    public RelationCardinality Cardinality { get; set; } = RelationCardinality.ManyToMany;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}