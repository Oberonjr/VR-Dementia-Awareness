using System;
using UnityEngine;

[Serializable]
public class TimerTask : Task
{
    [SerializeField, Min(0)]
    private float endTime = 5;
    [SerializeField, Min(0)]
    private float currentTime = 0;

    [SerializeField]
    private Timer _timer;
    private bool TimerStarted => _timer.didStart;

    public override void OnValidate()
    {
        if (currentTime > endTime)
            currentTime = endTime;
        
        if (_timer == null)
        {
            _timer.SetCurrentPassedTime(currentTime);
            _timer.SetWaitTime(endTime);
        }
    }

    public override void StartTask(object argument = null)
    {
        // Task cannot start if the timer cannot be assigned.
        if (TryGetTimer(out Timer timer, argument))
        {
            _timer = timer;
            base.StartTask(argument);

            _timer.Setup(endTime, false, true);
            _timer.SetCurrentPassedTime(currentTime);
            _timer.OnTimerFinished += this.EndTask;
        }
    }

    public override void EndTask()
    {
        Debug.Log(_timer);
        if (_timer != null)
            _timer.OnTimerFinished -= this.EndTask;
        
        base.EndTask();
    }

    private bool TryGetTimer(out Timer timer, object argument)
    {
        if (argument != null && argument is Timer argTimer)
        {
            timer = argTimer;
            return true;
        }

        timer = null;
        return false;
    }
}