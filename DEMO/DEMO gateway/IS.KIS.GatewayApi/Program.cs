using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using IS.KIS.GatewayApi.Infrastructure;
using IS.KIS.GatewayApi.Infrastructure.ValueObject.Settings;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Serilog;
using Serilog.Formatting.Elasticsearch;

try
{
    var builder = WebApplication.CreateBuilder(
        new WebApplicationOptions
        {
            EnvironmentName = Environment.GetEnvironmentVariable("ENVIRONMENT"),
            Args = args
        }
    );
    builder.Configuration
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile(
            "ocelot.json",
            optional: false,
            reloadOnChange: true
        )
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile(
            "appsettings.json",
            optional: true,
            reloadOnChange: true
        )
        .AddEnvironmentVariables()
        .Build();

    builder.Services.AddControllers();
    var azureAdSettings = builder.Configuration.GetSection(nameof(AzureAd)).Get<AzureAd>();

    builder.Services
        .AddAuthentication(sharedOptions =>
        {
            sharedOptions.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            sharedOptions.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(
            "Admin",
            options =>
            {
                options.Audience = azureAdSettings.Audience;
                options.MetadataAddress = azureAdSettings.MetadataAddress;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = false,
                    ValidateIssuer = false
                };
            }
        );

    builder.Services.AddAuthorization(options =>
    {
        var defaultAuthorizationPolicyBuilder = new AuthorizationPolicyBuilder("Admin");
        defaultAuthorizationPolicyBuilder =
            defaultAuthorizationPolicyBuilder.RequireAuthenticatedUser();
        options.DefaultPolicy = defaultAuthorizationPolicyBuilder.Build();
    });

    builder.Services.AddCors(options =>
    {
        options.AddPolicy(
            "CorsPolicy",
            builder =>
                builder
                    .SetIsOriginAllowed(isOriginAllowed: _ => true)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
        );
    });

    builder.Services.AddSwaggerForOcelot(
        builder.Configuration,
        (o) =>
        {
            o.AddAuthenticationProviderKeyMapping("Bearer", "Admin");
        }
    );

    builder.Services
        .AddOcelot(builder.Configuration)
        .AddDelegatingHandler<HeaderDelegatingHandler>(true)
        .AddKubernetesFixed();

    var app = builder.Build();

    var logger = app.Logger;
    var lifetime = app.Lifetime;
#pragma warning disable CA2254 // Template should be a static expression

    app.UseHttpsRedirection();
    app.UseRouting();
    app.UseCors("CorsPolicy");
    app.UseAuthentication();
    app.UseAuthorization();
    if (!app.Environment.IsEnvironment("PROD") && !app.Environment.IsEnvironment("STAGING"))
    {
        var swaggerSettings = builder.Configuration
            .GetSection(nameof(SwaggerSettings))
            .Get<SwaggerSettings>();
        var context =  app.Services.GetRequiredService<IHttpContextAccessor>();
        app.UseSwaggerForOcelotUI(options =>
        { 
            options.DownstreamSwaggerEndPointBasePath = "/api/swagger/docs";
            options.PathToSwaggerGenerator = "/api/swagger/docs";
            options.DownstreamSwaggerHeaders = new[]
            {
                new KeyValuePair<string, string>("Bearer", context.HttpContext?.Request.Headers.Authorization.FirstOrDefault()??""), 
            };
        }, c =>
        {
            c.OAuthConfigObject.ClientId  = swaggerSettings.ClientId;
            c.OAuthConfigObject.UsePkceWithAuthorizationCodeGrant = true;
        });
    }
    var conf = new OcelotPipelineConfiguration()
    {
        PreErrorResponderMiddleware = async (ctx, next) =>
        {
            if (ctx.Request.Path.Equals(new PathString("/healthcheck")))
            {
                await ctx.Response.WriteAsync("ok");
            }
            else
            {
                await next.Invoke();
            }
        }
    };
    app.UseOcelot(conf).Wait();
    AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(
        (s, a) =>
            logger.LogError((Exception)a.ExceptionObject, ((Exception)a.ExceptionObject).Message)
    );
    lifetime.ApplicationStarted.Register(
        () => logger.LogInformation($"The application started")
    );
#pragma warning restore CA2254 // Template should be a static expression
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "The application failed to start.");
}
finally
{
    Log.CloseAndFlush();
}
