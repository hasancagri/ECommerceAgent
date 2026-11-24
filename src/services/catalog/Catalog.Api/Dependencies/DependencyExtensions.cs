

namespace Catalog.Api.Dependencies;

public static class DependencyExtensions
{
    public static void AddAllDependencies(this IServiceCollection serviceCollection)
    {
        // AsSelfWithInterfaces: hem somut tip hem arayüzler (ör. ISingletonDependency) kayıtlı, TEK örnek.
        // YALNIZ AsImplementedInterfaces somut tipi kaydetmez → ctor somut enjekte edince DI çözemez
        // (bilinen tuzak: host-çözümlü BackgroundService EmbeddingClientFactory'i somut ister → Aspire'da
        // sessiz boot-etmez + gateway WaitFor kilitlenir). ScoutAgent 019 fix'iyle hizalı.
        serviceCollection.Scan(scan => scan
            .FromApplicationDependencies()
            .AddClasses(classes => classes.AssignableTo<ITransientDependency>()).AsSelfWithInterfaces().WithTransientLifetime()
            .AddClasses(classes => classes.AssignableTo<IScopedDependency>()).AsSelfWithInterfaces().WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo<ISingletonDependency>()).AsSelfWithInterfaces().WithSingletonLifetime()
        );
    }
}