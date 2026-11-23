using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
public class InputSceneManager : MonoBehaviour
{
    private SceneAsset _sceneAsset;

    private void Start()
    {
        
    }

    private void OnButtonPress()
    {
        SceneManager.LoadScene(_sceneAsset.name);
    }
}
#endif