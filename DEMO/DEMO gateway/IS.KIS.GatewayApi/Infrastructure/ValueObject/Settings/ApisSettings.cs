namespace IS.KIS.GatewayApi.Infrastructure.ValueObject.Settings
{
    public class Apis
    {
        public Settings TestApi { get; set; } = new Settings();
    }

    public class Settings
    {
        public string Scope { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }
}