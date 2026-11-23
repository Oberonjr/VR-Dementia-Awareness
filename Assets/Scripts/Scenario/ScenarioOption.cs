using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Oculus.Interaction;

public class ScenarioOption : MonoBehaviour
{
    [SerializeField]
    private TMP_Text text;
    [SerializeField]
    private Image image;
    [SerializeField]
    private InteractableUnityEventWrapper button;

    private string _scene;
    private SceneController _sceneController;
    
    private void Start()
    {
        if (button != null)
            button.WhenSelect.AddListener(GoToScenario);
    }

    public void ApplyScenarioValues(Scenario scenario)
    {
        if (scenario == null) return;
        
        if (text != null)
            text.SetText(scenario.name);

        if (image != null && scenario.thumbnail != null)
            image.sprite = scenario.thumbnail;
        
        _scene = scenario.scene;
    }

    private void GoToScenario()
    {
        if (_scene == "") return;
        _sceneController.LoadScene(_scene);
    }
}
