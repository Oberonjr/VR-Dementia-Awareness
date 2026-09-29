using UnityEngine;

public class TaskCaller : MonoBehaviour
{
    private Task _task = null;
    private bool _taskOnGoing = false;

    public void SetTask(Task task)
    {
        _task = task;
        _taskOnGoing = true;
    }

    public void FinishTask()
    {
        if (_taskOnGoing)
        {
            _task.EndTask();
            _taskOnGoing = false;
            _task = null;
        }
    }

    public bool CallerValid()
    {
        return _taskOnGoing && _task != null;
    }
}
