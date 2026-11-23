using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Inspector for GrassScatter: one preset ("zone") per ticked lawn material, Rebuild / Clear, stats.
// The actual scattering happens here (editor only); the result is saved into a GrassScatterData asset.
[CustomEditor(typeof(GrassScatter))]
public class GrassScatterEditor : Editor
{
    const string GrassDir = "Assets/TerrainDemoScene_URP/Prefabs/Details/";

    void OnEnable() => MigrateOldSetup((GrassScatter)target);

    // Converts the old "one preset for everything" setup into one zone per ticked material.
    static void MigrateOldSetup(GrassScatter gs)
    {
        if (gs.zones.Count > 0 || gs.grassMaterials.Count == 0) return;
        Undo.RecordObject(gs, "Convert grass setup to zones");
        foreach (var m in gs.grassMaterials)
        {
            if (m == null) continue;
            var z = new GrassScatter.GrassZone
            {
                material = m, density = gs.density, scaleRange = gs.scaleRange, alignToGround = gs.alignToGround,
                edgeMargin = gs.edgeMargin, edgeSoftness = gs.edgeSoftness,
            };
            foreach (var p in gs.prefabs) z.prefabs.Add(new GrassScatter.GrassPrefab { prefab = p.prefab, weight = p.weight });
            gs.zones.Add(z);
        }
        gs.grassMaterials.Clear();
        EditorUtility.SetDirty(gs);
    }

    public override void OnInspectorGUI()
    {
        var gs = (GrassScatter)target;
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("source"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxSlope"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("seed"));
        serializedObject.ApplyModifiedProperties();

        // ---- one row per material on the mesh; ticked ones get a preset ----
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Grass presets per material", EditorStyles.boldLabel);
        var mf = gs.source ? gs.source.GetComponent<MeshFilter>() : null;
        if (mf == null || mf.sharedMesh == null)
            EditorGUILayout.HelpBox("Drag the A_1 mesh object (SUBURB_2) into Source first.", MessageType.Info);
        else
        {
            var seen = new HashSet<Material>();
            var mats = gs.source.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null || !seen.Add(m)) continue;
                int tris = i < mf.sharedMesh.subMeshCount ? (int)mf.sharedMesh.GetSubMesh(i).indexCount / 3 : 0;
                int zi = gs.zones.FindIndex(z => z.material == m);
                bool on = zi >= 0;
                var style = m.name.ToLower().Contains("grass") ? EditorStyles.boldLabel : EditorStyles.label;
                bool now = EditorGUILayout.ToggleLeft($"{m.name}   ({tris} tris)", on, style);
                if (now != on)
                {
                    Undo.RecordObject(gs, "Toggle grass material");
                    if (now) gs.zones.Add(NewZone(gs, m)); else gs.zones.RemoveAt(zi);
                    EditorUtility.SetDirty(gs);
                    GUIUtility.ExitGUI();
                }
                if (on) DrawZone(gs, zi);
            }
        }

        // ---- drawing + bake ----
        EditorGUILayout.Space(8);
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("drawDistance"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("chunkSize"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("castShadows"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("data"));
        serializedObject.ApplyModifiedProperties();
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button("Rebuild", GUILayout.Height(28))) Rebuild(gs);
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("Clear", GUILayout.Height(28), GUILayout.Width(70))) Clear(gs);
        }
        if (gs.data != null)
            EditorGUILayout.HelpBox($"{gs.data.totalInstances:N0} clumps in {gs.data.chunks.Count} chunks.", MessageType.None);

        // ---- light probes ----
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Light probes", EditorStyles.boldLabel);
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("probeGroup"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("probeSpacing"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("probeHeight"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("solidCutoutMaterials"), true);
        serializedObject.ApplyModifiedProperties();
        if (GUILayout.Button("Fix Light Probes (remove buried ones + add lawn layer)", GUILayout.Height(24)))
            ProbeFixer.Fix(gs);
        EditorGUILayout.HelpBox("Undo with Ctrl+Z. Afterwards re-bake: Window > Rendering > Lighting > Generate Lighting.", MessageType.None);
    }

    // A newly ticked material starts as a copy of the last preset (or sensible defaults + Grass_A-D).
    static GrassScatter.GrassZone NewZone(GrassScatter gs, Material m)
    {
        if (gs.zones.Count > 0) return gs.zones[gs.zones.Count - 1].Clone(m);
        var z = new GrassScatter.GrassZone { material = m };
        AddPrefabs(z, "Grass_A", "Grass_B", "Grass_C", "Grass_D");
        return z;
    }

    void DrawZone(GrassScatter gs, int zi)
    {
        var zone = gs.zones[zi];
        serializedObject.Update();
        var zp = serializedObject.FindProperty("zones").GetArrayElementAtIndex(zi);

        EditorGUI.indentLevel++;
        zone.expanded = EditorGUILayout.Foldout(zone.expanded, "Preset", true);
        if (zone.expanded)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(zp.FindPropertyRelative("prefabs"), true);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+ Grass_A-D"))    { Undo.RecordObject(gs, "Add grass"); AddPrefabs(zone, "Grass_A", "Grass_B", "Grass_C", "Grass_D"); }
                    if (GUILayout.Button("+ GrassDry_A-D")) { Undo.RecordObject(gs, "Add grass"); AddPrefabs(zone, "GrassDry_A", "GrassDry_B", "GrassDry_C", "GrassDry_D"); }
                }
                EditorGUILayout.PropertyField(zp.FindPropertyRelative("density"));
                EditorGUILayout.PropertyField(zp.FindPropertyRelative("height"));
                EditorGUILayout.PropertyField(zp.FindPropertyRelative("scaleRange"));
                EditorGUILayout.PropertyField(zp.FindPropertyRelative("alignToGround"));
                EditorGUILayout.PropertyField(zp.FindPropertyRelative("edgeMargin"));
                EditorGUILayout.PropertyField(zp.FindPropertyRelative("edgeSoftness"));

                // copy another zone's preset
                var others = new List<string> { "Copy preset from..." };
                var idx = new List<int> { -1 };
                for (int j = 0; j < gs.zones.Count; j++)
                    if (j != zi && gs.zones[j].material) { others.Add(gs.zones[j].material.name); idx.Add(j); }
                if (others.Count > 1)
                {
                    int pick = EditorGUILayout.Popup(0, others.ToArray());
                    if (pick > 0)
                    {
                        Undo.RecordObject(gs, "Copy grass preset");
                        var copy = gs.zones[idx[pick]].Clone(zone.material);
                        copy.expanded = true;
                        gs.zones[zi] = copy;
                        EditorUtility.SetDirty(gs);
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }
        EditorGUI.indentLevel--;
        serializedObject.ApplyModifiedProperties();
    }

    static void AddPrefabs(GrassScatter.GrassZone z, params string[] names)
    {
        foreach (var n in names)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(GrassDir + n + ".prefab");
            if (p != null && !z.prefabs.Exists(x => x.prefab == p)) z.prefabs.Add(new GrassScatter.GrassPrefab { prefab = p });
        }
    }

    static void Clear(GrassScatter gs)
    {
        if (gs.data == null) return;
        gs.data.chunks.Clear(); gs.data.types.Clear(); gs.data.totalInstances = 0;
        gs.InvalidateLighting();
        EditorUtility.SetDirty(gs.data);
        AssetDatabase.SaveAssetIfDirty(gs.data);
    }

    static void Rebuild(GrassScatter gs)
    {
        var mf = gs.source ? gs.source.GetComponent<MeshFilter>() : null;
        if (mf == null || mf.sharedMesh == null) { Debug.LogError("[GrassScatter] No source mesh."); return; }
        if (gs.zones.Count == 0)                  { Debug.LogError("[GrassScatter] Tick at least one grass material."); return; }

        // --- grass types (unique prefabs across all zones) + each zone's weighted pick list ---
        var types = new List<GrassScatterData.GrassType>();
        var locals = new List<Matrix4x4>();
        var typeOf = new Dictionary<GameObject, int>();
        var zonePicks = new List<List<(int type, float w)>>();
        float typeRadius = 0.5f;
        foreach (var zone in gs.zones)
        {
            var picks = new List<(int, float)>();
            foreach (var gp in zone.prefabs)
            {
                if (gp == null || gp.prefab == null || gp.weight <= 0f) continue;
                if (!typeOf.TryGetValue(gp.prefab, out int t))
                {
                    var pmf = gp.prefab.GetComponentInChildren<MeshFilter>(true);
                    var pmr = pmf ? pmf.GetComponent<MeshRenderer>() : null;
                    if (pmf == null || pmr == null || pmf.sharedMesh == null) { Debug.LogWarning($"[GrassScatter] {gp.prefab.name} has no mesh, skipped."); continue; }
                    foreach (var m in pmr.sharedMaterials)
                        if (m != null && !m.enableInstancing) { m.enableInstancing = true; EditorUtility.SetDirty(m); }
                    t = types.Count;
                    types.Add(new GrassScatterData.GrassType { mesh = pmf.sharedMesh, materials = pmr.sharedMaterials });
                    locals.Add(gp.prefab.transform.worldToLocalMatrix * pmf.transform.localToWorldMatrix);
                    typeOf[gp.prefab] = t;
                    typeRadius = Mathf.Max(typeRadius, pmf.sharedMesh.bounds.extents.magnitude + pmf.sharedMesh.bounds.center.magnitude);
                }
                picks.Add((t, gp.weight));
            }
            zonePicks.Add(picks);
            if (picks.Count == 0 && zone.material) Debug.LogWarning($"[GrassScatter] '{zone.material.name}' has no grass prefabs - it will stay bare.");
        }
        if (types.Count == 0) { Debug.LogError("[GrassScatter] Add grass prefabs to at least one preset."); return; }

        // --- read the source mesh ---
        var mesh = mf.sharedMesh;
        var verts = mesh.vertices;
        if (verts == null || verts.Length == 0)
        {
            Debug.LogError($"[GrassScatter] Can't read {mesh.name}. Tick Read/Write on its FBX import settings, Apply, then Rebuild.");
            return;
        }
        var l2w = gs.source.transform.localToWorldMatrix;
        // A_1 comes in with scale (-100,-100,-100) = a mirror image. Unity renders mirrored objects fine,
        // but face directions computed from them come out reversed unless we correct for it.
        bool mirrored = l2w.determinant < 0f;
        var mats = gs.source.sharedMaterials;
        float minUp = Mathf.Cos(gs.maxSlope * Mathf.Deg2Rad);
        var rng = new System.Random(gs.seed);
        float R() => (float)rng.NextDouble();

        // --- lawn triangles grass can grow on (ticked materials, facing up, not too steep) ---
        // Filtering BEFORE edge detection matters: a lawn slab's sides/bottom would otherwise hide the border.
        var lawn = new List<(Vector3 a, Vector3 b, Vector3 c)>();
        var lawnSub = new List<int>();
        var lawnZone = new List<int>();
        var stats = new Dictionary<int, int[]>();   // per slot: [tris, up, down, steep, placed, held at edge]
        var subArea = new Dictionary<int, float>();
        float maxEdge = 0f;
        foreach (var z in gs.zones) maxEdge = Mathf.Max(maxEdge, z.edgeMargin + z.edgeSoftness);

        for (int sub = 0; sub < mesh.subMeshCount && sub < mats.Length; sub++)
        {
            int zi = gs.zones.FindIndex(z => z.material == mats[sub]);
            if (zi < 0) continue;
            var st = stats[sub] = new int[6]; subArea[sub] = 0f;
            var tris = mesh.GetTriangles(sub);
            for (int i = 0; i < tris.Length; i += 3)
            {
                var a = l2w.MultiplyPoint3x4(verts[tris[i]]);
                var b = l2w.MultiplyPoint3x4(verts[tris[i + 1]]);
                var c = l2w.MultiplyPoint3x4(verts[tris[i + 2]]);
                if (mirrored) (b, c) = (c, b);   // mirrored object: swap winding so "up" is really up
                var cr = Vector3.Cross(b - a, c - a);
                st[0]++;
                if (cr.sqrMagnitude < 1e-12f) continue;
                float ny = cr.normalized.y;
                if (ny < -minUp) { st[2]++; continue; }
                if (ny < minUp)  { st[3]++; continue; }
                st[1]++; subArea[sub] += cr.magnitude * 0.5f;
                lawn.Add((a, b, c)); lawnSub.Add(sub); lawnZone.Add(zi);
            }
        }
        // All ticked materials count as one lawn for edge detection, so the line where two
        // grass materials meet is NOT treated as an edge.
        var edges = new LawnEdges(lawn, maxEdge);

        var cells = new Dictionary<(int, int, int), List<Matrix4x4>>();
        int total = 0, skippedEdge = 0;
        for (int li = 0; li < lawn.Count; li++)
        {
            var (a, b, c) = lawn[li];
            var st = stats[lawnSub[li]];
            var zone = gs.zones[lawnZone[li]];
            var picks = zonePicks[lawnZone[li]];
            if (picks.Count == 0) continue;
            float totalW = 0f; foreach (var pk in picks) totalW += pk.w;

            Vector3 cr = Vector3.Cross(b - a, c - a);
            float area = cr.magnitude * 0.5f;
            if (area <= 1e-6f) continue;
            Vector3 n = cr.normalized;

            float expected = area * zone.density;
            int count = Mathf.FloorToInt(expected) + (R() < expected - Mathf.Floor(expected) ? 1 : 0);
            for (int k = 0; k < count; k++)
            {
                float r1 = R(), r2 = R();
                if (r1 + r2 > 1f) { r1 = 1f - r1; r2 = 1f - r2; }
                Vector3 p = a + r1 * (b - a) + r2 * (c - a);

                // Keep clear of the lawn's outline, then fade in.
                float d = edges.Distance(p);
                if (d < zone.edgeMargin) { skippedEdge++; st[5]++; continue; }
                if (zone.edgeSoftness > 0f && d < zone.edgeMargin + zone.edgeSoftness
                    && R() > (d - zone.edgeMargin) / zone.edgeSoftness) { skippedEdge++; st[5]++; continue; }

                Vector3 up = Vector3.Lerp(Vector3.up, n, zone.alignToGround).normalized;
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, R() * 360f, 0f);
                float s = Mathf.Lerp(zone.scaleRange.x, zone.scaleRange.y, R());

                float pick = R() * totalW; int pi = 0;
                while (pi < picks.Count - 1 && pick > picks[pi].w) { pick -= picks[pi].w; pi++; }
                int t = picks[pi].type;

                var key = (Mathf.FloorToInt(p.x / gs.chunkSize), Mathf.FloorToInt(p.z / gs.chunkSize), t);
                if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<Matrix4x4>();
                list.Add(Matrix4x4.TRS(p, rot, new Vector3(s, s * zone.height, s)) * locals[t]);
                total++; st[4]++;
            }
        }

        // --- pack into chunks (max 1023 per draw) ---
        if (gs.data == null) gs.data = CreateDataAsset(gs, mesh);
        var data = gs.data;
        data.types = types;
        data.chunks = new List<GrassScatterData.Chunk>();
        float maxScale = 1f;
        foreach (var z in gs.zones) maxScale = Mathf.Max(maxScale, Mathf.Max(z.scaleRange.x, z.scaleRange.y) * Mathf.Max(1f, z.height));
        float pad = typeRadius * maxScale;
        foreach (var kv in cells)
        {
            var all = kv.Value;
            for (int start = 0; start < all.Count; start += 1023)
            {
                int len = Mathf.Min(1023, all.Count - start);
                var arr = all.GetRange(start, len).ToArray();
                var bounds = new Bounds(arr[0].GetColumn(3), Vector3.zero);
                foreach (var m in arr) bounds.Encapsulate(m.GetColumn(3));
                bounds.Expand(pad * 2f);
                data.chunks.Add(new GrassScatterData.Chunk { type = kv.Key.Item3, bounds = bounds, matrices = arr });
            }
        }
        data.totalInstances = total;
        gs.InvalidateLighting();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);
        EditorUtility.SetDirty(gs);
        EditorSceneManager.MarkSceneDirty(gs.gameObject.scene);

        string msg = $"[GrassScatter] {total:N0} clumps in {data.chunks.Count} chunks ({skippedEdge:N0} held back from lawn edges).";
        if (total > 40000) msg += " That's a lot for Quest - consider lowering Density.";
        Debug.Log(msg, gs);

        // One line per ticked material slot, so it's clear WHY a slot got little or no grass.
        foreach (var kv in stats)
        {
            var s = kv.Value;
            string why = "";
            if (s[1] == 0 && s[2] > 0) why = "  <-- no faces point up (check Face Orientation in Blender)";
            else if (s[1] == 0 && s[3] > 0) why = "  <-- all faces steeper than Max Slope";
            else if (s[1] > 0 && s[4] == 0 && s[5] > 0) why = "  <-- whole area within Edge Margin: lower it";
            Debug.Log($"[GrassScatter]  slot {kv.Key} '{mats[kv.Key].name}': {s[0]} tris | up {s[1]} ({subArea[kv.Key]:0.#} m²) | down {s[2]} | steep {s[3]} | placed {s[4]} | held at edge {s[5]}{why}", gs);
        }
    }

    // "How far is this point from where the lawn ends?" - answered by LOOKING, not by mesh topology:
    // from the point, step outwards in 8 directions and check (top-down) whether there is still lawn
    // underneath. Works no matter how the lawn is modelled: dense/small faces, T-junctions, unmerged
    // vertices, separate pieces touching - none of that creates fake edges.
    class LawnEdges
    {
        readonly List<(Vector2 a, Vector2 b, Vector2 c)> tris = new List<(Vector2, Vector2, Vector2)>();
        readonly Dictionary<(int, int), List<int>> grid = new Dictionary<(int, int), List<int>>();
        const float Cell = 1f;
        readonly float maxDist;
        static readonly Vector2[] Dirs;

        static LawnEdges()
        {
            Dirs = new Vector2[8];
            for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; Dirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
        }

        public LawnEdges(List<(Vector3 a, Vector3 b, Vector3 c)> lawn, float maxDist)
        {
            this.maxDist = Mathf.Max(maxDist, 0.01f);
            foreach (var (a, b, c) in lawn)
            {
                var t = (new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z));
                int idx = tris.Count; tris.Add(t);
                int x0 = Mathf.FloorToInt(Mathf.Min(t.Item1.x, Mathf.Min(t.Item2.x, t.Item3.x)) / Cell);
                int x1 = Mathf.FloorToInt(Mathf.Max(t.Item1.x, Mathf.Max(t.Item2.x, t.Item3.x)) / Cell);
                int z0 = Mathf.FloorToInt(Mathf.Min(t.Item1.y, Mathf.Min(t.Item2.y, t.Item3.y)) / Cell);
                int z1 = Mathf.FloorToInt(Mathf.Max(t.Item1.y, Mathf.Max(t.Item2.y, t.Item3.y)) / Cell);
                for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                    {
                        if (!grid.TryGetValue((x, z), out var l)) grid[(x, z)] = l = new List<int>();
                        l.Add(idx);
                    }
            }
        }

        bool OnLawn(Vector2 p)
        {
            if (!grid.TryGetValue((Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.y / Cell)), out var list)) return false;
            foreach (int i in list)
            {
                var (a, b, c) = tris[i];
                // tiny tolerance so points exactly on a shared edge between two faces count as inside
                float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
                const float eps = 1e-5f;
                if ((d1 >= -eps && d2 >= -eps && d3 >= -eps) || (d1 <= eps && d2 <= eps && d3 <= eps)) return true;
            }
            return false;
        }
        static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

        // Approximate distance to the lawn's end (in ~10 cm steps), capped at maxDist = "far enough".
        public float Distance(Vector3 p3)
        {
            var p = new Vector2(p3.x, p3.z);
            const float step = 0.1f;
            for (float r = step; r <= maxDist + 1e-4f; r += step)
                foreach (var d in Dirs)
                    if (!OnLawn(p + d * r)) return r - step;
            return maxDist;
        }
    }

    static GrassScatterData CreateDataAsset(GrassScatter gs, Mesh sourceMesh)
    {
        // Saved next to the source model (e.g. Assets/Models/Scenarios/A_1/A_1_GrassScatter.asset).
        string dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(sourceMesh));
        if (string.IsNullOrEmpty(dir) || !dir.StartsWith("Assets")) dir = "Assets";
        string sceneName = string.IsNullOrEmpty(gs.gameObject.scene.name) ? "Scene" : gs.gameObject.scene.name;
        string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{sceneName}_GrassScatter.asset".Replace('\\', '/'));
        var asset = CreateInstance<GrassScatterData>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
