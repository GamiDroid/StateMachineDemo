using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MQTTnet.Protocol;
using Stateless.Reflection;
using StateMachineDemo.Infrastructure.Messaging;
using StateMachineDemo.Infrastructure.Persistance;
using StateMachineDemo.Infrastructure.Persistance.Tables;
using StateMachineDemo.StateMachines;

namespace StateMachineDemo.Presentation;

public static class ChocoReworkStationEndpoints
{
    public static void MapChocoReworkStationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/choco_rework_stations", GetAllChocoReworkStations)
            .WithName(nameof(GetAllChocoReworkStations))
            .Produces<List<ChocoReworkStation>>(StatusCodes.Status200OK);

        app.MapGet("/choco_rework_stations/{id}", GetChocoReworkStationById)
            .WithName(nameof(GetChocoReworkStationById))
            .Produces<ChocoReworkStation>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPut("/choco_rework_stations/{id}", UpdateChocoReworkStation)
            .WithName(nameof(UpdateChocoReworkStation));

        app.MapGet("/choco_rework_stations/{id}/state", GetChocoReworkStationState)
            .WithName(nameof(GetChocoReworkStationState))
            .Produces<MachineState>(StatusCodes.Status200OK);

        app.MapPut("/choco_rework_stations/{id}/trigger/{trigger}", TriggerMachineState)
            .WithName(nameof(TriggerMachineState))
            .Produces<MachineState>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        app.MapGet("/choco_rework_stations/{id}/info", GetStateMachineInfo)
            .WithName(nameof(GetStateMachineInfo))
            .Produces<StateMachineInfo>(StatusCodes.Status200OK);

        app.MapGet("/choco_rework_stations/{id}/mermaid-diagram", GetStateMachineMermaidDiagram)
            .WithName(nameof(GetStateMachineMermaidDiagram))
            .Produces<string>(StatusCodes.Status200OK, contentType: "text/plain");

        app.MapGet("/choco_rework_stations/{id}/permitted-triggers", GetStateMachinePermittedTriggers)
            .WithName(nameof(GetStateMachinePermittedTriggers))
            .Produces<MachineState[]>(StatusCodes.Status200OK);

    }

    static Task<List<ChocoReworkStation>> GetAllChocoReworkStations(AppDbContext db)
    {
        return db.ChocoReworkStations.AsNoTracking().ToListAsync();
    }

    static async Task<Results<NotFound<string>, Ok<ChocoReworkStation>>> GetChocoReworkStationById(int id, AppDbContext db)
    {
        return await db.ChocoReworkStations.FindAsync(id)
            is ChocoReworkStation chocoReworkStation
                ? TypedResults.Ok(chocoReworkStation)
                : TypedResults.NotFound($"Choco rework station with Id {id} not found");
    }

    static async Task<Results<Ok<ChocoReworkStation>, NotFound<string>>> UpdateChocoReworkStation(
        AppDbContext db, MqttConnection mqtt, int id, UpdateChocoReworkStationRequest request, CancellationToken cancellationToken)
    {
        var reworkStation = await db.ChocoReworkStations.FindAsync([id], cancellationToken: cancellationToken);
        if (reworkStation is null)
            return TypedResults.NotFound($"Choco rework station with Id {id} not found");

        reworkStation.Status = request.Status;
        await db.SaveChangesAsync(cancellationToken);

        await mqtt.PublishAsync($"dcr/station{id}/state", reworkStation, MqttQualityOfServiceLevel.AtMostOnce, cancellationToken);

        return TypedResults.Ok(reworkStation);
    }

    static string GetChocoReworkStationState(MachineStateManagerFactory machineStateFactory, int id)
    {
        var machineState = machineStateFactory.Create(id);
        return machineState.CurrentState.ToString();
    }

    static async Task<IResult> TriggerMachineState(
        MachineStateManagerFactory machineStateFactory, int id, MachineTrigger trigger, Dictionary<string, object>? parameters = null)
    {
        var stateManager = machineStateFactory.Create(id);
        var success = await stateManager.TriggerAsync(trigger, parameters);

        if (success)
        {
            return Results.Ok(new { State = stateManager.CurrentState.ToString() });
        }

        return Results.BadRequest(new { Error = $"Cannot trigger {trigger} from current state {stateManager.CurrentState}" });
    }

    static IResult GetStateMachineInfo(MachineStateManagerFactory machineStateFactory, int id)
    {
        var stateMachine = machineStateFactory.Create(id);
        var info = stateMachine.GetInfo();
        return Results.Ok(new { info.InitialState, info.States });
    }

    static IResult GetStateMachineMermaidDiagram(MachineStateManagerFactory machineStateFactory, int id)
    {
        var stateMachine = machineStateFactory.Create(id);
        return Results.Text(stateMachine.GetMermaidDiagram(), contentType: "text/plain", statusCode: StatusCodes.Status200OK);
    }

    static IResult GetStateMachinePermittedTriggers(MachineStateManagerFactory machineStateFactory, int id)
    {
        var stateMachine = machineStateFactory.Create(id);
        return Results.Ok(stateMachine.GetPermittedTriggers().Select(t => t.ToString()));
    }
}

public record UpdateChocoReworkStationRequest(
    string Status);
