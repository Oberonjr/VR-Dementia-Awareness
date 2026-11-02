using System;
using UnityEngine;

[Serializable]
public class Task
{
    [SerializeField]
    protected string name;
    [SerializeField]
    protected string description;

    protected bool taskStarted = false;
    
    public event Action onTaskStart, onTaskEnd;

    /// <param name="argument">Optional argument that can be used for inherited classes.</param>
    public virtual void StartTask(object argument = null)
    {
        onTaskStart?.Invoke();
        taskStarted = true;
    }
    
    public virtual void EndTask()
    {
        Debug.Log("Task has ended. This task is called: " + name);
        onTaskEnd?.Invoke();
        taskStarted = false;
    }
    
    
    public virtual void OnValidate() {}
    public virtual void Update() {}
}