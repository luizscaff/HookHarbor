namespace HookHarbor.Api.Security;

public class ManagementApiKeyOptions
{
    public const string SectionName = "ManagementApi";

    public string ApiKey { get; init; } = string.Empty;
}
