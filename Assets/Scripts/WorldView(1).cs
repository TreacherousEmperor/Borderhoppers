using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dej na prázdný GameObject. Vygeneruje svět a vykreslí ho jako low-poly mesh
/// (každá buňka = vějíř trojúhelníků, osvětlení je "zapečené" do barev vrcholů,
/// takže stačí jakýkoli shader s vertex colors; výchozí je Sprites/Default).
/// </summary>
public class WorldView : MonoBehaviour
{
    public WorldSettings settings = new WorldSettings();
    public Material material;                       // volitelné: vlastní materiál s vertex colors
    public Vector3 lightDir = new Vector3(-0.5f, 1f, -0.4f);
    public bool drawGizmos = true;

    public World World { get; private set; }

    GameObject meshObject;
    float[] cornerY;

    void Start() => Regenerate();

    [ContextMenu("Regenerate")]
    public void Regenerate()
    {
        World = WorldGenerator.Generate(settings);
        BuildMesh(World);
    }

    // ---------------------------------------------------------------- mesh
    float CellY(Cell c) => c.isWater ? 0f : 0.05f + c.elevation * settings.heightScale;

    void BuildMesh(World w)
    {
        // výška vrcholů = průměr okolních buněk -> hladké pobřeží a svahy
        cornerY = new float[w.corners.Count];
        foreach (var cn in w.corners)
        {
            float sum = 0;
            foreach (var c in cn.touches) sum += CellY(c);
            cornerY[cn.id] = sum / cn.touches.Count;
        }

        var verts = new List<Vector3>();
        var cols = new List<Color>();
        var tris = new List<int>();
        Vector3 light = lightDir.normalized;

        foreach (var cell in w.cells)
        {
            Vector3 ctr = new Vector3(cell.center.x, CellY(cell), cell.center.y);
            Color col = CellColor(cell);
            int n = cell.corners.Length;
            for (int k = 0; k < n; k++)
            {
                var a = cell.corners[k]; var b = cell.corners[(k + 1) % n];
                AddTri(verts, cols, tris, ctr,
                       new Vector3(a.pos.x, cornerY[a.id], a.pos.y),
                       new Vector3(b.pos.x, cornerY[b.id], b.pos.y), col, light);
            }
        }

        // řeky jako úzké pásy po hranách (šířka podle objemu)
        Color riverCol = new Color(0.20f, 0.45f, 0.85f);
        foreach (var cn in w.corners)
        {
            if (cn.river <= 0 || cn.down == null) continue;
            Vector3 a = new Vector3(cn.pos.x, cornerY[cn.id] + 0.04f, cn.pos.y);
            Vector3 b = new Vector3(cn.down.pos.x, cornerY[cn.down.id] + 0.04f, cn.down.pos.y);
            Vector3 dir = (b - a); dir.y = 0;
            if (dir.sqrMagnitude < 1e-6f) continue;
            Vector3 side = Vector3.Cross(dir.normalized, Vector3.up) * (0.08f + 0.06f * Mathf.Sqrt(cn.river));
            AddTri(verts, cols, tris, a - side, a + side, b + side, riverCol, light);
            AddTri(verts, cols, tris, a - side, b + side, b - side, riverCol, light);
        }

        var mesh = new Mesh { name = "World" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

        if (meshObject != null) Destroy(meshObject);
        meshObject = new GameObject("WorldMesh");
        meshObject.transform.SetParent(transform, false);
        meshObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        meshCollider = meshObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
        var mr = meshObject.AddComponent<MeshRenderer>();
        if (material != null) mr.sharedMaterial = material;
        else
        {
            var sh = Shader.Find("World/VertexColorUnlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");   // záloha (průhledný, bledší)
            mr.sharedMaterial = new Material(sh);
        }
    }

    static void AddTri(List<Vector3> v, List<Color> c, List<int> t,
                       Vector3 p0, Vector3 p1, Vector3 p2, Color col, Vector3 light)
    {
        Vector3 nrm = Vector3.Cross(p1 - p0, p2 - p0);
        if (nrm.y < 0) { (p1, p2) = (p2, p1); nrm = -nrm; }   // přední strana musí mířit nahoru, jinak ji raycast ignoruje
        nrm.Normalize();
        float shade = 0.55f + 0.45f * Mathf.Max(0f, Vector3.Dot(nrm, light));
        Color lit = new Color(col.r * shade, col.g * shade, col.b * shade, 1f);

        int i = v.Count;
        v.Add(p0); v.Add(p1); v.Add(p2);
        c.Add(lit); c.Add(lit); c.Add(lit);
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
    }

    Color CellColor(Cell c)
    {
        Color col;
        switch (c.biome)
        {
            case Biome.Ocean:     col = c.isShallow ? new Color(0.25f, 0.55f, 0.80f) : new Color(0.12f, 0.30f, 0.60f); break;
            case Biome.Lake:      col = new Color(0.25f, 0.55f, 0.85f); break;
            case Biome.Snow:      col = new Color(0.95f, 0.97f, 1.00f); break;
            case Biome.Tundra:    col = new Color(0.62f, 0.68f, 0.58f); break;
            case Biome.Desert:    col = new Color(0.90f, 0.80f, 0.50f); break;
            case Biome.Steppe:    col = new Color(0.72f, 0.74f, 0.40f); break;
            case Biome.Grassland: col = new Color(0.40f, 0.70f, 0.30f); break;
            case Biome.Forest:    col = new Color(0.17f, 0.45f, 0.22f); break;
            case Biome.Jungle:    col = new Color(0.08f, 0.55f, 0.28f); break;
            default:              col = Color.magenta; break;
        }
        if (c.relief == Relief.Hills)    col = Color.Lerp(col, new Color(0.55f, 0.42f, 0.28f), 0.35f);
        if (c.relief == Relief.Mountain) col = Color.Lerp(col, new Color(0.50f, 0.50f, 0.52f), 0.85f);

        // lehká variace, ať to nevypadá jako jedna plocha
        float j = (((uint)c.id * 2654435761u) >> 24) / 255f;
        float k = 0.94f + 0.12f * j;
        return new Color(col.r * k, col.g * k, col.b * k, 1f);
    }

    // ---------------------------------------------------------------- ladění
    // ---------------------------------------------------------------- pozice a picking
    MeshCollider meshCollider;

    /// <summary>Pozice středu buňky ve světě (s povrchem terénu), volitelně zvednutá nahoru.</summary>
    public Vector3 CellPos(Cell c, float lift = 0f) =>
        transform.TransformPoint(new Vector3(c.center.x, CellY(c) + lift, c.center.y));

    /// <summary>Buňka pod paprskem (např. z kurzoru myši), nebo null.</summary>
    public Cell RaycastCell(Ray ray)
    {
        if (World == null || meshCollider == null) return null;
        if (!meshCollider.Raycast(ray, out var hit, 10000f)) return null;
        Vector3 local = meshObject.transform.InverseTransformPoint(hit.point);
        return World.GetCellAt(new Vector2(local.x, local.z));
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos || World == null) return;

        Gizmos.color = Color.red;
        foreach (var c in World.startPositions)
            Gizmos.DrawSphere(new Vector3(c.center.x, CellY(c) + 0.6f, c.center.y), 0.6f);

        foreach (var c in World.cells)
        {
            if (c.resource == Resource.None) continue;
            Gizmos.color = ResourceColor(c.resource);
            Gizmos.DrawCube(new Vector3(c.center.x, CellY(c) + 0.3f, c.center.y), Vector3.one * 0.35f);
        }
    }

    static Color ResourceColor(Resource r)
    {
        switch (r)
        {
            case Resource.Wheat:  return Color.yellow;
            case Resource.Cattle: return new Color(0.6f, 0.3f, 0.1f);
            case Resource.Fish:   return Color.cyan;
            case Resource.Fur:    return new Color(0.8f, 0.6f, 0.4f);
            case Resource.Spices: return new Color(1f, 0.4f, 0f);
            case Resource.Iron:   return Color.gray;
            case Resource.Stone:  return Color.white;
            case Resource.Gold:   return new Color(1f, 0.84f, 0f);
            default:              return Color.black;
        }
    }
}
