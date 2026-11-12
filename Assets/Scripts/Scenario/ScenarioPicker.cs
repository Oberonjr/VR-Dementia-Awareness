using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class Scenario
{
    public string name;
    public SceneAsset scene;
}

public class ScenarioPicker : MonoBehaviour
{
    [SerializeField]
    private Scenario[] scenarios;

    private SceneController _sceneController;
    
    private void Start()
    {
        _sceneController = GetComponent<SceneController>();
    }

    private void ChooseScenario(int index)
    {
        _sceneController.LoadScene(scenarios[index].scene.name);
    }
}