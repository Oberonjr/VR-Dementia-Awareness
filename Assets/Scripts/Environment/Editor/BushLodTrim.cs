using System.IO;
using UnityEditor;
using UnityEngine;

// Tools > Environment > Bush LODs > Start From LOD1 / LOD2 / Restore Original
//
// Makes the chosen LOD the close-up mesh in the Yughues bush prefabs (the LOD0 meshes have
// leaf cards stacked on top of each other, which shimmers). The skipped LOD objects are switched
// off, not deleted.
// Works on the bush prefabs selected in the Project window, or on all 5 P_Bush prefabs if none are selected.
//
// Safety: before the first change, each prefab is copied to Prefabs/Backup~/ (Unity ignores "~"
// folders). Every "Start From" begins from that original, so you can switch LOD1 <-> LOD2 freely.
public static class BushLodTrim
{
    const string PrefabDir = "Assets/YughuesFreeBushes2018/Prefabs";
    const string BackupDir = PrefabDir + "/Backup~";

    [MenuItem("Tools/Environment/Bush LODs/Start From LOD1")] static void From1() => Apply(1);
    [MenuItem("Tools/Environment/Bush LODs/Start From LOD2")] static void From2() => Apply(2);

    [MenuItem("Tools/Environment/Bush LODs/Restore Original")]
    static void Restore()
    {
        int n = 0;
        foreach (var path in Targets())
            if (RestoreFromBackup(path)) n++;
        AssetDatabase.Refresh();
        Debug.Log($"[BushLodTrim] Restored {n} prefab(s) to their original LODs.");
    }

    static void Apply(int startLod)
    {
        Directory.CreateDirectory(BackupDir);
        int n = 0;
        foreach (var path in Targets())
        {
            string backup = BackupPath(path);
            if (!File.Exists(backup)) File.Copy(path, backup);   // first time only: keep the original
            else RestoreFromBackup(path);                         // always start from the original

            var root = PrefabUtility.LoadPrefabContents(path);
            var lg = root.GetComponentInChildren<LODGroup>(true);
            var lods = lg ? lg.GetLODs() : null;
            if (lods == null || lods.Length <= startLod)
            {
                Debug.LogWarning($"[BushLodTrim] {Path.GetFileName(path)}: no LOD{startLod}, skipped.");
                PrefabUtility.UnloadPrefabContents(root);
                continue;
            }

            var kept = new LOD[lods.Length - startLod];
            System.Array.Copy(lods, startLod, kept, 0, kept.Length);

            // Switch off renderers that only belonged to the skipped LODs.
            for (int i = 0; i < startLod; i++)
                foreach (var r in lods[i].renderers)
                    if (r && System.Array.TrueForAll(kept, k => System.Array.IndexOf(k.renderers, r) < 0))
                        r.gameObject.SetActive(false);

            lg.SetLODs(kept);
            lg.RecalculateBounds();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            n++;
        }
        AssetDatabase.Refresh();
        Debug.Log($"[BushLodTrim] {n} prefab(s) now start from LOD{startLod}. Undo with Bush LODs > Restore Original.");
    }

    static bool RestoreFromBackup(string path)
    {
        string backup = BackupPath(path);
        if (!File.Exists(backup)) return false;
        File.Copy(backup, path, true);   // .meta untouched, so the prefab GUID and scene links stay
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        return true;
    }

    static string BackupPath(string prefabPath) => $"{BackupDir}/{Path.GetFileName(prefabPath)}";

    static string[] Targets()
    {
        var picked = new System.Collections.Generic.List<string>();
        foreach (var o in Selection.GetFiltered<GameObject>(SelectionMode.Assets))
        {
            string p = AssetDatabase.GetAssetPath(o);
            if (p.StartsWith(PrefabDir) && p.EndsWith(".prefab")) picked.Add(p);
        }
        if (picked.Count > 0) return picked.ToArray();
        var all = new string[5];
        for (int i = 0; i < 5; i++) all[i] = $"{PrefabDir}/P_Bush0{i + 1}.prefab";
        return all;
    }
}
