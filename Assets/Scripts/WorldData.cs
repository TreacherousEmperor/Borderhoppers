using System;
using System.Collections.Generic;
using UnityEngine;

public enum Biome { Ocean, Lake, Snow, Tundra, Desert, Steppe, Grassland, Forest, Jungle }
public enum Relief { Flat, Hills, Mountain }
public enum Resource { None, Wheat, Cattle, Fish, Fur, Spices, Iron, Stone, Gold }

/// <summary>Vrchol Voronoi buněk (sdílený mezi 2-3 buňkami). Po jeho hranách tečou řeky.</summary>
public class Corner
{
    public int id;
    public Vector2 pos;
    public float elevation;          // průměr výšek okolních buněk
    public float filled;             // výška po vyplnění depresí (pro odtok vody)
    public bool isWaterEdge;         // dotýká se vody (moře nebo jezera)
    public int river;                // objem řeky, 0 = žádná
    public Corner down;              // kam voda odtéká
    public List<Cell> touches = new List<Cell>();
    public List<Corner> adjacent = new List<Corner>();
}

/// <summary>Jedna buňka mapy = "dlaždice" hry. Sem patří i města a budovy.</summary>
public class Cell
{
    public int id;
    public Vector2 center;
    public Vector2[] polygon;        // vrcholy polygonu (po obvodu)
    public Corner[] corners;         // stejné pořadí jako polygon
    public List<Cell> neighbors = new List<Cell>();

    // terén
    public float height;             // surová výška (0..1)
    public float elevation;          // 0 pro vodu, 0..1 nad hladinou pro pevninu
    public float temperature;        // 0..1
    public float moisture;           // 0..1
    public Biome biome;
    public Relief relief;
    public bool isWater, isOcean, isCoast, isShallow, isBorder, hasRiver;
    public HashSet<int> riverEdges = new HashSet<int>();   // id sousedů, se kterými sdílí řeku na hranici
    public Resource resource;

    // hra (sem navážeš svoje systémy)
    public int ownerId = -1;
    public int cityId = -1;
    public List<string> buildings = new List<string>();

    public bool Passable => !isWater && relief != Relief.Mountain;

    public float MoveCost
    {
        get
        {
            if (!Passable) return float.PositiveInfinity;
            float c = 1f;
            if (relief == Relief.Hills) c += 1f;
            if (biome == Biome.Forest || biome == Biome.Jungle) c += 1f;
            return c;
        }
    }
}

public class World
{
    public WorldSettings settings;
    public float width, height;
    public float seaLevel, maxHeight;
    public List<Cell> cells;         // index == Cell.id
    public List<Corner> corners;     // index == Corner.id
    public List<Cell> startPositions = new List<Cell>();

    /// <summary>Buňka obsahující bod (u Voronoi je to prostě nejbližší střed).</summary>
    public Cell GetCellAt(Vector2 p)
    {
        Cell best = null; float bd = float.MaxValue;
        foreach (var c in cells)
        {
            float d = (c.center - p).sqrMagnitude;
            if (d < bd) { bd = d; best = c; }
        }
        return best;
    }
}

[Serializable]
public class WorldSettings
{
    public int seed = 1;

    [Header("Velikost")]
    public float width = 200f;
    public float height = 120f;
    public int cellsX = 100;
    public int cellsY = 60;
    [Range(0, 5)] public int lloydIterations = 2;

    [Header("Tvar pevniny")]
    [Range(0.15f, 0.6f)] public float landRatio = 0.35f;
    public float continentFrequency = 2.2f;
    public float ridgeFrequency = 5f;
    [Range(0f, 1f)] public float mountainStrength = 0.45f;
    [Range(0.02f, 0.4f)] public float oceanMargin = 0.12f;
    [Range(0f, 0.5f)] public float mountainShare = 0.08f;   // podíl pevniny
    [Range(0f, 0.6f)] public float hillShare = 0.20f;

    [Header("Řeky")]
    public float riverDensity = 0.05f;        // zdrojů na buňku pevniny
    public int minRiverLength = 4;

    [Header("Zdroje a starty")]
    [Range(0f, 0.4f)] public float resourceChance = 0.12f;
    public int players = 6;
    public float minStartDistance = 12f;      // v "průměrných buňkách"

    [Header("Vizuál")]
    public float heightScale = 6f;
}
