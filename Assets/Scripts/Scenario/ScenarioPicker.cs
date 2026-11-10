using System;
using Udar.SceneManager;
using UnityEngine;

[Serializable]
public class Scenario
{
    public string name;
    public SceneField scene;
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
        _sceneController.LoadScene(scenarios[index].scene.Name);
    }
}