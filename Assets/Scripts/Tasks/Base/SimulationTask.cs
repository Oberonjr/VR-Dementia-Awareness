using System;
using UnityEngine;

public class SimulationTask : MonoBehaviour
{
    public event Action onTaskFinished;

    protected bool isActive;
    protected bool isFinished;

    public virtual void StartTask()
    {
        if (isActive) { return; }
        isActive = true;
    }

    public virtual void FinishTask()
    {
        if (isFinished) { return; }

        onTaskFinished?.Invoke();
        isFinished = true;
        isActive = false;
    }
}
