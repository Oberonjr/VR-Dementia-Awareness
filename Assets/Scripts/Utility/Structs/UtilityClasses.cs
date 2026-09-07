using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Classes and structs used to keep data within script concise, when needed
/// </summary>

// Used to update tasks on menu
[Serializable]
public struct TaskLine
{
    public TextMeshProUGUI title;
    public TextMeshProUGUI description;
}

[Serializable]
public struct TutorialInstance
{
    public TutorialType tutorialType;
    public GameObject hudInstance;
    public GameObject[] movementComponent;
}

[Serializable]
public struct IndicatorInstance
{
    public IndicatorHUDs indicatorType;
    public GameObject instance;
}

[Serializable]
public struct JulietteAnimationInfo
{
    public JulietteAnimations animationType;
    public GameObject startPosition;
}

[Serializable]
public struct AnimationQueueItem
{
    public string triggerName;
    public JulietteAnimations animType;

    public AnimationQueueItem(string triggerName, JulietteAnimations animType)
    {
        this.triggerName = triggerName;
        this.animType = animType;
    }
}