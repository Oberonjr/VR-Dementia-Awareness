using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// One per scene (or one per mesh you want grass on).
// Scatters grass prefabs over the faces of a mesh that use the materials you tick, and draws them
// with GPU instancing: thousands of clumps for the cost of a handful of draw calls.
//
// Setup: put this on an empty GameObject, drag the A_1 mesh object into "Source", tick the grass
// materials, give each its own preset (prefabs, density, height...), press Rebuild.
// Press Rebuild again whenever A_1 changes or you change a preset.
// Wind: uses the prefabs' own TerrainGrass materials, so WindMaster drives it automatically.
[ExecuteAlways]
[AddComponentMenu("Environment/Grass Scatter")]
public class GrassScatter : MonoBehaviour
{
    [System.Serializable]
    public class GrassPrefab
    {
        public GameObject prefab;
        [Tooltip("How often this one is picked compared to the others.")]
        [Min(0f)] public float weight = 1f;
    }

    // The preset for one lawn material.
    [System.Serializable]
    public class GrassZone
    {
        public Material material;
        public List<GrassPrefab> prefabs = new List<GrassPrefab>();
        [Tooltip("Grass clumps per square metre.")]
        [Range(0.1f, 20f)] public float density = 2f;
        [Tooltip("Random overall size per clump (min, max).")]
        public Vector2 scaleRange = new Vector2(0.8f, 1.2f);
        [Tooltip("Squash or stretch the grass vertically. 0.5 = mown lawn, 1 = as modelled, 1.5 = long meadow.")]
        [Range(0.2f, 2f)] public float height = 1f;
        [Tooltip("0 = grass grows straight up, 1 = leans with the ground slope.")]
        [Range(0f, 1f)] public float alignToGround = 0.3f;
        [Tooltip("No grass closer than this (m) to where the lawn ends - stops clumps spilling onto concrete.")]
        [Range(0f, 2f)] public float edgeMargin = 0.3f;
        [Tooltip("Past the margin, grass thins out gradually over this distance (m) instead of a hard line.")]
        [Range(0f, 3f)] public float edgeSoftness = 0.5f;
        [HideInInspector] public bool expanded = true;   // inspector fold-out state only

        public GrassZone Clone(Material forMaterial)
        {
            var z = (GrassZone)MemberwiseClone();
            z.material = forMaterial;
            z.prefabs = new List<GrassPrefab>();
            foreach (var p in prefabs) z.prefabs.Add(new GrassPrefab { prefab = p.prefab, weight = p.weight });
            return z;
        }
    }

    [Header("Where")]
    [Tooltip("The scene object whose mesh gets grass (A_1's SUBURB_2).")]
    public MeshRenderer source;
    [Tooltip("Skip faces steeper than this (degrees from flat). Stops grass on walls/curbs.")]
    [Range(0f, 90f)] public float maxSlope = 35f;
    [Tooltip("Change for a different random layout.")]
    public int seed = 1234;

    [Tooltip("One preset per ticked lawn material. Edit them in the list below.")]
    public List<GrassZone> zones = new List<GrassZone>();

    [Header("Drawing (Quest budget)")]
    [Tooltip("Grass further than this from the camera isn't drawn at all. Main performance knob.")]
    [Range(5f, 150f)] public float drawDistance = 30f;
    [Tooltip("Size of the chunks grass is grouped into (m). Smaller = finer draw-distance cut-off.")]
    [Range(4f, 50f)] public float chunkSize = 10f;
    [Tooltip("Shadows from grass are expensive on Quest. Leave off unless you really see the difference.")]
    public bool castShadows = false;

    [Header("Light probes (for the 'Fix Light Probes' button)")]
    [Tooltip("The scene's Light Probe Group. Found automatically if empty.")]
    public LightProbeGroup probeGroup;
    [Tooltip("Spacing (m) of the probe layer added over the lawns.")]
    [Range(1f, 6f)] public float probeSpacing = 2.5f;
    [Tooltip("How high above the lawn that layer sits (m) - roughly mid-grass height.")]
    [Range(0.1f, 1f)] public float probeHeight = 0.3f;
    [Tooltip("Cutout materials that are really SOLID blocks (e.g. the 'Bush' hedges). Other cutout materials (tree leaves) are ignored by the probe fixer.")]
    public List<Material> solidCutoutMaterials = new List<Material>();

    [Header("Baked result (made by Rebuild)")]
    public GrassScatterData data;

    // ---- old single-preset settings: only read once to convert an existing setup into zones ----
    [HideInInspector] public List<Material> grassMaterials = new List<Material>();
    [HideInInspector] public List<GrassPrefab> prefabs = new List<GrassPrefab>();
    [HideInInspector] public float density = 2f;
    [HideInInspector] public Vector2 scaleRange = new Vector2(0.8f, 1.2f);
    [HideInInspector] public float alignToGround = 0.3f;
    [HideInInspector] public float edgeMargin = 0.3f;
    [HideInInspector] public float edgeSoftness = 0.5f;

    void OnEnable()  { RenderPipelineManager.beginCameraRendering += Draw; }
    void OnDisable() { RenderPipelineManager.beginCameraRendering -= Draw; }

    // (Per-clump probe lighting via CustomProvided SH arrays was tried on 2026-11-20: TerrainGrass in
    //  URP ignores it and renders with no baked light. Lighting is per chunk - use a smaller Chunk Size
    //  for finer lighting.)
    public void InvalidateLighting() { }

    void Draw(ScriptableRenderContext ctx, Camera cam)
    {
        if (data == null || data.chunks.Count == 0) return;
        if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;

        Vector3 camPos = cam.transform.position;
        float maxSqr = drawDistance * drawDistance;
        var shadows = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

        for (int ci = 0; ci < data.chunks.Count; ci++)
        {
            var c = data.chunks[ci];
            if (c.matrices == null || c.matrices.Length == 0) continue;
            if (c.bounds.SqrDistance(camPos) > maxSqr) continue;
            var t = data.types[c.type];
            if (t.mesh == null) continue;

            for (int sub = 0; sub < t.mesh.subMeshCount && sub < t.materials.Length; sub++)
            {
                if (t.materials[sub] == null) continue;
                var rp = new RenderParams(t.materials[sub])
                {
                    camera = cam,
                    worldBounds = c.bounds,        // Unity frustum-culls the chunk with this
                    shadowCastingMode = shadows,
                    receiveShadows = true,
                    // Baked lighting reaches instanced grass only through light probes
                    // (sampled at the chunk's centre). Without this the grass gets no baked light = dark.
                    lightProbeUsage = LightProbeUsage.BlendProbes,
                    layer = gameObject.layer,
                };
                Graphics.RenderMeshInstanced(rp, t.mesh, sub, c.matrices);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (data == null) return;
        Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.25f);
        foreach (var c in data.chunks) Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}
