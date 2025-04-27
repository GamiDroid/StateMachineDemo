namespace StateMachineDemo.StateMachines.Operations;

public class ScanTankOperationHandler(ILogger<ScanPalletOperationHandler> logger) : IOperationHandler
{
    private readonly ILogger<ScanPalletOperationHandler> _logger = logger;

    public async Task ExecuteAsync(MachineContext context)
    {
        _logger.LogInformation("scan tank: {ReworkStationId}", context.ReworkStationId);
        // Implementeer de daadwerkelijke resume logica
        await Task.Delay(1000); // Simuleer enige processeringstijd
    }
}
