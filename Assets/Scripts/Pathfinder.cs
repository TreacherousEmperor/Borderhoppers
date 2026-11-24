using System.Collections.Generic;

/// <summary>
/// Nejkratší cesta po grafu buněk (Dijkstra s předčasným ukončením).
/// Respektuje pravidla jednotky (CanEnter / CostToEnter), takže stejný kód
/// obslouží vojáka i vzducholoď.
/// </summary>
public static class Pathfinder
{
    /// <summary>Cesta bez startovní buňky, nebo null když cíl není dosažitelný.</summary>
    public static List<Cell> FindPath(World w, Unit u, Cell goal)
    {
        Cell start = u.cell;
        if (goal == null || goal == start || !u.CanEnter(goal)) return null;

        int n = w.cells.Count;
        var dist = new float[n];
        var prev = new Cell[n];
        for (int i = 0; i < n; i++) dist[i] = float.PositiveInfinity;

        var pq = new SortedSet<(float, int)>();
        dist[start.id] = 0f;
        pq.Add((0f, start.id));

        while (pq.Count > 0)
        {
            var top = pq.Min; pq.Remove(top);
            if (top.Item1 > dist[top.Item2]) continue;       // zastaralý záznam
            var c = w.cells[top.Item2];
            if (c == goal) break;

            foreach (var nb in c.neighbors)
            {
                if (!u.CanEnter(nb)) continue;
                float nd = dist[c.id] + u.CostToEnter(nb);
                if (nd < dist[nb.id])
                {
                    dist[nb.id] = nd;
                    prev[nb.id] = c;
                    pq.Add((nd, nb.id));
                }
            }
        }

        if (prev[goal.id] == null) return null;

        var path = new List<Cell>();
        for (var c = goal; c != null && c != start; c = prev[c.id]) path.Add(c);
        path.Reverse();
        return path;
    }
}
