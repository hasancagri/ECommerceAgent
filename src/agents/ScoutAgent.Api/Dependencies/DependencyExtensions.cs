namespace ScoutAgent.Api.Dependencies;

public static class DependencyExtensions
{
    public static void AddAllDependencies(this IServiceCollection serviceCollection)
    {
        // 019 sub-agent'ları SOMUT tip enjekte eder (McpToolSource, ChatClientFactory, ISubAgent'lar) →
        // AsSelfWithInterfaces: hem somut tip hem arayüzler (ISingletonDependency + ör. ISubAgent) kayıtlı,
        // TEK örnek paylaşılır. (Yalnız AsImplementedInterfaces somut tipi kaydetmez → ctor çözülemez.)
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
