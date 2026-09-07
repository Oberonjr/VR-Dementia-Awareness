using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages and triggers simulation tasks by listening to their completion events
/// Used to determine the order in which tasks get triggered
/// </summary>
public class TaskManager : MonoBehaviour
{
    [Header("Task Setup")]
    [SerializeField] GameObject[] taskInstances;

    private List<SimulationTask> tasks = new List<SimulationTask>();
    private int currentTaskIndex;

    private void Start()
    {
        // Extract SimulationTask components from serialized GameObjects
        for (int i = 0; i < taskInstances.Length; i++)
        {
            if (taskInstances[i].TryGetComponent<SimulationTask>(out SimulationTask task)) { tasks.Add(task); }
            else { Debug.LogWarning("TaskManager: GameObject is missing a SimulationTask component"); }
        }

        // Safely initialize and start the first task if available
        if (tasks.Count > 0)
        {
            tasks[currentTaskIndex].onTaskFinished += TriggerNextTask;
            tasks[0].StartTask();
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < tasks.Count; i++)
        {
            tasks[i].onTaskFinished -= TriggerNextTask;
        }
    }

    private void TriggerNextTask()
    {
        tasks[currentTaskIndex].onTaskFinished -= TriggerNextTask;

        // Advance to the next task sequence if available
        if (currentTaskIndex < tasks.Count - 1)
        {
            currentTaskIndex++;
            tasks[currentTaskIndex].StartTask();
            tasks[currentTaskIndex].onTaskFinished += TriggerNextTask;
        }
    }
}