using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Spawn jednotek, výběr myší, příkazy a kola.
/// LMB = vybrat jednotku (klik přímo na ni, nebo na buňku, kde stojí)
/// RMB = poslat vybranou jednotku na buňku
/// Mezerník = další kolo (obnoví pohyb a dojde zbytek cesty)
/// Používá nový Input System. Vybraná jednotka má outline (shader World/Outline).
/// </summary>
public class UnitController : MonoBehaviour
{
    public WorldView worldView;
    public float moveSpeed = 10f;          // jednotek světa za sekundu
    public Material outlineMaterial;       // volitelné; jinak se vyrobí z shaderu World/Outline

    World world;
    readonly List<Unit> units = new List<Unit>();
    Unit selected;
    LineRenderer line;
    string message = "";

    void Update()
    {
        if (world == null)
        {
            if (worldView == null || worldView.World == null) return;
            world = worldView.World;
            SpawnUnits();
        }

        var mouse = Mouse.current;
        var keyboard = Keyboard.current;

        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame) ClickSelect();
            if (mouse.rightButton.wasPressedThisFrame && selected != null)
            {
                var c = PickCell();
                if (c != null) OrderMove(selected, c);
                else Say("Klik netrefil mapu (mimo mapu nebo chybí collider).");
            }
        }
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) NextTurn();
    }

    // ------------------------------------------------------------ spawn
    void SpawnUnits()
    {
        Cell start = world.startPositions.Count > 0 ? world.startPositions[0] : world.cells.Find(c => c.Passable);

        // voják: 2 body pohybu, jen souš
        AddUnit(new Unit("Voják", 0, MoveType.Land, 2f, start), PrimitiveType.Capsule,
                new Vector3(0.5f, 0.6f, 0.5f), new Color(0.8f, 0.2f, 0.2f));

        // vzducholoď: 4 body pohybu, létá všude
        AddUnit(new Unit("Vzducholoď", 0, MoveType.Air, 4f, start), PrimitiveType.Sphere,
                new Vector3(1.6f, 0.7f, 0.7f), new Color(0.95f, 0.95f, 0.95f));

        var go = new GameObject("PathLine");
        line = go.AddComponent<LineRenderer>();
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = line.endColor = Color.yellow;
        line.startWidth = line.endWidth = 0.15f;
        line.positionCount = 0;
    }

    void AddUnit(Unit u, PrimitiveType shape, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(shape);   // collider zůstává, aby šlo na jednotku kliknout
        go.name = u.name;
        go.transform.localScale = scale;
        go.AddComponent<UnitRef>().unit = u;

        u.view = go.transform;
        u.rend = go.GetComponent<Renderer>();
        u.color = color;
        u.rend.material.color = color;

        // outline: kopie meshe s obráceným cullingem, vypnutá dokud není jednotka vybraná
        var outline = new GameObject("Outline");
        outline.transform.SetParent(go.transform, false);
        outline.AddComponent<MeshFilter>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
        outline.AddComponent<MeshRenderer>().sharedMaterial = GetOutlineMaterial();
        outline.SetActive(false);
        u.outline = outline;

        u.view.position = UnitPos(u, u.cell);
        units.Add(u);
    }

    Material GetOutlineMaterial()
    {
        if (outlineMaterial != null) return outlineMaterial;
        var sh = Shader.Find("World/Outline");
        if (sh == null) { Debug.LogError("Shader World/Outline nenalezen. Přidej Outline.shader do projektu."); return null; }
        outlineMaterial = new Material(sh);
        return outlineMaterial;
    }

    Vector3 UnitPos(Unit u, Cell c) => worldView.CellPos(c, u.moveType == MoveType.Air ? 2.5f : 0.5f);

    // ------------------------------------------------------------ výběr a příkazy
    Cell PickCell() =>
        worldView.RaycastCell(Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue()));

    void ClickSelect()
    {
        // 1) klik přímo na jednotku
        var ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out var hit, 10000f))
        {
            var r = hit.collider.GetComponentInParent<UnitRef>();
            if (r != null) { SetSelected(r.unit); return; }
        }

        // 2) klik na buňku: vybere jednotku na ní (opakovaně přepíná mezi nimi)
        Cell c = PickCell();
        Unit next = null;
        if (c != null)
        {
            var here = units.FindAll(u => u.cell == c);
            if (here.Count > 0)
            {
                int i = here.IndexOf(selected);          // -1 pokud nic nevybráno
                next = here[(i + 1) % here.Count];
            }
        }
        SetSelected(next);
    }

    void SetSelected(Unit u)
    {
        selected = u;
        foreach (var other in units)
            if (other.outline != null) other.outline.SetActive(other == selected);
        UpdateLine();
        if (selected != null) Debug.Log($"{selected.name}: pohyb {selected.movePoints}/{selected.maxMovePoints}");
    }

    void OrderMove(Unit u, Cell goal)
    {
        if (u.moving) { Say($"{u.name} se právě pohybuje, počkej, až dojde."); return; }
        if (goal == u.cell) { Say($"{u.name} už na té buňce stojí."); return; }
        if (!u.CanEnter(goal)) { Say($"{u.name} nemůže na tuto buňku (voda nebo hory)."); return; }

        var path = Pathfinder.FindPath(world, u, goal);
        if (path == null) { Say($"{u.name} se tam nedostane (jiná pevnina nebo zablokovaná cesta)."); return; }

        u.path = path;
        if (u.movePoints <= 0f)
            Say($"{u.name} nemá pohyb. Cesta uložena ({path.Count} buněk), vyrazí v dalším kole (mezerník).");
        else
            Say($"{u.name}: cesta {path.Count} buněk.");
        Advance(u);
    }

    void NextTurn()
    {
        if (units.Exists(x => x.moving)) { Say("Počkej, až se jednotky dopohybují."); return; }
        foreach (var u in units)
        {
            u.StartTurn();
            if (u.path.Count > 0) Advance(u);
        }
        Debug.Log("Nové kolo");
    }

    void Advance(Unit u)
    {
        var moved = u.AdvanceAlongPath();
        if (moved.Count > 0) { u.moving = true; StartCoroutine(Animate(u, moved)); }
        UpdateLine();
    }

    // ------------------------------------------------------------ vizuál
    IEnumerator Animate(Unit u, List<Cell> cells)
    {
        foreach (var c in cells)
        {
            Vector3 target = UnitPos(u, c);
            while ((u.view.position - target).sqrMagnitude > 0.0001f)
            {
                u.view.position = Vector3.MoveTowards(u.view.position, target, moveSpeed * Time.deltaTime);
                yield return null;
            }
        }
        u.moving = false;
        UpdateLine();
    }

    void Say(string text)
    {
        message = text;
        Debug.Log(text);
    }

    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
        style.normal.textColor = Color.white;

        string info = selected != null
            ? $"Vybráno: {selected.name}   pohyb {selected.movePoints}/{selected.maxMovePoints}   zbývá buněk: {selected.path.Count}"
            : "Nic nevybráno (levé tl. = výběr, pravé = cíl, mezerník = další kolo)";

        GUI.Label(new Rect(10, 10, 900, 24), info, style);
        GUI.Label(new Rect(10, 34, 900, 24), message, style);
    }

    void UpdateLine()
    {
        if (line == null) return;
        if (selected == null || selected.path.Count == 0) { line.positionCount = 0; return; }

        line.positionCount = selected.path.Count + 1;
        line.SetPosition(0, worldView.CellPos(selected.cell, 0.4f));
        for (int i = 0; i < selected.path.Count; i++)
            line.SetPosition(i + 1, worldView.CellPos(selected.path[i], 0.4f));
    }
}
