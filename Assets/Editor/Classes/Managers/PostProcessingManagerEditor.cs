using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PostProcessingManager))]
public class PostProcessingManagerEditor : Editor
{
    private PostProcessingManager targetScript;
    private string newConfigName = "NewMoodConfig";

    private void OnEnable()
    {
        targetScript = (PostProcessingManager)target;
        // Ensure the list matches the Enum
        ValidateLiveList();
    }

    public override void OnInspectorGUI()
    {
        ValidateLiveList();

        // Draw the default script field (cause good practice)
        GUI.enabled = false;
        EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((MonoBehaviour)target), typeof(PostProcessingManager), false);
        GUI.enabled = true;

        // Update, to support undo/redo and dirty-handling
        serializedObject.Update();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("References", EditorStyles.boldLabel);

        // FMOD Music Event
        SerializedProperty musicEventProp = serializedObject.FindProperty("musicEvent");
        EditorGUILayout.PropertyField(musicEventProp);

        // Volume Collection rendern
        SerializedProperty collectionProp = serializedObject.FindProperty("volumeCollection");
        EditorGUILayout.PropertyField(collectionProp);

        if (targetScript.volumeCollection == null)
        {
            EditorGUILayout.HelpBox("Assign a VolumeCollection Asset to start.", MessageType.Warning);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        DrawLiveSliders();
        DrawSaveSection();

        // Mark as dirty if changes were made
        if (GUI.changed)
        {
            EditorUtility.SetDirty(targetScript);
        }
        // Important for undo to work
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawLiveSliders()
    {
        GUILayout.Space(20);
        EditorGUILayout.LabelField("PlayMode Mood Controls", new GUIStyle(EditorStyles.largeLabel) { alignment = TextAnchor.MiddleCenter });
        GUILayout.Space(5);

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };

        // Access the activeConfigs list to support undo properly
        SerializedProperty activeList = serializedObject.FindProperty("activeConfigs");

        for (int i = 0; i < activeList.arraySize; i++)
        {
            // Get element and all relevant data
            SerializedProperty entry = activeList.GetArrayElementAtIndex(i);
            SerializedProperty moodProp = entry.FindPropertyRelative("mood");
            SerializedProperty valProp = entry.FindPropertyRelative("volumePercentage");
            string moodName = Enum.GetName(typeof(Mood), moodProp.enumValueIndex);

            // Draw a container box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);

            // Title
            EditorGUILayout.LabelField(moodName, titleStyle);

            // Slider
            EditorGUILayout.BeginHorizontal();
            int currentVal = valProp.intValue;
            int newVal = EditorGUILayout.IntSlider(currentVal, 0, 100);

            // Update if new value is differs the initial value
            if (newVal != currentVal)
            {
                valProp.intValue = newVal;
                // Executes the percentage calculation of the original script
                targetScript.SetDirty();
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
            GUILayout.Space(2);
        }
    }

    private void DrawSaveSection()
    {
        GUILayout.Space(20);
        EditorGUILayout.LabelField("Save Current Configuration", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        {
            GUILayout.Space(10);

            // Name Input
            newConfigName = EditorGUILayout.TextField("ScriptableObject Asset Name", newConfigName);

            GUILayout.Space(5);

            if (GUILayout.Button("Save as New Configuration", GUILayout.Height(30)))
            {
                SaveCurrentSettingsAsAsset();
            }

            GUILayout.Space(10);
            EditorGUILayout.HelpBox("Assets are saved under 'Assets/ScriptableObject/VolumeCollection/Config'.", MessageType.Info);
        }
        EditorGUILayout.EndVertical();
    }

    private void SaveCurrentSettingsAsAsset()
    {
        string folderPath = "Assets/ScriptableObjects/VolumeCollection/Config";

        // Create folder in case it doesn't exist
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
            AssetDatabase.Refresh();
        }

        string fullPath = $"{folderPath}/{newConfigName}.asset";

        // Check if file already exists, and ask whether to overwrite
        if (File.Exists(fullPath))
        {
            bool confirm = EditorUtility.DisplayDialog("Overwrite?",
                $"A config named '{newConfigName}' already exists. Overwrite?", "Yes", "No");
            if (!confirm) { return; }
        }

        // Create VolumeConfiguration Instance
        VolumeConfiguration newSO = ScriptableObject.CreateInstance<VolumeConfiguration>();

        // Copy Data from Manager to the new Asset
        newSO.configs = new List<VolumeConfig>();
        foreach (VolumeConfig active in targetScript.activeConfigs)
        {
            newSO.configs.Add(new VolumeConfig
            {
                mood = active.mood,
                volumePercentage = active.volumePercentage
            });
        }

        // Turn the instance into an Asset and save
        AssetDatabase.CreateAsset(newSO, fullPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Turn focus towards the newly created Asset
        Selection.activeObject = newSO;
        Debug.Log($"<color=green>Saved Volume Configuration:</color> {fullPath}");
    }

    // Keep list of options synchronized with the order and amount of the enums
    private void ValidateLiveList()
    {
        // Get all moods
        List<Mood> currentEnumValues = Enum.GetValues(typeof(Mood)).Cast<Mood>().ToList();

        if (targetScript.activeConfigs == null) { targetScript.activeConfigs = new List<VolumeConfig>(); }

        bool changed = false;
        List<VolumeConfig> newSortedList = new List<VolumeConfig>();

        foreach (Mood mood in currentEnumValues)
        {
            VolumeConfig existingEntry = targetScript.activeConfigs.Find(x => x.mood == mood);

            // Check whether config already exists
            if (existingEntry != null) { newSortedList.Add(existingEntry); }
            // Create a new config if not
            else
            {
                newSortedList.Add(new VolumeConfig { mood = mood, volumePercentage = 0 });
                changed = true;
            }
        }

        // Check whether the amount is still the same as previously (enum was added/deleted)
        if (targetScript.activeConfigs.Count != newSortedList.Count) { changed = true; }

        // Update list if change is detected
        if (changed || IsListOrderDifferent(targetScript.activeConfigs, newSortedList))
        {
            targetScript.activeConfigs = newSortedList;
            EditorUtility.SetDirty(targetScript);
        }
    }

    private bool IsListOrderDifferent(List<VolumeConfig> oldList, List<VolumeConfig> newList)
    {
        for (int i = 0; i < oldList.Count; i++)
        {
            if (oldList[i].mood != newList[i].mood) { return true; }
        }
        return false;
    }
}