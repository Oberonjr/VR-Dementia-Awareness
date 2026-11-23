using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// "Fix Light Probes" button on GrassScatter.
//  1. Removes probes buried inside solid geometry (hedges, walls, house shells). A buried probe
//     bakes black and darkens everything near it. Test: shoot rays out of the probe; if many of
//     them hit the BACK of a surface, the probe is inside something.
//  2. Adds a layer of probes at grass height over the ticked lawn materials, so grass gets the
//     light that's actually down at ground level.
// Only opaque objects count as "solid" - leaves/cutout foliage are ignored, except cutout
// materials listed in GrassScatter's "Solid Cutout Materials" (e.g. the A_1 'Bush' hedges).
// Undo with Ctrl+Z. Re-bake lighting afterwards.
public static class ProbeFixer
{
    const float RayLen = 3f;
    const float BuriedRatio = 0.25f;   // same idea as Unity's own probe validity check

    public static void Fix(GrassScatter gs)
    {
        // --- the probe group ---
        var group = gs.probeGroup ? gs.probeGroup : Object.FindFirstObjectByType<LightProbeGroup>();
        if (group == null)
        {
            var go = new GameObject("LightProbes_Grass");
            Undo.RegisterCreatedObjectUndo(go, "Create light probes");
            group = go.AddComponent<LightProbeGroup>();
            group.probePositions = new Vector3[0];
        }
        if (gs.probeGroup != group) { Undo.RecordObject(gs, "Assign probe group"); gs.probeGroup = group; }

        // --- 1. solid geometry in the scene ---
        var solid = new TriGrid(2f);
        foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!mr.enabled || !mr.gameObject.activeInHierarchy) continue;
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var mesh = mf.sharedMesh;
            var verts = mesh.vertices;
            if (verts == null || verts.Length == 0) continue;
            var l2w = mr.transform.localToWorldMatrix;
            bool mirrored = l2w.determinant < 0f;
            var w = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++) w[i] = l2w.MultiplyPoint3x4(verts[i]);
            var mats = mr.sharedMaterials;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var m = sub < mats.Length ? mats[sub] : null;
                // skip cutout/transparent (leaves, glass) unless marked as a solid block (hedges)
                if (m == null || (m.renderQueue >= 2450 && !gs.solidCutoutMaterials.Contains(m))) continue;
                var tris = mesh.GetTriangles(sub);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    var a = w[tris[i]]; var b = w[tris[i + 1]]; var c = w[tris[i + 2]];
                    if (mirrored) (b, c) = (c, b);
                    solid.Add(a, b, c);
                }
            }
        }

        // --- 2. keep only probes that aren't buried ---
        var kept = new List<Vector3>();
        int buried = 0;
        foreach (var lp in group.probePositions)
        {
            var wp = group.transform.TransformPoint(lp);
            if (solid.IsBuried(wp, RayLen, BuriedRatio)) buried++;
            else kept.Add(wp);
        }

        // --- 3. lawn layer ---
        int added = 0;
        var lawn = CollectLawn(gs);
        if (lawn.Count > 0)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue); var max = -min;
            foreach (var (a, b, c) in lawn)
                foreach (var v in new[] { a, b, c })
                { min = Vector2.Min(min, new Vector2(v.x, v.z)); max = Vector2.Max(max, new Vector2(v.x, v.z)); }

            float sp = gs.probeSpacing, near = sp * 0.45f;
            for (float x = Mathf.Floor(min.x / sp) * sp + sp * 0.5f; x <= max.x; x += sp)
                for (float z = Mathf.Floor(min.y / sp) * sp + sp * 0.5f; z <= max.y; z += sp)
                {
                    if (!TopOfLawn(lawn, x, z, out float y)) continue;
                    var p = new Vector3(x, y + gs.probeHeight, z);
                    if (solid.IsBuried(p, RayLen, BuriedRatio)) continue;
                    bool tooClose = false;
                    foreach (var k in kept) if ((k - p).sqrMagnitude < near * near) { tooClose = true; break; }
                    if (tooClose) continue;   // also makes pressing the button twice harmless
                    kept.Add(p); added++;
                }
        }

        if (kept.Count < 4) { Debug.LogError("[ProbeFixer] Fewer than 4 probes would remain - nothing changed."); return; }

        Undo.RecordObject(group, "Fix light probes");
        var local = new Vector3[kept.Count];
        for (int i = 0; i < kept.Count; i++) local[i] = group.transform.InverseTransformPoint(kept[i]);
        group.probePositions = local;
        EditorUtility.SetDirty(group);

        Debug.Log($"[ProbeFixer] Removed {buried} buried probes, added {added} lawn probes ({kept.Count} total) on '{group.name}'. " +
                  "Now re-bake: Window > Rendering > Lighting > Generate Lighting.", group);
    }

    // Up-facing lawn triangles (world space) for the ticked grass materials.
    static List<(Vector3, Vector3, Vector3)> CollectLawn(GrassScatter gs)
    {
        var list = new List<(Vector3, Vector3, Vector3)>();
        var mf = gs.source ? gs.source.GetComponent<MeshFilter>() : null;
        if (mf == null || mf.sharedMesh == null) return list;
        var mesh = mf.sharedMesh; var verts = mesh.vertices; var mats = gs.source.sharedMaterials;
        var l2w = gs.source.transform.localToWorldMatrix; bool mirrored = l2w.determinant < 0f;
        float minUp = Mathf.Cos(gs.maxSlope * Mathf.Deg2Rad);
        for (int sub = 0; sub < mesh.subMeshCount && sub < mats.Length; sub++)
        {
            if (gs.zones.FindIndex(z => z.material == mats[sub]) < 0) continue;
            var tris = mesh.GetTriangles(sub);
            for (int i = 0; i < tris.Length; i += 3)
            {
                var a = l2w.MultiplyPoint3x4(verts[tris[i]]);
                var b = l2w.MultiplyPoint3x4(verts[tris[i + 1]]);
                var c = l2w.MultiplyPoint3x4(verts[tris[i + 2]]);
                if (mirrored) (b, c) = (c, b);
                var n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f || n.normalized.y < minUp) continue;
                list.Add((a, b, c));
            }
        }
        return list;
    }

    // Height of the highest lawn triangle under (x, z), if any.
    static bool TopOfLawn(List<(Vector3 a, Vector3 b, Vector3 c)> lawn, float x, float z, out float y)
    {
        y = float.MinValue; bool found = false;
        var p = new Vector2(x, z);
        foreach (var (a, b, c) in lawn)
        {
            Vector2 A = new Vector2(a.x, a.z), B = new Vector2(b.x, b.z), C = new Vector2(c.x, c.z);
            float d1 = Cr(B - A, p - A), d2 = Cr(C - B, p - B), d3 = Cr(A - C, p - C);
            if (!((d1 >= 0 && d2 >= 0 && d3 >= 0) || (d1 <= 0 && d2 <= 0 && d3 <= 0))) continue;
            var n = Vector3.Cross(b - a, c - a);
            float h = a.y - (n.x * (x - a.x) + n.z * (z - a.z)) / n.y;
            if (h > y) { y = h; found = true; }
        }
        return found;
    }
    static float Cr(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

    // World-space triangles in a 3D grid, for fast "what's near this point" ray tests.
    class TriGrid
    {
        readonly List<Vector3> A = new List<Vector3>(), B = new List<Vector3>(), C = new List<Vector3>(), N = new List<Vector3>();
        readonly Dictionary<(int, int, int), List<int>> cells = new Dictionary<(int, int, int), List<int>>();
        readonly float size;
        int[] stamp = new int[0]; int stampId;
        static readonly Vector3[] Dirs = MakeDirs();

        public TriGrid(float cellSize) { size = cellSize; }

        static Vector3[] MakeDirs()
        {
            var d = new List<Vector3> { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                d.Add(new Vector3(x, y, z).normalized);
            return d.ToArray();   // 14 directions
        }

        (int, int, int) Key(Vector3 p) => (Mathf.FloorToInt(p.x / size), Mathf.FloorToInt(p.y / size), Mathf.FloorToInt(p.z / size));

        public void Add(Vector3 a, Vector3 b, Vector3 c)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return;
            int idx = A.Count; A.Add(a); B.Add(b); C.Add(c); N.Add(n.normalized);
            var lo = Key(Vector3.Min(a, Vector3.Min(b, c))); var hi = Key(Vector3.Max(a, Vector3.Max(b, c)));
            for (int x = lo.Item1; x <= hi.Item1; x++)
                for (int y = lo.Item2; y <= hi.Item2; y++)
                    for (int z = lo.Item3; z <= hi.Item3; z++)
                    {
                        if (!cells.TryGetValue((x, y, z), out var l)) cells[(x, y, z)] = l = new List<int>();
                        l.Add(idx);
                    }
        }

        // Inside something solid if enough rays first hit the BACK of a surface.
        public bool IsBuried(Vector3 p, float len, float ratio)
        {
            if (stamp.Length < A.Count) stamp = new int[A.Count];
            stampId++;
            var cand = new List<int>();
            var lo = Key(p - Vector3.one * len); var hi = Key(p + Vector3.one * len);
            for (int x = lo.Item1; x <= hi.Item1; x++)
                for (int y = lo.Item2; y <= hi.Item2; y++)
                    for (int z = lo.Item3; z <= hi.Item3; z++)
                        if (cells.TryGetValue((x, y, z), out var l))
                            foreach (int i in l) if (stamp[i] != stampId) { stamp[i] = stampId; cand.Add(i); }
            if (cand.Count == 0) return false;

            int back = 0;
            foreach (var d in Dirs)
            {
                float best = len; int hit = -1;
                foreach (int i in cand)
                    if (Ray(p, d, A[i], B[i], C[i], out float t) && t < best) { best = t; hit = i; }
                if (hit >= 0 && Vector3.Dot(N[hit], d) > 0f) back++;
            }
            return back >= Mathf.CeilToInt(Dirs.Length * ratio);
        }

        // Möller–Trumbore, two-sided.
        static bool Ray(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float t)
        {
            t = 0f;
            Vector3 e1 = b - a, e2 = c - a, pv = Vector3.Cross(d, e2);
            float det = Vector3.Dot(e1, pv);
            if (Mathf.Abs(det) < 1e-9f) return false;
            float inv = 1f / det;
            Vector3 tv = o - a;
            float u = Vector3.Dot(tv, pv) * inv; if (u < 0f || u > 1f) return false;
            Vector3 qv = Vector3.Cross(tv, e1);
            float v = Vector3.Dot(d, qv) * inv; if (v < 0f || u + v > 1f) return false;
            t = Vector3.Dot(e2, qv) * inv;
            return t > 1e-4f;
        }
    }
}
