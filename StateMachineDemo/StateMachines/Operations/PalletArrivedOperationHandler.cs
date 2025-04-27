namespace StateMachineDemo.StateMachines.Operations;

public class PalletArrivedOperationHandler(ILogger<PalletArrivedOperationHandler> logger) : IOperationHandler
{
    private readonly ILogger<PalletArrivedOperationHandler> _logger = logger;

    public async Task ExecuteAsync(MachineContext context)
    {
        _logger.LogInformation("Initializing machine: {ReworkStationId}", context.ReworkStationId);
        // Implementeer de daadwerkelijke initialisatie logica
        await Task.Delay(1000); // Simuleer enige processeringstijd
    }
}
