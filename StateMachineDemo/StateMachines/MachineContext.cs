namespace StateMachineDemo.StateMachines;

public class MachineContext
{
    public int ReworkStationId { get; set; }
    public MachineState PreviousState { get; set; }
    public MachineState CurrentState { get; set; }
    public MachineTrigger Trigger { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = [];
}
