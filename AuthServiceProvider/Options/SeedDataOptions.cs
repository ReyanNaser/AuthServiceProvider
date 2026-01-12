namespace AuthServiceProvider.Options;

public class SeedDataOptions
{
    public List<SsoClientOptions> Clients { get; set; } = new();
    public List<SsoScopeOptions> Scopes { get; set; } = new();
}
