namespace EntityForge.Models;

public class TypeDefinition
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string DisplayName { get; set; } = string.Empty;

    public string BaseType { get; set; } = string.Empty;
    public string DbColumnType { get; set; } = string.Empty;

    public bool IsNullable { get; set; }
    public int? MaxLength { get; set; }

    public bool IsValueObject { get; set; }
    public string? ValidationPattern { get; set; }
    public string? ValidationErrorMessage { get; set; }
}