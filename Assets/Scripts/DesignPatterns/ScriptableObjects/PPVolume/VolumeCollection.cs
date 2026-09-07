using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes it possible to create a collection of moods
/// With this, it's easier to create different volume collections for testing purposes
/// </summary>

[CreateAssetMenu(fileName = "VolumeCollection", menuName = "Scriptable Objects/VolumeCollection")]
public class VolumeCollection : ScriptableObject
{
    public List<VolumeEntry> entries = new List<VolumeEntry>();
}
