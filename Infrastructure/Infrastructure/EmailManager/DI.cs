using Application.Common.Services.EmailManager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.EmailManager;

public static class DI
{
    public static IServiceCollection RegisterEmailManager(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SmtpSettings>(configuration.GetSection("SmtpSettings"));
        services.AddSingleton<SmtpSettingsStore>();
        services.AddSingleton<ISmtpSettingsStore>(sp => sp.GetRequiredService<SmtpSettingsStore>());
        services.AddTransient<IEmailService, EmailService>();

        return services;
    }
}
