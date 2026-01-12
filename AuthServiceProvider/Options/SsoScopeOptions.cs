namespace AuthServiceProvider.Options
{
    public class SsoScopeOptions
    {
        public string Name { get; set; } = default!;
        public List<string> Resources { get; set; } = new();
    }
}
