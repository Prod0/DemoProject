using System.Globalization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using FluentValidation;
using FluentValidation.AspNetCore;
using MediatR;
using Prometheus;
using Serilog;
using System.Reflection;
using Microsoft.IdentityModel.Logging;

try
{
    var builder = WebApplication.CreateBuilder(
        new WebApplicationOptions
        {
            EnvironmentName = Environment.GetEnvironmentVariable("ENVIRONMENT")
        }
    );

    builder.Configuration
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile(
            $"appsettings.{Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "Production"}.json",
            optional: true,
            reloadOnChange: true
        )
        .AddEnvironmentVariables()
        .Build();


    builder.Services.AddControllers();
    builder.Services
        .AddAuthentication(
            sharedOptions => sharedOptions.DefaultScheme = JwtBearerDefaults.AuthenticationScheme
        )
        .AddJwtBearer(
            "Admin",
            options =>
            {
                options.Audience = "api://CLIENT-ID";
                options.MetadataAddress = "https://TENANT-ID/.well-known/openid-configuration";
            }
        );

    builder.Services.AddAuthorization(options =>
    {
        var defaultAuthorizationPolicyBuilder = new AuthorizationPolicyBuilder("Admin");
        defaultAuthorizationPolicyBuilder =
            defaultAuthorizationPolicyBuilder.RequireAuthenticatedUser();
        options.DefaultPolicy = defaultAuthorizationPolicyBuilder.Build();
    });

    builder.Services.AddSwaggerGen(options =>
    {
        options.CustomSchemaIds(s => s.FullName!.Replace("+", "."));

        var pipelineBuildVersion = "dummy-version";
        options.SwaggerDoc(
            "v1",
            new OpenApiInfo { Version = pipelineBuildVersion, Title = "Test API" }
        );

        options.AddSecurityDefinition(
            "Bearer",
            new OpenApiSecurityScheme
            {
                Description =
                    "JWT Authorization header using the Bearer scheme. \r\n\r\n Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 12345abcdef\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.OAuth2,
                Scheme = "Bearer",
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        Scopes = new Dictionary<string, string>
                        {
                            { "api://CLIENT-ID/user_impersonation", "Access API" }
                        },
                        AuthorizationUrl = new Uri("https://login.microsoftonline.com/TENANT-ID/oauth2/v2.0/authorize"),
                        TokenUrl = new Uri("https://login.microsoftonline.com/TENANT-ID/oauth2/v2.0/token"),
                    }
                }
            }
        );

        options.AddSecurityRequirement(
            new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        },
                        Scheme = "oauth2",
                        Name = "Bearer",
                        In = ParameterLocation.Header,
                    },
                    new List<string> { "api://CLIENT-ID/user_impersonation" }
                }
            }
        );
    });

    var app = builder.Build();
    var logger = app.Logger;
    var lifetime = app.Lifetime;
#pragma warning disable CA2254 // Template should be a static expression

    if (!app.Environment.IsEnvironment("PROD") && !app.Environment.IsEnvironment("STAGING"))
    {
        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseSwagger(c =>
        {
            c.RouteTemplate = "api/swagger/docs/{documentName}/swagger.json";
        });
        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = "api/swagger";
            options.SwaggerEndpoint("/api/swagger/docs/v1/swagger.json", "Test API");
            options.OAuthClientId("ClientId");
            options.OAuthUsePkce();
        });
        logger.LogDebug("Swagger initialized");
    }
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseEndpoints(endpoints => endpoints.MapControllers());

    lifetime.ApplicationStarted.Register(
        () => logger.LogInformation($"The application {app.Environment.ApplicationName} started")
    );
#pragma warning restore CA2254 // Template should be a static expression

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "The application failed to start.");
    Console.WriteLine(ex);
}
finally
{
    Log.CloseAndFlush();
}
