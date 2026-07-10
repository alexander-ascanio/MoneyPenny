namespace MoneyPenny.ViewModels.Home;

public class ApiDocsViewModel
{
    public string Intro { get; init; } = string.Empty;
    public IReadOnlyList<ApiEndpointGroupViewModel> Groups { get; init; } = [];
}

public class ApiEndpointGroupViewModel
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<ApiEndpointViewModel> Endpoints { get; init; } = [];
}

public class ApiEndpointViewModel
{
    public string HttpMethod { get; init; } = "GET";
    public string Path { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public bool RequiresAntiForgery { get; init; }
    public IReadOnlyList<ApiFieldViewModel> RequestFields { get; init; } = [];
    public IReadOnlyList<ApiFieldViewModel> ResponseFields { get; init; } = [];
}

public class ApiFieldViewModel
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public bool Required { get; init; }
    public string Description { get; init; } = string.Empty;
}
