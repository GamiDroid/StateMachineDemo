using StateMachineDemo.StateMachines;
using StateMachineDemo.StateMachines.Operations;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

// Extension methods voor DI registratie
public static class MachineStateManagerExtensions
{
    public static IServiceCollection AddMachineStateManager(this IServiceCollection services)
    {
        // Registreer alle operation handlers

        // TODO: Automatically register all handlers in the assembly
        services.AddTransient<InitializeOperationHandler>();
        services.AddTransient<StartOperationHandler>();
        services.AddTransient<ResumeOperationHandler>();

        // Registreer de state manager factory
        services.AddScoped<MachineStateManagerFactory>();

        return services;
    }
}
