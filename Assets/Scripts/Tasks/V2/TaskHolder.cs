using System;
using System.Collections.Generic;
using UnityEngine;

public class TaskHolder : MonoBehaviour
{
    public static event Action onTaskStart;

    [SerializeReference, SubclassSelector]
    private Task[] taskList;
    
    private Task _currentTask;
    private int _currentTaskIndex = -1;
    private Timer _timer;

    /// <summary>
    /// Updates the tasks if their values are changed.
    /// </summary>
    private void OnValidate()
    {
        if (onTaskStart != null)
        {
            foreach (Task task in taskList)
            {
                task.OnValidate();
            }
        }
    }
    
    private void Start()
    {
        if (taskList == null || taskList.Length == 0)
        {
            Debug.LogWarning("No tasks assigned, no tasks will be started.");
            return;
        }
        
        StartNextTask();
    }

    /// <summary>
    /// Tries to get the task from an array.
    /// </summary>
    /// <param name="task">The task that gets picked out of an array.</param>
    /// <param name="index">The index of the task from an array.</param>
    /// <returns>True if the Task has been successfully received.</returns>
    private bool TryGetTask(out Task task, int index)
    {
        task = null;
        if (taskList == null)
        {
            Debug.LogError("The task list is null.");
            return false;
        }
        if (taskList.Length <= index)
        {
            Debug.Log("There are no tasks on this index or higher.");
            return false;
        }
        if (taskList[index] == null)
        {
            Debug.LogError("Selected task is null.");
            return false;
        }
        
        task = taskList[index];
        
        return true;
    }
    
    /// <summary>
    /// Starts the next task out of the Task array.
    /// </summary>
    private void StartNextTask()
    {
        // Remove the method from the current task for unneccesary calls.
        if (_currentTaskIndex != -1)
            _currentTask.onTaskEnd -= StartNextTask;
        
        // Tries to get the next task.
        _currentTaskIndex++;
        if (TryGetTask(out _currentTask, _currentTaskIndex))
        {
            // If starting the task is successful, Continue!
            // Otherwise, prevent any potential Exception by returning the method.
            if (!StartTask()) return;
            
            _currentTask.StartTask();
            onTaskStart?.Invoke();
            _currentTask.onTaskEnd += StartNextTask;
        }
    }

    /// <summary>
    /// Starts the current task. Add the proper values to a task if needed.
    /// </summary>
    /// <returns>True on success.</returns>
    private bool StartTask()
    {
        try
        {
            switch (_currentTask)
            {
                case TimerTask timerTask:
                    if (_timer == null) _timer = gameObject.AddComponent<Timer>();
                    timerTask.StartTask(_timer);
                    return true;
                
                default:
                    _currentTask.StartTask();
                    return true;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("An Exception occured when trying to start a task:\n" + e);
            return false;
        }
    }

    /// <summary>
    /// Calls the current task Update method.
    /// </summary>
    private void Update()
    {
        _currentTask?.Update();
    }
}
