using MQTTnet;
using StateMachineDemo.Infrastructure.Messaging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public static class MqttConnectionExtensions
{
    public static void AddMqtt(this IServiceCollection services, Func<IServiceProvider, MqttClientOptions> options)
    {
        services.AddSingleton<MqttConnection>();
        services.AddSingleton<MqttClientOptions>(options);

        services.AddHostedService<MqttConnectionService>();
    }
}
