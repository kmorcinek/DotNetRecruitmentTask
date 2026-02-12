using System.Reflection;
using Abstractions.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Commands;

public static class Extensions
{
    public static IServiceCollection AddCommands(this IServiceCollection services,
        IReadOnlyCollection<Assembly> assemblies) =>
        services
            .AddScoped<ICommandDispatcher, CommandDispatcher>()
            .Scan(typeSourceSelector => typeSourceSelector.FromAssemblies(assemblies)
                .AddClasses(filter => filter.AssignableTo(typeof(ICommandHandler<,>)))
                .AsImplementedInterfaces()
                .WithScopedLifetime());
}
