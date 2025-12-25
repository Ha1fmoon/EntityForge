namespace EntityForge.Shared.Models;

public class ServicePaths
{
    public string Root { get; set; } = string.Empty;
    private string ServiceName => Path.GetFileName(Root);

    public string Domain => Path.Combine(Root, $"{ServiceName}.Domain");
    public string Application => Path.Combine(Root, $"{ServiceName}.Application");
    public string Infrastructure => Path.Combine(Root, $"{ServiceName}.Infrastructure");
    public string Api => Path.Combine(Root, $"{ServiceName}.Api");
}