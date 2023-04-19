using System.Net.Http.Headers;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using IS.KIS.GatewayApi.Infrastructure.ValueObject.Settings;

namespace IS.KIS.GatewayApi.Infrastructure
{
    public class HeaderDelegatingHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public HeaderDelegatingHandler(
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration
        )
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var azureScope = new List<string>();

            if (
                _httpContextAccessor.HttpContext != null
                && !string.IsNullOrEmpty(_httpContextAccessor.HttpContext.Request.Path.Value)
            )
            {
                azureScope = GetScope(
                    _httpContextAccessor.HttpContext.Request.Path.Value ?? "",
                    _configuration.GetSection(nameof(Apis)).Get<Apis>()
                );
            }
            else
            {
                throw new InvalidOperationException("HttpContext was null");
            }
            var userAccessToken =
                _httpContextAccessor.HttpContext.Request.Headers.Authorization.FirstOrDefault();

            if (userAccessToken == null)
                throw new InvalidOperationException("User AccessToken cant be null");

            var userAssertion = new UserAssertion(
                userAccessToken.ToString().Replace("Bearer ", "")
            );

            var confidentialClient = ConfidentialClientApplicationBuilder
                .Create("CLIENT-ID")
                .WithClientSecret("CLIENT-SECRET")
                .WithAuthority(new Uri("https://login.microsoftonline.com/TENANT-ID/.well-known/openid-configuration"))
                .Build();

            confidentialClient.AddDistributedTokenCache(services =>
            {
                services.AddDistributedSqlServerCache(options =>
                {
                    options.ConnectionString = "ConnectionString";
                    options.SchemaName = "SchemaName";
                    options.TableName = "TableName";
                    options.DefaultSlidingExpiration = TimeSpan.FromMinutes(
                        30
                    );
                });
            });

            var accessTokenRequest = confidentialClient.AcquireTokenOnBehalfOf(
                azureScope,
                userAssertion
            );

            var oboAccessToken = await accessTokenRequest
                .ExecuteAsync(cancellationToken);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", oboAccessToken.AccessToken);

            return await base.SendAsync(request, cancellationToken);
        }

        public static List<string> GetScope(string requestUri, Apis apiSettings)
        {
            var scopes = new List<string>();
            scopes.Add(apiSettings.TestApi.Scope);
            return scopes;
        }
    }
}
