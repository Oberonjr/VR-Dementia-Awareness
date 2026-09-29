using UnityEngine;

public class NavigateMenu : SimulationTask
{
    private void Start()
    {
        EventBus<OnChangePalmMenuActive>.Publish(new OnChangePalmMenuActive(false));
    }

    private void OnDestroy()
    {
        EventBus<OnStartSimulation>.OnEvent -= SimulationStarted;
    }

    public override void StartTask()
    {
        base.StartTask();
        EventBus<OnStartSimulation>.OnEvent += SimulationStarted;
    }

    private void SimulationStarted(OnStartSimulation evt)
    {
        EventBus<OnStartSimulation>.OnEvent -= SimulationStarted;
        FinishTask();
    }
}