using System.Collections.Generic;
using UnityEngine;

// The baked result of a GrassScatter "Rebuild": every grass clump's position/rotation/scale,
// grouped into chunks so far-away chunks can be skipped. Lives in its own asset file so the
// scene file stays small and merge-friendly for the team.
public class GrassScatterData : ScriptableObject
{
    [System.Serializable]
    public class GrassType
    {
        public Mesh mesh;
        public Material[] materials;   // one per submesh
    }

    [System.Serializable]
    public class Chunk
    {
        public int type;               // index into types
        public Bounds bounds;          // world-space, used for culling + draw distance
        public Matrix4x4[] matrices;   // max 1023 per chunk
    }

    public List<GrassType> types = new List<GrassType>();
    public List<Chunk> chunks = new List<Chunk>();
    public int totalInstances;
}
