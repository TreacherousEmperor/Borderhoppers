using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Čistá logika generace (bez MonoBehaviour). Pořadí:
/// Voronoi -> výška -> moře/jezera -> teplota/vlhkost -> biomy -> řeky -> zdroje -> startovní pozice.
/// Stejný seed = stejný svět.
/// </summary>
public static class WorldGenerator
{
    public static World Generate(WorldSettings s)
    {
        var rng = new System.Random(s.seed);
        float W = s.width, H = s.height;
        float sx = W / s.cellsX, sy = H / s.cellsY;

        // 1) body: jitterovaná mřížka, pak Lloydova relaxace v builderu
        var pts = new Vector2[s.cellsX * s.cellsY];
        for (int y = 0; y < s.cellsY; y++)
        for (int x = 0; x < s.cellsX; x++)
            pts[y * s.cellsX + x] = new Vector2(
                (x + 0.15f + 0.7f * (float)rng.NextDouble()) * sx,
                (y + 0.15f + 0.7f * (float)rng.NextDouble()) * sy);

        VoronoiBuilder.Build(pts, W, H, Mathf.Max(sx, sy), s.lloydIterations,
                             out var cells, out var corners);

        var world = new World { settings = s, width = W, height = H, cells = cells, corners = corners };

        var offC = RandOffset(rng); var offR = RandOffset(rng);
        var offT = RandOffset(rng); var offM = RandOffset(rng);

        GenerateHeights(world, offC, offR);
        GenerateWater(world);
        GenerateClimate(world, offT, offM);
        AssignBiomes(world);
        GenerateRivers(world, rng);
        PlaceResources(world, rng);
        PickStartPositions(world, (sx + sy) * 0.5f);
        return world;
    }

    // ---------------------------------------------------------------- výška
    static void GenerateHeights(World w, Vector2 offC, Vector2 offR)
    {
        var s = w.settings;
        foreach (var c in w.cells)
        {
            float u = c.center.x / w.width * (w.width / w.height);   // izotropní souřadnice
            float v = c.center.y / w.height;
            float nx = c.center.x / w.width, ny = v;

            float baseN = Fbm(u * s.continentFrequency, v * s.continentFrequency, offC, 5);

            float ridge = 1f - Mathf.Abs(2f * Mathf.PerlinNoise(
                u * s.ridgeFrequency + offR.x, v * s.ridgeFrequency + offR.y) - 1f);
            ridge *= ridge;

            float e = baseN + ridge * s.mountainStrength * Smooth(0.45f, 0.65f, baseN);

            float edge = Mathf.Min(Mathf.Min(nx, 1 - nx), Mathf.Min(ny, 1 - ny));
            float mask = Smooth(0f, s.oceanMargin, edge);      // okraje mapy = moře
            c.height = e * mask;
        }

        // hladina moře podle požadovaného podílu pevniny
        var sorted = new List<float>(w.cells.Count);
        foreach (var c in w.cells) sorted.Add(c.height);
        sorted.Sort();
        w.seaLevel = sorted[Mathf.Clamp((int)((1f - s.landRatio) * (sorted.Count - 1)), 0, sorted.Count - 1)];
        w.maxHeight = sorted[sorted.Count - 1];
    }

    // ---------------------------------------------------------------- moře, jezera, pobřeží
    static void GenerateWater(World w)
    {
        var s = w.settings;
        foreach (var c in w.cells)
        {
            c.isWater = c.height < w.seaLevel || c.isBorder;
            c.elevation = c.isWater ? 0f : (c.height - w.seaLevel) / Mathf.Max(1e-5f, w.maxHeight - w.seaLevel);
        }

        // oceán = voda spojená s okrajem mapy, zbytek jsou jezera
        var q = new Queue<Cell>();
        foreach (var c in w.cells)
            if (c.isWater && c.isBorder) { c.isOcean = true; q.Enqueue(c); }
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            foreach (var n in c.neighbors)
                if (n.isWater && !n.isOcean) { n.isOcean = true; q.Enqueue(n); }
        }

        foreach (var c in w.cells)
        {
            foreach (var n in c.neighbors)
            {
                if (!c.isWater && n.isWater) c.isCoast = true;
                if (c.isOcean && !n.isWater) c.isShallow = true;
            }
        }

        // reliéf podle percentilů výšek pevniny
        var land = new List<Cell>();
        foreach (var c in w.cells) if (!c.isWater) land.Add(c);
        land.Sort((a, b) => b.elevation.CompareTo(a.elevation));
        int mt = (int)(land.Count * s.mountainShare);
        int hl = (int)(land.Count * (s.mountainShare + s.hillShare));
        for (int i = 0; i < land.Count; i++)
            land[i].relief = i < mt ? Relief.Mountain : (i < hl ? Relief.Hills : Relief.Flat);
    }

    // ---------------------------------------------------------------- teplota a vlhkost
    static void GenerateClimate(World w, Vector2 offT, Vector2 offM)
    {
        // vzdálenost (v krocích grafu) od nejbližší vody
        var dist = new int[w.cells.Count];
        for (int i = 0; i < dist.Length; i++) dist[i] = int.MaxValue;
        var q = new Queue<Cell>();
        foreach (var c in w.cells) if (c.isWater) { dist[c.id] = 0; q.Enqueue(c); }
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            foreach (var n in c.neighbors)
                if (dist[n.id] == int.MaxValue) { dist[n.id] = dist[c.id] + 1; q.Enqueue(n); }
        }

        foreach (var c in w.cells)
        {
            float u = c.center.x / w.height, v = c.center.y / w.height;
            float lat = 1f - Mathf.Abs(c.center.y / w.height * 2f - 1f);   // rovník uprostřed
            float t = lat + (Fbm(u * 2f, v * 2f, offT, 3) - 0.5f) * 0.3f - c.elevation * 0.35f;
            c.temperature = Mathf.Clamp01(t);

            float near = 1f - Mathf.Min(dist[c.id], 8) / 8f;
            float m = Fbm(u * 3f, v * 3f, offM, 4) * 0.6f + near * 0.4f;
            c.moisture = Mathf.Clamp01(m);
        }
    }

    static void AssignBiomes(World w)
    {
        foreach (var c in w.cells)
        {
            if (c.isWater) { c.biome = c.isOcean ? Biome.Ocean : Biome.Lake; continue; }
            float t = c.temperature, m = c.moisture;

            if (t < 0.15f) c.biome = Biome.Snow;
            else if (t < 0.30f) c.biome = Biome.Tundra;
            else if (t > 0.65f)
                c.biome = m < 0.35f ? Biome.Desert : (m < 0.55f ? Biome.Steppe : (m > 0.70f ? Biome.Jungle : Biome.Grassland));
            else
                c.biome = m < 0.30f ? Biome.Steppe : (m < 0.60f ? Biome.Grassland : Biome.Forest);
        }
    }

    // ---------------------------------------------------------------- řeky (po hranách buněk jako v Civ 6)
    static void GenerateRivers(World w, System.Random rng)
    {
        var s = w.settings;

        foreach (var cn in w.corners)
        {
            float sum = 0;
            foreach (var c in cn.touches) { sum += c.height; if (c.isWater) cn.isWaterEdge = true; }
            cn.elevation = sum / cn.touches.Count;
        }

        // priority-flood: každý vrchol dostane "down" ukazující k vodě, takže řeka nikdy neuvízne v prohlubni
        var visited = new bool[w.corners.Count];
        var pq = new SortedSet<(float, int)>();
        foreach (var cn in w.corners)
            if (cn.isWaterEdge) { cn.filled = cn.elevation; visited[cn.id] = true; pq.Add((cn.filled, cn.id)); }

        while (pq.Count > 0)
        {
            var top = pq.Min; pq.Remove(top);
            var cur = w.corners[top.Item2];
            foreach (var adj in cur.adjacent)
            {
                if (visited[adj.id]) continue;
                visited[adj.id] = true;
                adj.filled = Mathf.Max(adj.elevation, cur.filled + 1e-5f);
                adj.down = cur;
                pq.Add((adj.filled, adj.id));
            }
        }

        // prameny: vyšší a vlhčí vrcholy
        float minElev = w.seaLevel + 0.15f * (w.maxHeight - w.seaLevel);
        var sources = new List<Corner>();
        foreach (var cn in w.corners)
        {
            if (cn.isWaterEdge || cn.elevation < minElev) continue;
            float m = 0; foreach (var c in cn.touches) m += c.moisture;
            if (m / cn.touches.Count > 0.35f) sources.Add(cn);
        }
        for (int i = sources.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (sources[i], sources[j]) = (sources[j], sources[i]);
        }

        int landCount = 0; foreach (var c in w.cells) if (!c.isWater) landCount++;
        int target = Mathf.RoundToInt(landCount * s.riverDensity), made = 0;

        foreach (var src in sources)
        {
            if (made >= target) break;
            if (src.river > 0) continue;

            var path = new List<Corner>();
            var cur = src;
            while (cur != null) { path.Add(cur); if (cur.isWaterEdge) break; cur = cur.down; }
            if (path.Count < s.minRiverLength || !path[path.Count - 1].isWaterEdge) continue;

            for (int i = 0; i < path.Count; i++)
            {
                path[i].river++;
                if (i + 1 < path.Count) MarkRiverEdge(path[i], path[i + 1]);
            }
            made++;
        }
    }

    static void MarkRiverEdge(Corner a, Corner b)
    {
        Cell x = null, y = null;
        foreach (var c in a.touches)
            if (b.touches.Contains(c)) { if (x == null) x = c; else y = c; }
        if (x == null || y == null || x.isWater || y.isWater) return;
        x.riverEdges.Add(y.id); y.riverEdges.Add(x.id);
        x.hasRiver = y.hasRiver = true;
    }

    // ---------------------------------------------------------------- zdroje
    static void PlaceResources(World w, System.Random rng)
    {
        var order = new List<Cell>(w.cells);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        foreach (var c in order)
        {
            if (rng.NextDouble() > w.settings.resourceChance) continue;
            bool crowded = false;
            foreach (var n in c.neighbors) if (n.resource != Resource.None) { crowded = true; break; }
            if (crowded) continue;

            if (c.isWater) { if (c.isShallow) c.resource = Resource.Fish; continue; }
            if (c.relief == Relief.Mountain) continue;

            double r = rng.NextDouble();
            if (c.relief == Relief.Hills)
                c.resource = r < 0.5 ? Resource.Iron : (r < 0.85 ? Resource.Stone : Resource.Gold);
            else switch (c.biome)
            {
                case Biome.Grassland:
                case Biome.Steppe: c.resource = r < 0.5 ? Resource.Wheat : Resource.Cattle; break;
                case Biome.Forest:
                case Biome.Tundra: c.resource = Resource.Fur; break;
                case Biome.Jungle: c.resource = Resource.Spices; break;
                case Biome.Desert: c.resource = r < 0.6 ? Resource.Gold : Resource.Stone; break;
            }
        }
    }

    // ---------------------------------------------------------------- startovní pozice
    static float CellValue(Cell c)
    {
        float v;
        if (c.isWater) v = c.isShallow ? 1.0f : 0.3f;
        else if (c.relief == Relief.Mountain) v = 0.2f;
        else
        {
            switch (c.biome)
            {
                case Biome.Grassland: v = 3f; break;
                case Biome.Steppe:    v = 2f; break;
                case Biome.Forest:    v = 1.5f; break;
                case Biome.Jungle:    v = 1.2f; break;
                case Biome.Tundra:    v = 0.6f; break;
                case Biome.Desert:    v = 0.3f; break;
                default:              v = 0.1f; break;
            }
            if (c.relief == Relief.Hills) v += 0.8f;
        }
        if (c.hasRiver) v += 1.5f;
        if (c.resource != Resource.None) v += 1.5f;
        return v;
    }

    static float StartScore(Cell c)
    {
        // součet hodnot v okruhu 2 buněk
        var seen = new HashSet<Cell> { c };
        var frontier = new List<Cell> { c };
        float sum = CellValue(c);
        for (int depth = 0; depth < 2; depth++)
        {
            var next = new List<Cell>();
            foreach (var f in frontier)
                foreach (var n in f.neighbors)
                    if (seen.Add(n)) { sum += CellValue(n); next.Add(n); }
            frontier = next;
        }
        return sum;
    }

    static void PickStartPositions(World w, float avgSpacing)
    {
        var scored = new List<(Cell cell, float score)>();
        foreach (var c in w.cells)
        {
            if (c.isWater || c.relief == Relief.Mountain || c.biome == Biome.Snow) continue;
            scored.Add((c, StartScore(c)));
        }
        scored.Sort((a, b) => b.score.CompareTo(a.score));

        float minDist = w.settings.minStartDistance * avgSpacing;
        for (int attempt = 0; attempt < 8 && w.startPositions.Count < w.settings.players; attempt++)
        {
            w.startPositions.Clear();
            foreach (var (cell, _) in scored)
            {
                bool ok = true;
                foreach (var st in w.startPositions)
                    if (Vector2.Distance(st.center, cell.center) < minDist) { ok = false; break; }
                if (!ok) continue;
                w.startPositions.Add(cell);
                if (w.startPositions.Count >= w.settings.players) break;
            }
            minDist *= 0.8f;   // nevešli se? zkus menší vzdálenost
        }
    }

    // ---------------------------------------------------------------- šum
    static Vector2 RandOffset(System.Random rng) =>
        new Vector2(rng.Next(0, 10000), rng.Next(0, 10000));

    static float Fbm(float x, float y, Vector2 off, int oct)
    {
        float v = 0, a = 1, f = 1, n = 0;
        for (int i = 0; i < oct; i++)
        {
            v += Mathf.PerlinNoise(x * f + off.x, y * f + off.y) * a;
            n += a; a *= 0.5f; f *= 2f;
        }
        return v / n;
    }

    static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }
}
