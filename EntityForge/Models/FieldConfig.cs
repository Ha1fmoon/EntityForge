namespace EntityForge.Models;

public class FieldConfig
{
    public string Name { get; set; } = string.Empty;
    public required TypeDefinition Type { get; set; }
    public bool IsRequired { get; set; }
    public bool IsUnique { get; set; } = false;
    public bool IsSearchable { get; set; } = false;
}