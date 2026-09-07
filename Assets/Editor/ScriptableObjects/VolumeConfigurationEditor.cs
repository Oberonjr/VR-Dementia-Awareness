using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VolumeConfiguration))]
public class VolumeConfigurationEditor : Editor
{
    private VolumeConfiguration targetScript;

    private void OnEnable()
    {
        targetScript = (VolumeConfiguration)target;
        ValidateList();
    }

    public override void OnInspectorGUI()
    {
        ValidateList();

        serializedObject.Update();

        // Textstyle
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12
        };

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Mood Percentages", new GUIStyle(EditorStyles.largeLabel) { alignment = TextAnchor.MiddleCenter });
        GUILayout.Space(10);

        for (int i = 0; i < targetScript.configs.Count; i++)
        {
            VolumeConfig entry = targetScript.configs[i];

            // Draw a container box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);

            // Mood Title
            EditorGUILayout.LabelField(entry.mood.ToString(), titleStyle);

            GUILayout.Space(2);

            // Slider
            EditorGUILayout.BeginHorizontal();
            {
                int newVal = EditorGUILayout.IntSlider(entry.volumePercentage, 0, 100);

                if (newVal != entry.volumePercentage)
                {
                    Undo.RecordObject(targetScript, "Change Volume Percentage");
                    entry.volumePercentage = newVal;
                }
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
        }

        if (GUI.changed)
        {
            EditorUtility.SetDirty(targetScript);
        }
        serializedObject.ApplyModifiedProperties();
    }

    // Checks whether list needs to be updated
    private void ValidateList()
    {
        List<Mood> currentEnumValues = Enum.GetValues(typeof(Mood)).Cast<Mood>().ToList();
        if (targetScript.configs == null) { targetScript.configs = new List<VolumeConfig>(); }

        bool changed = false;
        List<VolumeConfig> newSortedList = new List<VolumeConfig>();

        foreach (Mood mood in currentEnumValues)
        {
            // Check whether data exists for this mood
            VolumeConfig existingEntry = targetScript.configs.Find(x => x.mood == mood);

            // Apply old value if available
            if (existingEntry != null) { newSortedList.Add(existingEntry); }
            // Apply new value if not
            else
            {
                newSortedList.Add(new VolumeConfig { mood = mood, volumePercentage = 0 });
                changed = true;
            }
        }

        // Check whether new moods were added to the enum
        if (targetScript.configs.Count != newSortedList.Count) { changed = true; }

        // Replace list with newly sorted one if new enums were added
        if (changed || IsListOrderDifferent(targetScript.configs, newSortedList))
        {
            targetScript.configs = newSortedList;
            EditorUtility.SetDirty(targetScript);
        }
    }

    private bool IsListOrderDifferent(List<VolumeConfig> oldList, List<VolumeConfig> newList)
    {
        for (int i = 0; i < oldList.Count; i++)
        {
            if (oldList[i].mood != newList[i].mood) return true;
        }
        return false;
    }
}