namespace IS.KIS.GatewayApi.Infrastructure.ValueObject.Settings
{
    public interface IAzureAd
    {
        string Audience { get; set; }
        string MetadataAddress { get; set; }
        string ClientId { get; set; }
        string ClientSecret { get; set; }
    }

    public class AzureAd : IAzureAd
    {
        public string Audience { get; set; } = string.Empty;
        public string MetadataAddress { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
    }
}