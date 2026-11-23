using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// One per scene. The Wind Zone on this object is THE wind for all foliage.
// Rotate this object to change wind direction; edit the Wind Zone to change strength/gusts.
//
// Who listens to what:
//   - SpeedTree trees (Cypress etc.)  -> Unity feeds them from the Wind Zone automatically.
//   - Lux trees/bushes                -> LuxInstantWind (same object) reads the Wind Zone.
//   - TerrainGrass materials (URP pack grass + Bush_A/B etc.) -> this script writes their
//     Wind_Yaw / Wind_Intensity / Wind_Speed so they follow the Wind Zone too.
[ExecuteAlways]
[RequireComponent(typeof(WindZone))]
[AddComponentMenu("Environment/Wind Master")]
public class WindMaster : MonoBehaviour
{
    [System.Serializable]
    public class GrassMaterial
    {
        public Material material;
        [Tooltip("This material's own sway at Wind Main = 1 (captured from the pack's original value).")]
        public float baseIntensity = 0.1f;
        [Tooltip("This material's own sway speed (captured from the pack's original value).")]
        public float baseSpeed = 3f;
    }

    [Header("Grass & URP-pack bushes (TerrainGrass shader)")]
    [Tooltip("Overall sway amount for grass, on top of the Wind Zone's Main value.")]
    [Range(0f, 3f)] public float grassStrength = 1f;
    [Tooltip("Overall sway speed for grass.")]
    [Range(0f, 3f)] public float grassSpeed = 1f;
    [Tooltip("Tick this if the grass leans the opposite way to the trees.")]
    public bool flipGrassDirection = false;

    [Tooltip("Filled automatically by right-click > Find Grass Materials.")]
    public List<GrassMaterial> grassMaterials = new List<GrassMaterial>();

    static readonly int YawID       = Shader.PropertyToID("Wind_Yaw");
    static readonly int IntensityID = Shader.PropertyToID("Wind_Intensity");
    static readonly int SpeedID     = Shader.PropertyToID("Wind_Speed");

    WindZone zone;
    float lastYaw = float.NaN, lastMain, lastStrength, lastSpeed;

    void OnEnable()   { zone = GetComponent<WindZone>(); Apply(true); }
    void OnValidate() { Apply(true); }
    void Update()     { Apply(false); }

    // Only touches the materials when something actually changed (cheap, and keeps git diffs quiet).
    void Apply(bool force)
    {
        if (zone == null) zone = GetComponent<WindZone>();
        if (zone == null) return;

        float yaw  = Mathf.Repeat(transform.eulerAngles.y + (flipGrassDirection ? 180f : 0f), 360f);
        float main = zone.windMain;

        if (!force && Mathf.Approximately(yaw, lastYaw) && Mathf.Approximately(main, lastMain)
            && Mathf.Approximately(grassStrength, lastStrength) && Mathf.Approximately(grassSpeed, lastSpeed))
            return;

        foreach (var g in grassMaterials)
        {
            if (g == null || g.material == null || !g.material.HasProperty(IntensityID)) continue;
            g.material.SetFloat(YawID, yaw);
            g.material.SetFloat(IntensityID, g.baseIntensity * main * grassStrength);
            g.material.SetFloat(SpeedID, g.baseSpeed * grassSpeed);
        }

        lastYaw = yaw; lastMain = main; lastStrength = grassStrength; lastSpeed = grassSpeed;
    }

#if UNITY_EDITOR
    // Finds every material using TerrainGrass.shadergraph and remembers its current sway as the base.
    // Already-listed materials keep their saved base (so running it twice is safe).
    [ContextMenu("Find Grass Materials")]
    public void FindGrassMaterials()
    {
        Undo.RecordObject(this, "Find Grass Materials");
        var known = new HashSet<Material>();
        foreach (var g in grassMaterials) if (g != null && g.material) known.Add(g.material);

        foreach (var guid in AssetDatabase.FindAssets("t:Material"))
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (mat == null || mat.shader == null || known.Contains(mat)) continue;
            if (!AssetDatabase.GetAssetPath(mat.shader).EndsWith("TerrainGrass.shadergraph")) continue;
            grassMaterials.Add(new GrassMaterial
            {
                material = mat,
                baseIntensity = mat.GetFloat(IntensityID),
                baseSpeed = mat.GetFloat(SpeedID),
            });
        }
        Apply(true);
        Debug.Log($"[WindMaster] Driving {grassMaterials.Count} TerrainGrass materials.", this);
    }

    // GameObject > Environment > Wind Master : builds the whole thing in one click.
    [MenuItem("GameObject/Environment/Wind Master", false, 10)]
    static void Create()
    {
        var existing = FindFirstObjectByType<WindMaster>();
        if (existing) { Selection.activeObject = existing; Debug.Log("[WindMaster] Scene already has one - selected it."); return; }

        var go = new GameObject("WindMaster");
        Undo.RegisterCreatedObjectUndo(go, "Create Wind Master");
        var wz = go.AddComponent<WindZone>();
        wz.mode = WindZoneMode.Directional;
        go.AddComponent<LuxInstantVegetation.LuxInstantWind>();
        var wm = go.AddComponent<WindMaster>();
        wm.FindGrassMaterials();
        Selection.activeObject = go;
    }
#endif
}
