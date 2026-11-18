using UnityEngine;
using UnityEngine.SceneManagement;
using Udar.SceneManager;

public class InputSceneManager : MonoBehaviour
{
    private SceneField _sceneAsset;

    private void OnButtonPress()
    {
        SceneManager.LoadScene(_sceneAsset.Name);
    }
}