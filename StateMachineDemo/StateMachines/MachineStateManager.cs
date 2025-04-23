using Microsoft.EntityFrameworkCore;
using Stateless;
using StateMachineDemo.Infrastructure.Persistance;
using StateMachineDemo.Infrastructure.Persistance.Tables;
using StateMachineDemo.StateMachines.Operations;

namespace StateMachineDemo.StateMachines;

// Model voor de machine state
public enum MachineState
{
    NoOrder,
    WaitPallet,
    ScanPallet,
    ScanTank,
    EmptyBigbag,
    ChooseTank,
    ShuttingDown
}

// Triggers/Events die state transitions kunnen veroorzaken
public enum MachineTrigger
{
    Initialize,
    Start,
    Pause,
    Resume,
    CompleteTask,
    DetectError,
    ResolveError,
    BeginMaintenance,
    EndMaintenance,
    Shutdown
}

public class MachineStateManager
{
    private readonly StateMachine<MachineState, MachineTrigger> _stateMachine;
    private readonly AppDbContext _db;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MachineStateManager> _logger;

    private readonly int _reworkStationId;
    private MachineState _currentState;

    public MachineStateManager(
        int reworkStationId,
        AppDbContext dbContext,
        IServiceProvider serviceProvider,
        ILogger<MachineStateManager> logger)
    {
        _reworkStationId = reworkStationId;
        _db = dbContext;
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Laad de huidige state uit de database of begin met de standaard state
        var stateRecord = _db.ChocoReworkStations
            .FirstOrDefault(s => s.Id == reworkStationId);

        _currentState = GetMachineState(stateRecord?.Status);

        // Initialiseer de state machine
        _stateMachine = new StateMachine<MachineState, MachineTrigger>(() => _currentState, state => _currentState = state);

        // Configureer alle toegestane state transitions
        ConfigureStateMachine();
    }

    public MachineState CurrentState => _currentState;

    private void ConfigureStateMachine()
    {
        // Configureer de state transitions met bijbehorende operaties
        _stateMachine.Configure(MachineState.NoOrder)
            .Permit(MachineTrigger.Initialize, MachineState.WaitPallet)
            .Permit(MachineTrigger.BeginMaintenance, MachineState.ChooseTank)
            .OnEntryAsync(PersistStateChangeAsync);

        _stateMachine.Configure(MachineState.WaitPallet)
            .Permit(MachineTrigger.Start, MachineState.ScanPallet)
            .Permit(MachineTrigger.DetectError, MachineState.EmptyBigbag)
            .OnEntryFromAsync(MachineTrigger.Initialize, ExecuteOperationAsync<InitializeOperationHandler>)
            .OnEntryAsync(PersistStateChangeAsync);

        _stateMachine.Configure(MachineState.ScanPallet)
            .Permit(MachineTrigger.Pause, MachineState.ScanTank)
            .Permit(MachineTrigger.DetectError, MachineState.EmptyBigbag)
            .Permit(MachineTrigger.Shutdown, MachineState.ShuttingDown)
            .Permit(MachineTrigger.CompleteTask, MachineState.NoOrder)
            .OnEntryFromAsync(MachineTrigger.Start, ExecuteOperationAsync<StartOperationHandler>)
            .OnEntryFromAsync(MachineTrigger.Resume, ExecuteOperationAsync<ResumeOperationHandler>)
            .OnEntryAsync(PersistStateChangeAsync);

        _stateMachine.Configure(MachineState.ScanTank)
            .Permit(MachineTrigger.Resume, MachineState.ScanPallet)
            .Permit(MachineTrigger.Shutdown, MachineState.ShuttingDown)
            .Permit(MachineTrigger.BeginMaintenance, MachineState.ChooseTank)
            .OnEntryAsync(PersistStateChangeAsync);

        _stateMachine.Configure(MachineState.EmptyBigbag)
            .Permit(MachineTrigger.ResolveError, MachineState.NoOrder)
            .Permit(MachineTrigger.BeginMaintenance, MachineState.ChooseTank)
            .OnEntryAsync(PersistStateChangeAsync);

        _stateMachine.Configure(MachineState.ChooseTank)
            .Permit(MachineTrigger.EndMaintenance, MachineState.NoOrder)
            .OnEntryAsync(PersistStateChangeAsync);

        _stateMachine.Configure(MachineState.ShuttingDown)
            .Permit(MachineTrigger.CompleteTask, MachineState.NoOrder)
            .OnEntryAsync(PersistStateChangeAsync);
    }

    public async Task<bool> TriggerAsync(MachineTrigger trigger, Dictionary<string, object>? parameters = null)
    {
        try
        {
            if (_stateMachine.CanFire(trigger))
            {
                _logger.LogInformation("Triggering {Trigger} on machine {ReworkStationId}", trigger, _reworkStationId);

                var context = new MachineContext
                {
                    ReworkStationId = _reworkStationId,
                    PreviousState = _currentState,
                    Trigger = trigger,
                    Parameters = parameters ?? []
                };

                await _stateMachine.FireAsync(trigger);

                context.CurrentState = _currentState;
                return true;
            }
            else
            {
                _logger.LogWarning("Cannot trigger {Trigger} from current state {CurrentState} on machine {ReworkStationId}",
                    trigger, _currentState, _reworkStationId);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering {Trigger} on machine {ReworkStationId}", trigger, _reworkStationId);
            return false;
        }
    }

    private async Task ExecuteOperationAsync<THandler>() where THandler : IOperationHandler
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<THandler>();

            var context = new MachineContext
            {
                ReworkStationId = _reworkStationId,
                PreviousState = _currentState,
                CurrentState = _currentState
            };

            await handler.ExecuteAsync(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing operation {Operation}", typeof(THandler).Name);
            // Optioneel: Voer automatisch een fout-trigger uit
            // await _stateMachine.FireAsync(MachineTrigger.DetectError);
        }
    }

    // TODO: Move this to a separate service
    // Persist state change to the database
    private async Task PersistStateChangeAsync(StateMachine<MachineState, MachineTrigger>.Transition transition)
    {
        try
        {
            var instanceRecord = await _db.ChocoReworkStations
                .FirstOrDefaultAsync(s => s.Id == _reworkStationId);

            if (instanceRecord == null)
            {
                instanceRecord = new ChocoReworkStation
                {
                    Id = _reworkStationId,
                    Status = GetMachineStateString(_currentState),
                    Type = "<???>",
                    AxItemRequestId = null,
                    ChocoProductionId = null,
                    Component = null,
                };

                _db.ChocoReworkStations.Add(instanceRecord);
            }
            else
            {
                instanceRecord.Status = GetMachineStateString(_currentState);
                // TODO: Add any other properties that need to be updated
                instanceRecord.ChocoProductionId = null;
                instanceRecord.AxItemRequestId = null;
                instanceRecord.Component = null;
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("Choco Rework Station {ReworkStationId} state changed to {CurrentState}", _reworkStationId, _currentState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error persisting state change to {CurrentState}", _currentState);
        }
    }

    private static MachineState GetMachineState(string? statusAsString)
    {
        return statusAsString switch
        {
            "no_order" => MachineState.NoOrder,
            "wait_pallet" => MachineState.WaitPallet,
            "scan_pallet" => MachineState.ScanPallet,
            "scan_tank" => MachineState.ScanTank,
            "empty_bigbag" => MachineState.EmptyBigbag,
            "choose_tank" => MachineState.ChooseTank,
            _ => throw new ArgumentOutOfRangeException(nameof(statusAsString), $"Unknown state: {statusAsString}")
        };
    }

    private static string GetMachineStateString(MachineState state)
    {
        return state switch
        {
            MachineState.NoOrder => "no_order",
            MachineState.WaitPallet => "wait_pallet",
            MachineState.ScanPallet => "scan_pallet",
            MachineState.ScanTank => "scan_tank",
            MachineState.EmptyBigbag => "empty_bigbag",
            MachineState.ChooseTank => "choose_tank",
            _ => throw new ArgumentOutOfRangeException(nameof(state), $"Unknown state: {state}")
        };
    }
}
