namespace StateMachineDemo.StateMachines;

// Interface voor operatie handlers
public interface IOperationHandler
{
    Task ExecuteAsync(MachineContext context);
}
