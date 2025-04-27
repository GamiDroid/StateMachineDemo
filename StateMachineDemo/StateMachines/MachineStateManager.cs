using Microsoft.EntityFrameworkCore;
using MQTTnet.Protocol;
using Stateless;
using Stateless.Graph;
using Stateless.Reflection;
using StateMachineDemo.Infrastructure.Messaging;
using StateMachineDemo.Infrastructure.Persistance;
using StateMachineDemo.Infrastructure.Persistance.Tables;
using StateMachineDemo.StateMachines.Operations;

namespace StateMachineDemo.StateMachines;

// Model voor de machine state
public enum MachineState
{
    /// <summary>
    /// Er is geen order
    /// </summary>
    NoOrder,

    /// <summary>
    /// Wacht op een pallet uit het magazijn
    /// </summary>
    WaitPallet,

    /// <summary>
    /// Wachten tot een pallet is gescand
    /// </summary>
    ScanPallet,

    /// <summary>
    /// Wachten tot een tank is gescand
    /// </summary>
    ScanTank,

    /// <summary>
    /// Wachten tot een bigbag is geleegd
    /// </summary>
    EmptyBigbag,

    /// <summary>
    /// Wachten tot een tank is gekozen
    /// </summary>
    ChooseTank,
}

// Triggers/Events die state transitions kunnen veroorzaken
public enum MachineTrigger
{
    Start,
    PalletArrived,
    PalletScanned,
    TankScanned,
    BigbagEmptied,
    TankChosen
}

public class MachineStateManager
{
    private readonly StateMachine<MachineState, MachineTrigger> _stateMachine;
    private readonly AppDbContext _db;
    private readonly IServiceProvider _serviceProvider;
    private readonly MqttConnection _mqtt;
    private readonly ILogger<MachineStateManager> _logger;

    private readonly int _reworkStationId;
    private MachineState _currentState;

    public MachineStateManager(
        int reworkStationId,
        AppDbContext dbContext,
        IServiceProvider serviceProvider,
        MqttConnection mqtt,
        ILogger<MachineStateManager> logger)
    {
        _reworkStationId = reworkStationId;
        _db = dbContext;
        _serviceProvider = serviceProvider;
        _mqtt = mqtt;
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
            .OnEntryAsync(PersistStateChangeAsync)
            .OnEntryFromAsync(MachineTrigger.TankChosen, ExecuteOperationAsync<TankChosenOperationHandler>)
            .Permit(MachineTrigger.Start, MachineState.WaitPallet);

        _stateMachine.Configure(MachineState.WaitPallet)
            .OnEntryAsync(PersistStateChangeAsync)
            .OnEntryFromAsync(MachineTrigger.Start, ExecuteOperationAsync<StartOperationHandler>)
            .Permit(MachineTrigger.PalletArrived, MachineState.ScanPallet);

        _stateMachine.Configure(MachineState.ScanPallet)
            .OnEntryAsync(PersistStateChangeAsync)
            .OnEntryFromAsync(MachineTrigger.PalletArrived, ExecuteOperationAsync<PalletArrivedOperationHandler>)
            .OnExitAsync(ExecuteOperationAsync<ScanPalletOperationHandler>)
            .Permit(MachineTrigger.PalletScanned, MachineState.ScanTank);

        _stateMachine.Configure(MachineState.ScanTank)
            .OnEntryAsync(PersistStateChangeAsync)
            .OnEntryFromAsync(MachineTrigger.PalletScanned, ExecuteOperationAsync<ScanPalletOperationHandler>)
            .Permit(MachineTrigger.TankScanned, MachineState.EmptyBigbag);

        _stateMachine.Configure(MachineState.EmptyBigbag)
            .Permit(MachineTrigger.BigbagEmptied, MachineState.ChooseTank)
            .OnEntryFromAsync(MachineTrigger.TankScanned, ExecuteOperationAsync<ScanTankOperationHandler>)
            .OnEntryAsync(PersistStateChangeAsync);

        _stateMachine.Configure(MachineState.ChooseTank)
            .Permit(MachineTrigger.TankChosen, MachineState.NoOrder)
            .OnEntryFromAsync(MachineTrigger.BigbagEmptied, ExecuteOperationAsync<BigbagEmptiedOperationHandler>)
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

    public StateMachineInfo GetInfo()
    {
        return _stateMachine.GetInfo();
    }

    public string GetMermaidDiagram()
    {
        return MermaidGraph.Format(_stateMachine.GetInfo());
    }

    public IEnumerable<MachineTrigger> GetPermittedTriggers()
    {
        return _stateMachine.PermittedTriggers;
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

            await _mqtt.PublishAsync($"dcr/station{_reworkStationId}/state", instanceRecord, MqttQualityOfServiceLevel.AtMostOnce, cancellationToken: CancellationToken.None);

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
