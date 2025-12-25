namespace EntityForge.Models;

public class EntityConfig
{
    public string Name { get; init; } = string.Empty;
    public string PluralName { get; set; } = string.Empty;
    public List<FieldConfig> Fields { get; set; } = [];
    public bool IsGenerated { get; set; }

    public string ServiceName => $"{Name}Service";

    public DateTime? LastGenerated { get; set; }
}