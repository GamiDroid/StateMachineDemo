using StateMachineDemo.Infrastructure.Persistance;

namespace StateMachineDemo.StateMachines;

// Factory voor het maken van state managers voor verschillende machines
public class MachineStateManagerFactory(
    AppDbContext dbContext,
    IServiceProvider serviceProvider,
    ILoggerFactory loggerFactory)
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<MachineStateManager> _logger = loggerFactory.CreateLogger<MachineStateManager>();

    public MachineStateManager Create(int reworkStationId)
    {
        return new MachineStateManager(reworkStationId, _dbContext, _serviceProvider, _logger);
    }
}