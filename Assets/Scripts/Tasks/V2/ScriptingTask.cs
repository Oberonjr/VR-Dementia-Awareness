using System;
using UnityEngine;

[Serializable]
public class ScriptingTask : Task
{
    [SerializeField]
    private TaskCaller target;
    [SerializeField]
    private bool enableObjectOnStart;
    [SerializeField]
    private bool DisableObjectOnEnd;
    
    public override void StartTask(object argument = null)
    {
        if (target == null) return;
        
        if (enableObjectOnStart) target.gameObject.SetActive(true);
        
        target.SetTask(this);
        
        base.StartTask(argument);
    }

    public override void EndTask()
    {
        base.EndTask();
        if (DisableObjectOnEnd) target.gameObject.SetActive(false);
    }
}