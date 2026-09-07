using System;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// The classes are used similar to a struct
/// It is not defined as a struct however, since modifying it would only yield a copy, instead of a reference
/// This avoids the issue of having to write extensive replacing logic in VolumeCollectionEditor.cs for example
/// </summary>

/// <summary>
/// VolumeEntry is used for defining different types of volumes for the available moods
/// </summary>

[Serializable]
public class VolumeEntry
{
    public Mood mood;
    public string paramFMOD;
    public GameObject volume;
}

/// <summary>
/// Volume Config is used to adjust the mix of different volumes more easily
/// </summary>
[Serializable]
public class VolumeConfig
{
    public Mood mood;

    [Range(0, 100)]
    public int volumePercentage;
}