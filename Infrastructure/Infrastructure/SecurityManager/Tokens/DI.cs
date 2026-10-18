using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;

namespace Infrastructure.SecurityManager.Tokens;

public static class DI
{
    public static IServiceCollection RegisterToken(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSectionName = "Jwt";
        services.Configure<TokenSettings>(configuration.GetSection(jwtSectionName));

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            var tokenSettings = configuration.GetSection(jwtSectionName).Get<TokenSettings>();

            if (string.IsNullOrWhiteSpace(tokenSettings?.Key) || tokenSettings.Key.Length < 32)
            {
                throw new InvalidOperationException(
                    "Jwt:Key is missing or shorter than 32 characters. Set it via user-secrets or the environment variable Jwt__Key.");
            }

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromMinutes(tokenSettings?.ClockSkewInMinute ?? 5),
                ValidIssuer = tokenSettings?.Issuer,
                ValidAudience = tokenSettings?.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenSettings?.Key ?? throw new ArgumentNullException("JWT Key is empty")))
            };

            options.Events = new JwtBearerEvents
            {
                // Prioritizing HttpOnly cookie before checking Authorization header
                OnMessageReceived = context =>
                {
                    var accessToken = context.HttpContext.Request.Cookies["accessToken"];
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        context.Token = accessToken;
                    }
                    else
                    {
                        var authorizationHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                        if (!string.IsNullOrEmpty(authorizationHeader) && authorizationHeader.StartsWith("Bearer "))
                        {
                            context.Token = authorizationHeader.Substring("Bearer ".Length).Trim();
                        }
                    }

                    return Task.CompletedTask;
                },

                // Custom handling for expired tokens
                OnChallenge = context =>
                {
                    // Browser page navigation (not an /api call) without a valid token: send the user to the login page.
                    if (!context.Request.Path.StartsWithSegments("/api"))
                    {
                        context.HandleResponse();
                        context.Response.Redirect("/Accounts/Login");
                        return Task.CompletedTask;
                    }

                    if (context.AuthenticateFailure is SecurityTokenExpiredException)
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = 498; // Custom status code for expired token
                        context.Response.ContentType = "application/json";

                        var result = JsonSerializer.Serialize(new
                        {
                            code = 498,
                            message = "Token has expired.",
                            error = new
                            {
                                @ref = "https://datatracker.ietf.org/doc/html/rfc9110",
                                exceptionType = "SecurityTokenExpiredException",
                                innerException = "SecurityTokenExpiredException",
                                source = "",
                                stackTrace = ""
                            }
                        });

                        return context.Response.WriteAsync(result);
                    }

                    return Task.CompletedTask;
                }



            };

        });

        services.AddTransient<ITokenService, TokenService>();
        services.AddScoped<TokenSettings>();

        return services;
    }
}

