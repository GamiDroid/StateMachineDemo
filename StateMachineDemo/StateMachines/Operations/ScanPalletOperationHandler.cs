namespace StateMachineDemo.StateMachines.Operations;

public class ScanPalletOperationHandler(ILogger<ScanPalletOperationHandler> logger) : IOperationHandler
{
    private readonly ILogger<ScanPalletOperationHandler> _logger = logger;

    public async Task ExecuteAsync(MachineContext context)
    {
        _logger.LogInformation("Scan pallet: {ReworkStationId}", context.ReworkStationId);
        // Implementeer de daadwerkelijke resume logica
        await Task.Delay(1000); // Simuleer enige processeringstijd
    }
}
