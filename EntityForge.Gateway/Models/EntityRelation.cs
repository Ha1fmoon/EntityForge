namespace EntityForge.Gateway.Models;

public class EntityRelation
{
    public string SourceEntity { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string RelatedEntity { get; set; } = string.Empty;
    public List<string> RelatedIds { get; set; } = [];
}