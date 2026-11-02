

namespace Basket.Api.Dependencies;

public static class DependencyExtensions
{
    public static void AddAllDependencies(this IServiceCollection serviceCollection)
    {
        serviceCollection.Scan(scan => scan
            .FromApplicationDependencies()
            .AddClasses(classes => classes.AssignableTo<ITransientDependency>())
                .AsImplementedInterfaces().WithTransientLifetime()
            .AddClasses(classes => classes.AssignableTo<IScopedDependency>())
                .AsImplementedInterfaces().WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo<ISingletonDependency>())
                .AsImplementedInterfaces().WithSingletonLifetime()
        );
    }
}