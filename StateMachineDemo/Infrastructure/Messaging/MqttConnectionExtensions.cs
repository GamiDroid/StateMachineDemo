using MQTTnet;

namespace StateMachineDemo.Infrastructure.Messaging;

public static class MqttConnectionExtensions
{
    public static void AddMqtt(this IServiceCollection services, Func<IServiceProvider, MqttClientOptions> options)
    {
        services.AddSingleton<MqttConnection>();
        services.AddSingleton<MqttClientOptions>(options);
    }
}
