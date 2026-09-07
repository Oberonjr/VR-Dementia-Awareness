using UnityEngine;

/// <summary>
/// Updates the task list in the handheld menu
/// </summary>

public class UpdateTaskList : MonoBehaviour
{
    [Header("Task References")]
    [SerializeField] private TaskLine[] tasks;
    [SerializeField] private Color inactiveColor;
    [SerializeField] private Color activeColor;

    private int currentIndex;

    private void Start()
    {
        EventBus<OnUpdateTask>.OnEvent += UpdateTask;
    }

    private void OnDestroy()
    {
        EventBus<OnUpdateTask>.OnEvent -= UpdateTask;
    }

    private void UpdateTask(OnUpdateTask evt)
    {
        if (currentIndex >= tasks.Length) { return; }

        if (currentIndex - 1 >= 0)
        {
            tasks[currentIndex - 1].title.color = inactiveColor;
            tasks[currentIndex - 1].description.color = inactiveColor;
        }

        tasks[currentIndex].title.color = activeColor;
        tasks[currentIndex].description.color = activeColor;

        currentIndex++;
    }
}