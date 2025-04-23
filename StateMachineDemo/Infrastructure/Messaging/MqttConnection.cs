using MQTTnet;
using MQTTnet.Protocol;
using System.Text.Json;

namespace StateMachineDemo.Infrastructure.Messaging;

public class MqttConnection(
    MqttClientOptions mqttClientOptions, 
    ILogger<MqttConnection> logger) : IDisposable
{
    private readonly IMqttClient _mqttClient = new MqttClientFactory().CreateMqttClient();
    private readonly MqttClientOptions _mqttClientOptions = mqttClientOptions;
    private readonly ILogger<MqttConnection> _logger = logger;

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Connecting to MQTT broker...");
        await _mqttClient.ConnectAsync(_mqttClientOptions, cancellationToken);
        _mqttClient.ApplicationMessageReceivedAsync += HandleApplicationMessageReceived;
        _mqttClient.ConnectedAsync += HandleConnected;
        _mqttClient.DisconnectedAsync += HandleDisconnected;
    }

    private Task HandleApplicationMessageReceived(MqttApplicationMessageReceivedEventArgs eventArgs)
    {
        _logger.LogInformation("Received message from topic: {topic} - {messageBody}", eventArgs.ApplicationMessage.Topic, eventArgs.ApplicationMessage.ConvertPayloadToString());
        return Task.CompletedTask;
    }

    private Task HandleConnected(MqttClientConnectedEventArgs eventArgs)
    {
        _logger.LogInformation("Connected to MQTT broker.");
        return Task.CompletedTask;
    }

    private Task HandleDisconnected(MqttClientDisconnectedEventArgs eventArgs)
    {
        _logger.LogInformation("Disconnected from MQTT broker.");
        return Task.CompletedTask;
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Disconnecting from MQTT broker...");
        await _mqttClient.DisconnectAsync(cancellationToken: cancellationToken);
    }

    public async Task<bool> PublishAsync<T>(string topic, T payload, MqttQualityOfServiceLevel qos, CancellationToken cancellationToken)
    {
        var payloadAsJson = JsonSerializer.Serialize(payload);
        return await PublishAsync(topic, payloadAsJson, qos, cancellationToken);
    }

    public async Task<bool> PublishAsync(string topic, string payload, MqttQualityOfServiceLevel qos, CancellationToken cancellationToken)
    {
        var result = await _mqttClient.PublishStringAsync(topic, payload, qos, retain: false, cancellationToken);
        return result.IsSuccess;
    }

    #region Dispose Methods
    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            _mqttClient.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    } 
    #endregion
}
