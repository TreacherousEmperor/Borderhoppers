using System.Collections.Generic;
using UnityEngine;

public enum MoveType { Land, Air }   // později třeba Water pro lodě

/// <summary>Čistá logika jednotky (žádné Unity objekty kromě odkazu na vizuál).</summary>
public class Unit
{
    public string name;
    public int ownerId;
    public MoveType moveType;
    public float maxMovePoints;
    public float movePoints;
    public Cell cell;
    public List<Cell> path = new List<Cell>();   // zbývající cesta (může trvat víc kol)

    // vizuál
    public Transform view;
    public Renderer rend;
    public Color color;
    public GameObject outline;    // zapíná se při výběru
    public bool moving;           // běží animace pohybu

    public Unit(string name, int ownerId, MoveType type, float movePoints, Cell start)
    {
        this.name = name; this.ownerId = ownerId; moveType = type;
        maxMovePoints = this.movePoints = movePoints; cell = start;
    }

    // --- TADY jsou pravidla, kam jednotka smí a kolik to stojí ---
    public bool CanEnter(Cell c) => moveType == MoveType.Air || c.Passable;   // vzducholoď kamkoliv, voják jen po souši mimo hory
    public float CostToEnter(Cell c) => moveType == MoveType.Air ? 1f : c.MoveCost;

    public void StartTurn() => movePoints = maxMovePoints;

    /// <summary>Jde po cestě, dokud zbývají body pohybu. Vrátí buňky, přes které prošla (pro animaci).</summary>
    public List<Cell> AdvanceAlongPath()
    {
        var moved = new List<Cell>();
        while (path.Count > 0 && movePoints > 0f)
        {
            var next = path[0];
            if (!CanEnter(next)) { path.Clear(); break; }
            // civ pravidlo: s jakýmkoli zbývajícím pohybem se dá vstoupit, zbytek se spotřebuje
            movePoints = Mathf.Max(0f, movePoints - CostToEnter(next));
            cell = next;
            path.RemoveAt(0);
            moved.Add(next);
        }
        return moved;
    }
}
