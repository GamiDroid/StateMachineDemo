using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MQTTnet.Protocol;
using StateMachineDemo.Infrastructure.Messaging;
using StateMachineDemo.Infrastructure.Persistance;
using StateMachineDemo.Infrastructure.Persistance.Tables;

namespace StateMachineDemo.Presentation;

public static class ChocoReworkStationEndpoints
{
    public static void MapChocoReworkStationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/choco_rework_stations", GetAllChocoReworkStations)
            .WithName("GetAllChocoReworkStations")
            .Produces<List<ChocoReworkStation>>(StatusCodes.Status200OK);

        app.MapGet("/choco_rework_stations/{id}", GetChocoReworkStationById)
        .WithName("GetChocoReworkStationById")
        .Produces<ChocoReworkStation>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        app.MapPut("/choco_rework_stations/{id}", UpdateChocoReworkStation)
            .WithName("UpdateChocoReworkStation");
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
}

public record UpdateChocoReworkStationRequest(
    string Status);
