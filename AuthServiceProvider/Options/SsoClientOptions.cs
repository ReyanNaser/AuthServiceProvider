namespace AuthServiceProvider.Options
{
    public class SsoClientOptions
    {
        public string ClientId { get; set; } = default!;
        public string ClientSecret { get; set; } = default!;
        public string DisplayName { get; set; } = default!;
        public List<string> Scopes { get; set; } = new();
    }
}
