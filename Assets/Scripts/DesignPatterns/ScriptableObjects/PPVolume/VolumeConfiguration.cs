using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Used to set exact percentages for each individual moods
/// Moods can be mixed together to form a mix of visuals and sounds
/// </summary>

[CreateAssetMenu(fileName = "VolumeConfiguration", menuName = "Scriptable Objects/VolumeConfiguration")]
public class VolumeConfiguration : ScriptableObject
{
    public List<VolumeConfig> configs = new List<VolumeConfig>();
}