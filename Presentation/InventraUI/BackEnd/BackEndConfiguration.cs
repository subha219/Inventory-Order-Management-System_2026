using Application;
using InventraUI.BackEnd.Common.Handlers;
using InventraUI.BackEnd.Common.Services;
using Infrastructure;
using Infrastructure.DataAccessManager.EFCore;
using Infrastructure.SeedManager;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using System.Threading.RateLimiting;

namespace InventraUI.BackEnd;

public static class BackEndConfiguration
{
    public static IServiceCollection AddBackEndServices(this IServiceCollection services, IConfiguration configuration)
    {
        //>>> Application Layer
        services.AddApplicationServices();

        //>>> Infrastructure Layer
        services.AddInfrastructureServices(configuration);

        services.AddExceptionHandler<CustomExceptionHandler>();

        //>>> Common

        services.AddHttpContextAccessor();
        // Daily low-stock e-mail digest (off unless LowStockAlerts:Enabled = true)
        services.Configure<LowStockAlertSettings>(configuration.GetSection("LowStockAlerts"));
        // Approval rule for large stock adjustments / scrapping
        services.Configure<ApprovalSettings>(configuration.GetSection("Approvals"));
        services.AddSingleton<LowStockAlertSender>();
        services.AddHostedService<LowStockAlertBackgroundService>();
        // Automation rules: in-app notifications and optional e-mails, checked every 15 minutes
        services.AddHostedService<AutomationBackgroundService>();
        // Scheduled report e-mails use this clock offset for their send times
        services.Configure<ReportSettings>(configuration.GetSection("Reports"));
        // CORS: same-origin by default. Cross-origin callers must be listed in Cors:AllowedOrigins.
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        services.AddCors(opt =>
        {
            opt.AddDefaultPolicy(builder =>
            {
                if (allowedOrigins.Length > 0)
                {
                    builder.WithOrigins(allowedOrigins)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                }
            });
        });

        // Rate limiting: a global per-client limit and a stricter "auth" policy for login-type endpoints.
        var globalPermit = configuration.GetValue<int?>("RateLimiting:GlobalPermitPerMinute") ?? 300;
        var authPermit = configuration.GetValue<int?>("RateLimiting:AuthPermitPerMinute") ?? 10;
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (ctx, token) =>
            {
                ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                ctx.HttpContext.Response.Headers["Retry-After"] = "30";
                ctx.HttpContext.Response.ContentType = "text/plain";
                await ctx.HttpContext.Response.WriteAsync("Too many requests. Please wait a few seconds and try again.", token);
            };
            // Only API calls are limited. Pages and their scripts/styles (dozens of files per page) are not.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                !ctx.Request.Path.StartsWithSegments("/api") ? RateLimitPartition.GetNoLimiter("static") :
                RateLimitPartition.GetFixedWindowLimiter(
                    ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = globalPermit,
                        Window = TimeSpan.FromMinutes(1)
                    }));
            options.AddPolicy("auth", ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = authPermit,
                        Window = TimeSpan.FromMinutes(1)
                    }));
        });

        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
        services.AddMemoryCache();
        services.AddControllers(options => options.Filters.Add<InventraUI.BackEnd.Common.Services.PermissionFilter>())
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
                options.JsonSerializerOptions.WriteIndented = true;
            });
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Inventra API", Version = "v1" });
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.ApiKey,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT Authorization header using the Bearer scheme."
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    new string[] { }
                }
            });

        });


        services.Configure<ApiBehaviorOptions>(x =>
        {
            x.SuppressModelStateInvalidFilter = true;
        });

        return services;
    }

    public static IEndpointRouteBuilder MapBackEndRoutes(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllers();
        endpoints.MapHealthChecks("/health").AllowAnonymous();

        return endpoints;
    }

    public static IApplicationBuilder RegisterBackEndBuilder(
        this IApplicationBuilder app,
        IWebHostEnvironment environment,
        IHost host,
        IConfiguration configuration
        )
    {
        // >>> Create database
        host.CreateDatabase();

        //seed database with system data
        host.SeedSystemData();

        //seed database with demo data
        if (configuration.GetValue<bool>("IsDemoVersion"))
        {
            host.SeedDemoData();
        }

        if (environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Inventra V1");
            });
        }

        return app;
    }


}
