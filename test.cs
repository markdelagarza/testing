
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EfQueryValidationRunner.Configuration;

public static class RunnerServiceRegistration
{
    public static IServiceCollection AddValidationDbContext<TContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configure)
        where TContext : DbContext
    {
        services.AddDbContext<TContext>((_, options) =>
        {
            configure(options);

            // Prevent EF Core from tracking returned entities.
            options.UseQueryTrackingBehavior(
                QueryTrackingBehavior.NoTracking);
        });

        return services;
    }

    public static IServiceCollection AddValidationDbContext<TContext, TInterface>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configure)
        where TContext : DbContext, TInterface
        where TInterface : class
    {
        services.AddValidationDbContext<TContext>(configure);

        services.AddScoped<TInterface>(provider =>
            provider.GetRequiredService<TContext>());

        return services;
    }
}
