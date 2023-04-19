namespace IS.KIS.GatewayApi.Infrastructure.ValueObject.Settings
{
    public class SwaggerSettings
    {
        public string ClientId { get; set; } = string.Empty;
        public Dictionary<string, string> AdditionalParameters { get; set; } = new Dictionary<string, string>();
        public string AuthorizationUrl { get; set; } = string.Empty;
        public string TokenUrl { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
    }
}
