using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

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