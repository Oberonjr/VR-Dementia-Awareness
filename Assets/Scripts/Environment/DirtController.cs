using UnityEngine;

// One per scene. Controls the dirt look for every material using "Custom/Dirt Lit".
// Move this object up/down: its Y position is the ground level the dirt starts from.
[ExecuteAlways]
[AddComponentMenu("Environment/Dirt Controller")]
public class DirtController : MonoBehaviour
{
    [Header("Where the dirt goes")]
    [Tooltip("How high (in meters) the dirt climbs up walls from this object's Y position.")]
    [Min(0.01f)] public float dirtHeight = 1.0f;
    [Tooltip("Higher = dirt hugs the ground more tightly; lower = softer, taller fade.")]
    [Range(0.2f, 6f)] public float falloff = 2f;
    [Tooltip("Dirt in corners where surfaces meet (needs URP's SSAO renderer feature).")]
    [Range(0f, 3f)] public float cornerDirt = 1.5f;
    [Tooltip("Dust settled on upward-facing ledges, sills and roofs.")]
    [Range(0f, 1f)] public float settledDust = 0.3f;

    [Header("Look")]
    public Color dirtColor = new Color(0.36f, 0.30f, 0.23f);
    [Tooltip("Optional tileable dirt/grime texture. Leave empty for flat color.")]
    public Texture2D dirtTexture;
    [Tooltip("Texture repeats per meter.")]
    public float textureTiling = 0.5f;
    [Range(0f, 1f)] public float dirtRoughness = 0.95f;

    [Header("Breakup")]
    [Tooltip("Size of the dirt patches. Higher = smaller patches.")]
    public float noiseScale = 1.5f;
    [Tooltip("0 = smooth gradient, 1 = very patchy.")]
    [Range(0f, 1f)] public float breakup = 0.6f;

    [Header("Master")]
    [Range(0f, 1f)] public float strength = 1f;
    [Tooltip("Shows the dirt mask in black & white so you can see exactly where it lands.")]
    public bool previewMask;

    static readonly int GroundHeightID = Shader.PropertyToID("_DirtGroundHeight");
    static readonly int HeightID = Shader.PropertyToID("_DirtHeight");
    static readonly int FalloffID = Shader.PropertyToID("_DirtFalloff");
    static readonly int CreviceID = Shader.PropertyToID("_DirtCrevice");
    static readonly int TopDustID = Shader.PropertyToID("_DirtTopDust");
    static readonly int ColorID = Shader.PropertyToID("_DirtColor");
    static readonly int TexID = Shader.PropertyToID("_DirtTex");
    static readonly int TilingID = Shader.PropertyToID("_DirtTiling");
    static readonly int RoughnessID = Shader.PropertyToID("_DirtRoughness");
    static readonly int NoiseScaleID = Shader.PropertyToID("_DirtNoiseScale");
    static readonly int BreakupID = Shader.PropertyToID("_DirtBreakup");
    static readonly int StrengthID = Shader.PropertyToID("_DirtStrength");
    static readonly int DebugID = Shader.PropertyToID("_DirtDebug");

    void OnEnable() => Apply();
    void OnValidate() => Apply();
    void Update() => Apply();

    void OnDisable()
    {
        Shader.SetGlobalFloat(StrengthID, 0f);
        Shader.SetGlobalFloat(DebugID, 0f);
    }

    void Apply()
    {
        Shader.SetGlobalFloat(GroundHeightID, transform.position.y);
        Shader.SetGlobalFloat(HeightID, dirtHeight);
        Shader.SetGlobalFloat(FalloffID, falloff);
        Shader.SetGlobalFloat(CreviceID, cornerDirt);
        Shader.SetGlobalFloat(TopDustID, settledDust);
        Shader.SetGlobalColor(ColorID, dirtColor);
        Shader.SetGlobalTexture(TexID, dirtTexture != null ? dirtTexture : Texture2D.whiteTexture);
        Shader.SetGlobalFloat(TilingID, textureTiling);
        Shader.SetGlobalFloat(RoughnessID, dirtRoughness);
        Shader.SetGlobalFloat(NoiseScaleID, noiseScale);
        Shader.SetGlobalFloat(BreakupID, breakup);
        Shader.SetGlobalFloat(StrengthID, strength);
        Shader.SetGlobalFloat(DebugID, previewMask ? 1f : 0f);
    }

    // Draws the ground level and the top of the dirt band when selected
    void OnDrawGizmosSelected()
    {
        Vector3 p = transform.position;
        Gizmos.color = new Color(0.55f, 0.4f, 0.25f, 0.25f);
        Gizmos.DrawCube(p, new Vector3(40f, 0.01f, 40f));
        Gizmos.color = new Color(0.55f, 0.4f, 0.25f, 1f);
        Gizmos.DrawWireCube(p + Vector3.up * dirtHeight, new Vector3(40f, 0f, 40f));
    }
}
