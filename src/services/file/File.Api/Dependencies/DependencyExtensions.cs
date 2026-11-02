namespace FileApi.Dependencies;

// Scrutor otomatik DI kaydı (IDependency marker'ları). LocalDiskFileStore/JsonCoverSource
// ISingletonDependency ile implemente eder → burada elle kayıt yok (konvansiyon).
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
