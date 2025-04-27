using StateMachineDemo.StateMachines;
using System.Reflection;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

// Extension methods voor DI registratie
public static class MachineStateManagerExtensions
{
    public static IServiceCollection AddMachineStateManager(this IServiceCollection services, Assembly assembly)
    {
        // Registreer alle operation handlers
        AddOperationHandlersFromAssembly(services, assembly);

        // Registreer de state manager factory
        services.AddScoped<MachineStateManagerFactory>();

        return services;
    }

    private static void AddOperationHandlersFromAssembly(IServiceCollection services, Assembly assembly)
    {
        var operationHandlerTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsAssignableTo(typeof(IOperationHandler)));

        foreach (var operationHandlerType in operationHandlerTypes)
        {
            services.AddTransient(operationHandlerType);
        }
    }
}
