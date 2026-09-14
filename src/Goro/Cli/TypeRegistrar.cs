using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace Goro.Cli;

/// <summary>
/// Adapts Spectre.Console.Cli's <see cref="ITypeRegistrar"/> seam onto
/// Microsoft.Extensions.DependencyInjection, per Spectre's documented DI pattern.
/// A hand-rolled dictionary-backed registrar isn't enough here: Spectre resolves
/// some of its own internal services (e.g. its help providers) as collections
/// (<c>IEnumerable&lt;T&gt;</c>), which needs a real container's multi-registration
/// support rather than a one-factory-per-type lookup.
/// </summary>
public sealed class TypeRegistrar(IServiceCollection services) : ITypeRegistrar
{
    public ITypeResolver Build() => new TypeResolver(services.BuildServiceProvider());

    public void Register(Type service, Type implementation) => services.AddSingleton(service, implementation);

    public void RegisterInstance(Type service, object implementation) => services.AddSingleton(service, implementation);

    public void RegisterLazy(Type service, Func<object> factory) => services.AddSingleton(service, _ => factory());
}
