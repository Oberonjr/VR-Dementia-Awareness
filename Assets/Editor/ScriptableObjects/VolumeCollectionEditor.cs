using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// This script enables user-friendly management of volumes connected to moods
/// It features adaptive enum selection, removing entries that are already contained, and warning for missing references
/// The UI is also made adaptive, to match any screen size
/// </summary>

[CustomEditor(typeof(VolumeCollection))]
public class VolumeCollectionEditor : Editor
{
    private VolumeCollection targetScript;

    private void OnEnable()
    {
        targetScript = (VolumeCollection)target;
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
        EditorGUILayout.LabelField("Mood Volume References", new GUIStyle(EditorStyles.largeLabel) { alignment = TextAnchor.MiddleCenter });
        GUILayout.Space(10);

        for (int i = 0; i < targetScript.entries.Count; i++)
        {
            VolumeEntry entry = targetScript.entries[i];

            // Draw a container box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);

            // Mood Title
            EditorGUILayout.LabelField(entry.mood.ToString(), titleStyle);
            GUILayout.Space(5);

            // Draws a text field for the FMOD parameter and saves it if changed
            string newParam = EditorGUILayout.TextField("FMOD Parameter", entry.paramFMOD);
            if (newParam != entry.paramFMOD)
            {
                Undo.RecordObject(targetScript, "Change FMOD Parameter");
                entry.paramFMOD = newParam;
            }

            GUILayout.Space(2);

            // Changed to EditorGUILayout to perfectly align with the TextField above
            GameObject newVol = (GameObject)EditorGUILayout.ObjectField("Volume Prefab", entry.volume, typeof(GameObject), false);
            if (newVol != entry.volume)
            {
                Undo.RecordObject(targetScript, "Assign Volume Prefab");
                entry.volume = newVol;
            }

            GUILayout.Space(5);

            // Draw warning if FMOD parameter is missing
            if (string.IsNullOrEmpty(entry.paramFMOD))
            {
                EditorGUILayout.HelpBox("FMOD Parameter string is empty.", MessageType.Info);
            }

            // Draw error if Volume GameObject is missing
            if (entry.volume == null)
            {
                EditorGUILayout.HelpBox("Missing Volume GameObject.", MessageType.Warning);
            }

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

    private void ValidateList()
    {
        List<Mood> currentEnumValues = Enum.GetValues(typeof(Mood)).Cast<Mood>().ToList();

        if (targetScript.entries == null)
            targetScript.entries = new List<VolumeEntry>();

        bool changed = false;
        List<VolumeEntry> newSortedList = new List<VolumeEntry>();

        foreach (Mood mood in currentEnumValues)
        {
            // Check whether data exists for this mood
            VolumeEntry existingEntry = targetScript.entries.Find(x => x.mood == mood);

            // Apply old value if available
            if (existingEntry != null) { newSortedList.Add(existingEntry); }
            // Apply new value if not
            else
            {
                newSortedList.Add(new VolumeEntry { mood = mood, paramFMOD = "", volume = null });
                changed = true;
            }
        }

        // Check whether new moods were added to the enum
        if (targetScript.entries.Count != newSortedList.Count)
        {
            changed = true;
        }

        // Replace list with newly sorted one if new enums were added
        if (changed || IsListOrderDifferent(targetScript.entries, newSortedList))
        {
            targetScript.entries = newSortedList;
            EditorUtility.SetDirty(targetScript);
        }
    }

    private bool IsListOrderDifferent(List<VolumeEntry> oldList, List<VolumeEntry> newList)
    {
        for (int i = 0; i < oldList.Count; i++)
        {
            if (oldList[i].mood != newList[i].mood) return true;
        }
        return false;
    }
}