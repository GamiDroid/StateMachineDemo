namespace StateMachineDemo.Infrastructure.Persistance.Tables;

public class ChocoReworkStation
{
    public int Id { get; set; }
    public string Type { get; set; } = null!;
    public string Status { get; set; } = null!;
    public uint? AxItemRequestId { get; set; }
    public int? ChocoProductionId { get; set; }
    public string? Component { get; set; }
}
