namespace ScoutAgent.Api.Dependencies;

public static class DependencyExtensions
{
    public static void AddAllDependencies(this IServiceCollection serviceCollection)
    {
        // AsSelfWithInterfaces: hem somut tip hem arayüzler (ör. ISingletonDependency) kayıtlı, TEK örnek
        // paylaşılır. Ctor somut tip enjekte ettiğinde (ör. ChatClientFactory, McpToolSource) çözülür —
        // yalnız AsImplementedInterfaces somut tipi kaydetmez → ctor çözülemez (bilinen tuzak).
        serviceCollection.Scan(scan => scan
            .FromApplicationDependencies()
            .AddClasses(classes => classes.AssignableTo<ITransientDependency>())
                .AsSelfWithInterfaces().WithTransientLifetime()
            .AddClasses(classes => classes.AssignableTo<IScopedDependency>())
                .AsSelfWithInterfaces().WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo<ISingletonDependency>())
                .AsSelfWithInterfaces().WithSingletonLifetime()
        );
    }
}
