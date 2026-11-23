using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class Scenario
{
    public string name;
    public Sprite thumbnail;
    public string scene;
}

public class ScenarioPicker : MonoBehaviour
{
    [SerializeField]
    private Scenario[] scenarios;
    [SerializeField] 
    private ScenarioOption scenarioUITemplate;
    [SerializeField] 
    private float spacing;
    
    private void Start()
    {
        if (FaultySetup()) return;
        
        List<Transform> scenarioObjs = new List<Transform>();
        foreach (Scenario scenario in scenarios)
        {
            ScenarioOption instantiatedUI = Instantiate(scenarioUITemplate, transform);
            
            instantiatedUI.ApplyScenarioValues(scenario);
            
            scenarioObjs.Add(instantiatedUI.transform);
        }
        SpaceOutScenarioObjects(scenarioObjs);
    }

    private bool FaultySetup()
    {
        if (scenarios == null || scenarios.Length == 0)
        {
            Debug.LogError("<color=#FF0000>[ScenarioPicker]</color> No scenarios selected.");
            return true;
        }

        if (scenarioUITemplate == null)
        {
            Debug.LogError("<color=#FF0000>[ScenarioPicker]</color> No template selected in the Scenario Picker component.");
            return true;
        }

        return false;
    }

    private void SpaceOutScenarioObjects(List<Transform> scenarioObjs)
    {
        for (int i = 0; i < scenarioObjs.Count; i++)
        {
            // Set the offset of the center based on the amount of objects.
            // uneven = 1, even = 0
            float centerOffset = scenarioObjs.Count % 2f == 0 ? (spacing == 0 ? 0 : spacing / 2) : 0;
            
            // Set the spacing per object
            float spacingOffset = (scenarioObjs.Count / 2f - 1) * spacing - i * spacing;
            
            // Combine the offsets and set the position of each object
            scenarioObjs[i].localPosition = new Vector3(-1 * (centerOffset + spacingOffset), 0, 0);
            
        }
    }
}