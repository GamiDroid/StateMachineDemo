namespace StateMachineDemo.StateMachines.Operations;

public class ResumeOperationHandler(ILogger<ResumeOperationHandler> logger) : IOperationHandler
{
    private readonly ILogger<ResumeOperationHandler> _logger = logger;

    public async Task ExecuteAsync(MachineContext context)
    {
        _logger.LogInformation("Resuming machine: {ReworkStationId}", context.ReworkStationId);
        // Implementeer de daadwerkelijke resume logica
        await Task.Delay(1000); // Simuleer enige processeringstijd
    }
}
