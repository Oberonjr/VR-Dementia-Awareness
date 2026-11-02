using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(TaskCaller))]
public class TaskCallerEditor : Editor
{
    private TaskCaller _script;

    private void OnEnable()
    {
        _script = (TaskCaller)target;
    }

    public override void OnInspectorGUI()
    {
        if (_script.CallerValid())
        {
            if (GUILayout.Button("Finish Task"))
            {
                _script.FinishTask();
            }
        }
    }
}