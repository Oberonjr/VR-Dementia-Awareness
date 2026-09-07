using System;
using UnityEngine;

/// <summary>
/// Reusable timer component providing events for active tracking and completion
/// </summary>
public class Timer : MonoBehaviour
{
    public event Action OnTimerFinished;
    public event Action OnTimerRunning;

    private float waitTime;
    private float currentPassedTime;

    private bool active;
    private bool loop;

    private void Update()
    {
        RunTimer();
    }

    public void Setup(float waitTimeSeconds, bool loop = false, bool startTimer = false)
    {
        waitTime = waitTimeSeconds;
        this.loop = loop;
        if (startTimer) { StartTimer(); }
    }

    private void RunTimer()
    {
        if (!active) { return; }

        currentPassedTime += Time.deltaTime;
        OnTimerRunning?.Invoke();

        if (currentPassedTime >= waitTime)
        {
            if (loop) { ResetTimer(true); }
            else { ResetTimer(); }

            OnTimerFinished?.Invoke();
        }
    }

    public void SetWaitTime(float waitTime)
    {
        this.waitTime = waitTime;
    }

    public void SetLoop(bool loop)
    {
        this.loop = loop;
    }

    public void StartTimer()
    {
        active = true;
    }

    public void StopTimer(bool resetTimer = false)
    {
        active = false;
        if (resetTimer) { ResetTimer(); }
    }

    public void ResetTimer(bool startTimer = false)
    {
        currentPassedTime = 0;
        if (startTimer) { StartTimer(); }
        else { StopTimer(); }
    }

    public bool GetActive()
    {
        return active;
    }

    public float GetTimeLeft()
    {
        float timeLeft = waitTime - currentPassedTime;
        if (timeLeft < 0) { timeLeft = 0; }
        return timeLeft;
    }
}