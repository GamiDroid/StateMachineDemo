namespace StateMachineDemo.StateMachines.Operations;

public class StartOperationHandler(ILogger<StartOperationHandler> logger) : IOperationHandler
{
    private readonly ILogger<StartOperationHandler> _logger = logger;

    public async Task ExecuteAsync(MachineContext context)
    {
        _logger.LogInformation("Starting machine: {ReworkStationId}", context.ReworkStationId);
        // Implementeer de daadwerkelijke start logica
        await Task.Delay(500);
    }
}
