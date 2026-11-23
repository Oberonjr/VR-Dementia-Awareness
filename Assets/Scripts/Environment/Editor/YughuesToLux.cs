using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Tools > Environment > Yughues Bushes -> Lux       : puts the 5 Yughues bushes on Lux's Leaves shader (wind!)
// Tools > Environment > Yughues Bushes -> Original  : puts the prefabs back on the old URP/Lit materials
//
// Non-destructive: creates NEW materials M_BushXX_Lux next to the old ones and swaps them in the
// P_BushXX prefabs. The old M_BushXX materials are left untouched.
// Needs the T_BushXX_Mask.png textures made by graduation/tools/pack_lux_mask.py.
public static class YughuesToLux
{
    const string Root      = "Assets/YughuesFreeBushes2018/";
    const string LuxShader = "Assets/Forst/Lux Instant Vegetation/Core/Shaders/URP/Leaves.shadergraph";
    // Lux's own bush material: used as the starting point for all wind / translucency settings.
    const string Template  = "Assets/Forst/Lux Instant Vegetation/Demo/Content/URP/Materials/M_RedBush URP.mat";

    [MenuItem("Tools/Environment/Yughues Bushes -> Lux")]
    static void ToLux()
    {
        var shader   = AssetDatabase.LoadAssetAtPath<Shader>(LuxShader);
        var template = AssetDatabase.LoadAssetAtPath<Material>(Template);
        if (shader == null || template == null) { Debug.LogError("[YughuesToLux] Lux Leaves shader or M_RedBush URP template not found."); return; }

        var swap = new Dictionary<Material, Material>();
        for (int i = 1; i <= 5; i++)
        {
            string id = $"Bush0{i}";
            var oldMat = AssetDatabase.LoadAssetAtPath<Material>($"{Root}Materials/M_{id}.mat");
            string maskPath = $"{Root}Textures/T_{id}_Mask.png";
            if (oldMat == null) { Debug.LogWarning($"[YughuesToLux] M_{id} missing, skipped."); continue; }
            if (!File.Exists(maskPath)) { Debug.LogError($"[YughuesToLux] {maskPath} missing - run pack_lux_mask.py --go first."); return; }

            // Mask holds data, not colour: must be linear (sRGB off).
            var imp = (TextureImporter)AssetImporter.GetAtPath(maskPath);
            if (imp.sRGBTexture) { imp.sRGBTexture = false; imp.alphaIsTransparency = false; imp.SaveAndReimport(); }

            string newPath = $"{Root}Materials/M_{id}_Lux.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(newPath);
            if (mat == null)
            {
                mat = new Material(template) { name = $"M_{id}_Lux" };   // copies Lux bush wind settings
                AssetDatabase.CreateAsset(mat, newPath);
            }
            mat.shader = shader;
            mat.SetTexture("_BaseColorMap", oldMat.GetTexture("_BaseMap"));
            mat.SetTexture("_Normal_Map",   oldMat.GetTexture("_BumpMap"));
            mat.SetTexture("_MaskMap",      AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath));
            mat.SetFloat("_AlphaThreshold", oldMat.GetFloat("_Cutoff"));
            mat.SetFloat("_Smoothness", 1f);          // use the authored gloss as-is
            mat.SetFloat("_VertexAO", 0f);            // Yughues meshes have no vertex colours
            mat.SetFloat("_FlipBackfaceNormals", 1f); // correct lighting on the back of leaves
            var hue = mat.GetColor("_HueVariation"); hue.a = 0f;
            mat.SetColor("_HueVariation", hue);      // no orange per-plant tint: keep Yughues' own colours
            mat.enableInstancing = true;
            mat.doubleSidedGI = true;
            EditorUtility.SetDirty(mat);
            swap[oldMat] = mat;
        }
        AssetDatabase.SaveAssets();
        int n = SwapInPrefabs(swap);
        Debug.Log($"[YughuesToLux] Done: {swap.Count} Lux materials, {n} renderers switched. Needs a WindMaster in the scene to move.");
    }

    [MenuItem("Tools/Environment/Yughues Bushes -> Original")]
    static void ToOriginal()
    {
        var swap = new Dictionary<Material, Material>();
        for (int i = 1; i <= 5; i++)
        {
            var lux = AssetDatabase.LoadAssetAtPath<Material>($"{Root}Materials/M_Bush0{i}_Lux.mat");
            var old = AssetDatabase.LoadAssetAtPath<Material>($"{Root}Materials/M_Bush0{i}.mat");
            if (lux && old) swap[lux] = old;
        }
        int n = SwapInPrefabs(swap);
        Debug.Log($"[YughuesToLux] Reverted {n} renderers to the original URP/Lit materials (Lux materials kept).");
    }

    static int SwapInPrefabs(Dictionary<Material, Material> swap)
    {
        int count = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Root + "Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                    if (mats[m] != null && swap.TryGetValue(mats[m], out var to)) { mats[m] = to; changed = true; count++; }
                r.sharedMaterials = mats;
            }
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
        return count;
    }
}
