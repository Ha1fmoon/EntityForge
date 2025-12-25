using EntityForge.Models;

namespace EntityForge.Services;

public class TypeService
{
    private readonly List<TypeDefinition> _types = [];

    public TypeService()
    {
        InitializeBaseTypes();
    }

    public IEnumerable<TypeDefinition> GetAllTypes()
    {
        return _types;
    }

    public TypeDefinition? FindTypeById(string id)
    {
        return _types.FirstOrDefault(t => t.Id == id);
    }

    private void InitializeBaseTypes()
    {
        _types.Add(new TypeDefinition
        {
            Id = "string",
            DisplayName = "String",
            BaseType = "string",
            IsValueObject = false,
            DbColumnType = "varchar",
            IsNullable = true,
            MaxLength = 255
        });

        _types.Add(new TypeDefinition
        {
            Id = "int",
            DisplayName = "Integer",
            BaseType = "int",
            IsValueObject = false,
            DbColumnType = "integer",
            IsNullable = false
        });

        _types.Add(new TypeDefinition
        {
            Id = "decimal",
            DisplayName = "Decimal",
            BaseType = "decimal",
            IsValueObject = false,
            DbColumnType = "numeric",
            IsNullable = false
        });

        _types.Add(new TypeDefinition
        {
            Id = "bool",
            DisplayName = "Boolean",
            BaseType = "bool",
            IsValueObject = false,
            DbColumnType = "boolean",
            IsNullable = false
        });

        _types.Add(new TypeDefinition
        {
            Id = "datetime",
            DisplayName = "Date Time",
            BaseType = "DateTime",
            IsValueObject = false,
            DbColumnType = "timestamp",
            IsNullable = false
        });

        _types.Add(new TypeDefinition
        {
            Id = "email",
            DisplayName = "Email",
            BaseType = "string",
            IsValueObject = true,
            DbColumnType = "varchar",
            IsNullable = false,
            MaxLength = 100,
            ValidationPattern = @"^[^\s@]+@[^\s@]+\.[^\s@]+$",
            ValidationErrorMessage = "Invalid email format",
            Name = "Email"
        });
    }
}