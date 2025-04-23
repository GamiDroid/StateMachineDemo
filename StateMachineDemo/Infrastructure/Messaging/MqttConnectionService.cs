namespace StateMachineDemo.Infrastructure.Messaging;

public class MqttConnectionService(MqttConnection mqtt) : IHostedService
{
    private readonly MqttConnection _mqtt = mqtt;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return _mqtt.ConnectAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _mqtt.DisconnectAsync(cancellationToken);
    }
}