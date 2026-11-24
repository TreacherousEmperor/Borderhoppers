using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Voronoi diagram bez externích knihoven: každá buňka se ořeže půlicími přímkami
/// k nejbližším bodům (half-plane clipping). Pro tisíce buněk je to rychlé.
/// Přidává Lloydovu relaxaci, sdílené vrcholy (Corner) a sousedství.
/// </summary>
public static class VoronoiBuilder
{
    class Poly
    {
        public List<Vector2> v = new List<Vector2>();
        public List<int> lab = new List<int>();   // lab[i] = soused za hranou v[i] -> v[i+1], -1 = okraj mapy
    }

    public static void Build(Vector2[] pts, float w, float h, float gridSize, int lloyd,
                             out List<Cell> cells, out List<Corner> corners)
    {
        Poly[] polys = null;
        for (int it = 0; it <= lloyd; it++)
        {
            polys = ComputePolys(pts, w, h, gridSize);
            if (it < lloyd)
                for (int i = 0; i < pts.Length; i++) pts[i] = Centroid(polys[i], pts[i]);
        }

        cells = new List<Cell>(pts.Length);
        corners = new List<Corner>();
        var cornerMap = new Dictionary<Vector3Int, Corner>();

        for (int i = 0; i < pts.Length; i++)
        {
            var p = polys[i];
            int n = p.v.Count;
            var cell = new Cell { id = i, center = pts[i], polygon = p.v.ToArray(), corners = new Corner[n] };

            for (int k = 0; k < n; k++)
            {
                int prev = p.lab[(k - 1 + n) % n], next = p.lab[k];
                if (prev == -1 || next == -1) cell.isBorder = true;

                // vrchol je jednoznačně určen trojicí buněk, které se v něm stýkají
                var key = SortedKey(i, prev, next);
                if (!cornerMap.TryGetValue(key, out var corner))
                {
                    corner = new Corner { id = corners.Count, pos = p.v[k] };
                    corners.Add(corner);
                    cornerMap[key] = corner;
                }
                if (!corner.touches.Contains(cell)) corner.touches.Add(cell);
                cell.corners[k] = corner;
            }

            for (int k = 0; k < n; k++)
            {
                var a = cell.corners[k]; var b = cell.corners[(k + 1) % n];
                if (a != b)
                {
                    if (!a.adjacent.Contains(b)) a.adjacent.Add(b);
                    if (!b.adjacent.Contains(a)) b.adjacent.Add(a);
                }
            }
            cells.Add(cell);
        }

        // sousedé (symetricky)
        for (int i = 0; i < pts.Length; i++)
            foreach (int l in polys[i].lab)
            {
                if (l < 0) continue;
                var a = cells[i]; var b = cells[l];
                if (!a.neighbors.Contains(b)) a.neighbors.Add(b);
                if (!b.neighbors.Contains(a)) b.neighbors.Add(a);
            }
    }

    static Poly[] ComputePolys(Vector2[] pts, float w, float h, float gs)
    {
        int cols = Mathf.Max(1, Mathf.CeilToInt(w / gs));
        int rows = Mathf.Max(1, Mathf.CeilToInt(h / gs));
        var grid = new List<int>[cols * rows];
        for (int i = 0; i < grid.Length; i++) grid[i] = new List<int>();
        for (int i = 0; i < pts.Length; i++)
        {
            int cx = Mathf.Clamp((int)(pts[i].x / gs), 0, cols - 1);
            int cy = Mathf.Clamp((int)(pts[i].y / gs), 0, rows - 1);
            grid[cy * cols + cx].Add(i);
        }

        var polys = new Poly[pts.Length];
        var cand = new List<KeyValuePair<float, int>>();

        for (int i = 0; i < pts.Length; i++)
        {
            Vector2 c = pts[i];
            int gx = Mathf.Clamp((int)(c.x / gs), 0, cols - 1);
            int gy = Mathf.Clamp((int)(c.y / gs), 0, rows - 1);

            cand.Clear();
            for (int dy = -3; dy <= 3; dy++)
            for (int dx = -3; dx <= 3; dx++)
            {
                int x = gx + dx, y = gy + dy;
                if (x < 0 || y < 0 || x >= cols || y >= rows) continue;
                foreach (int j in grid[y * cols + x])
                    if (j != i) cand.Add(new KeyValuePair<float, int>((pts[j] - c).sqrMagnitude, j));
            }
            cand.Sort((a, b) => a.Key.CompareTo(b.Key));

            var p = new Poly();
            p.v.Add(new Vector2(0, 0)); p.v.Add(new Vector2(w, 0));
            p.v.Add(new Vector2(w, h)); p.v.Add(new Vector2(0, h));
            for (int k = 0; k < 4; k++) p.lab.Add(-1);

            float maxR2 = MaxDist2(p, c);
            foreach (var kv in cand)
            {
                if (kv.Key * 0.25f > maxR2) break;   // dál už nemůže nic oříznout
                Clip(p, c, pts[kv.Value], kv.Value);
                maxR2 = MaxDist2(p, c);
            }
            polys[i] = p;
        }
        return polys;
    }

    // ponechá část polygonu bližší k c než k o; nová hrana dostane štítek oid
    static void Clip(Poly p, Vector2 c, Vector2 o, int oid)
    {
        Vector2 mid = (c + o) * 0.5f, nrm = o - c;
        int n = p.v.Count;
        var nv = new List<Vector2>(n + 2);
        var nl = new List<int>(n + 2);

        for (int i = 0; i < n; i++)
        {
            Vector2 a = p.v[i], b = p.v[(i + 1) % n];
            int l = p.lab[i];
            float fa = Vector2.Dot(a - mid, nrm), fb = Vector2.Dot(b - mid, nrm);
            bool ain = fa <= 0, bin = fb <= 0;

            if (ain)
            {
                nv.Add(a); nl.Add(l);
                if (!bin) { nv.Add(Vector2.Lerp(a, b, fa / (fa - fb))); nl.Add(oid); }
            }
            else if (bin)
            {
                nv.Add(Vector2.Lerp(a, b, fa / (fa - fb))); nl.Add(l);
            }
        }
        p.v = nv; p.lab = nl;
    }

    static float MaxDist2(Poly p, Vector2 c)
    {
        float m = 0;
        foreach (var v in p.v) m = Mathf.Max(m, (v - c).sqrMagnitude);
        return m;
    }

    static Vector2 Centroid(Poly p, Vector2 fallback)
    {
        float a = 0, cx = 0, cy = 0;
        int n = p.v.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 p0 = p.v[i], p1 = p.v[(i + 1) % n];
            float cr = p0.x * p1.y - p1.x * p0.y;
            a += cr; cx += (p0.x + p1.x) * cr; cy += (p0.y + p1.y) * cr;
        }
        if (Mathf.Abs(a) < 1e-6f) return fallback;
        return new Vector2(cx / (3f * a), cy / (3f * a));
    }

    static Vector3Int SortedKey(int a, int b, int c)
    {
        int t;
        if (a > b) { t = a; a = b; b = t; }
        if (b > c) { t = b; b = c; c = t; }
        if (a > b) { t = a; a = b; b = t; }
        return new Vector3Int(a, b, c);
    }
}
