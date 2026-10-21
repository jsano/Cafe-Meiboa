using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class CafeBoard : MonoBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField] Transform backdrop;
    [SerializeField] SpriteRenderer dragToken;
    [SerializeField] CafeHotspot[] hotspots;
    [SerializeField] SpriteRenderer[] cupMarks;

    CafeSession session;
    bool built;
    bool dragging;
    bool panning;
    Vector3 lastMouse;
    readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    CafeHotspot held;
    Transform ghost;
    readonly List<CafeHotspot> trays = new List<CafeHotspot>();

    public void Attach(CafeSession cafeSession)
    {
        session = cafeSession;
        if (cam == null)
            cam = Camera.main;
        if (dragToken != null)
            dragToken.gameObject.SetActive(false);
        WireHotspots();
        ClampCamera();
        built = true;
    }

    void WireHotspots()
    {
        trays.Clear();
        if (hotspots == null)
            return;

        for (int i = 0; i < hotspots.Length; i++)
        {
            CafeHotspot hotspot = hotspots[i];
            if (hotspot == null)
                continue;
            hotspot.Board = this;
            if (hotspot.Spot == CafeSpot.Tray)
                trays.Add(hotspot);
        }

        trays.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
        for (int i = 0; i < trays.Count; i++)
            trays[i].Index = i;
    }

    void Update()
    {
        if (!built)
            return;

        Pan();
        if (LeftPressed() && !dragging && !PointerOverUi())
        {
            CafeHotspot spot = HotspotUnderCursor();
            if (spot != null)
                Press(spot);
        }
        else if (dragging && held != null && LeftReleased())
        {
            Release(held);
        }
        FollowGhost();
        SyncCups();
    }

    static Vector2 PointerScreen()
    {
        if (Mouse.current != null)
            return Mouse.current.position.ReadValue();
        return Input.mousePosition;
    }

    static bool LeftPressed()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;
        return Input.GetMouseButtonDown(0);
    }

    static bool LeftReleased()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
            return true;
        return Input.GetMouseButtonUp(0);
    }

    static bool RightHeld()
    {
        if (Mouse.current != null && Mouse.current.rightButton.isPressed)
            return true;
        return Input.GetMouseButton(1);
    }

    bool PointerOverUi()
    {
        if (EventSystem.current == null)
            return false;

        PointerEventData data = new PointerEventData(EventSystem.current);
        data.position = PointerScreen();
        uiHits.Clear();
        EventSystem.current.RaycastAll(data, uiHits);
        for (int i = 0; i < uiHits.Count; i++)
        {
            GameObject hit = uiHits[i].gameObject;
            if (hit == null || hit.GetComponentInParent<CafeHotspot>() != null)
                continue;
            return true;
        }
        return false;
    }

    public void Press(CafeHotspot spot)
    {
        if (session == null || dragging || dragToken == null)
            return;
        if (!CanPick(spot))
            return;

        dragging = true;
        held = spot;
        dragToken.color = SpotColor(spot.Spot, session, spot.Index);
        dragToken.sortingOrder = 40;
        dragToken.gameObject.SetActive(true);
        ghost = dragToken.transform;
        FollowGhost();
    }

    public void Release(CafeHotspot spot)
    {
        if (!dragging || spot != held)
            return;

        dragging = false;
        CafeHotspot target = PointerOverUi() ? null : HotspotUnderCursor();
        if (dragToken != null)
            dragToken.gameObject.SetActive(false);
        ghost = null;
        held = null;

        if (target == null || target == spot)
            return;

        Route(spot, target);
    }

    void Route(CafeHotspot source, CafeHotspot target)
    {
        if (source.Spot == CafeSpot.Beans && target.Spot == CafeSpot.Coffee)
            session.BrewCoffee();
        else if (source.Spot == CafeSpot.Coffee && target.Spot == CafeSpot.Espresso)
            session.MakeEspresso();
        else if (source.Spot == CafeSpot.CupHot && target.Spot == CafeSpot.Tray)
            session.PlaceCup(true, target.Index);
        else if (source.Spot == CafeSpot.CupCold && target.Spot == CafeSpot.Tray)
            session.PlaceCup(false, target.Index);
        else if (source.Spot == CafeSpot.Straw && target.Spot == CafeSpot.Tray)
            session.AddStraw(target.Index);
        else if (source.Spot == CafeSpot.Ice && target.Spot == CafeSpot.Tray)
            session.AddIce(target.Index);
        else if (source.Spot == CafeSpot.WaterHot && target.Spot == CafeSpot.Tray)
            session.AddWater(target.Index, true);
        else if (source.Spot == CafeSpot.WaterCold && target.Spot == CafeSpot.Tray)
            session.AddWater(target.Index, false);
        else if (source.Spot == CafeSpot.Milk && target.Spot == CafeSpot.SteamedMilk)
            session.SteamMilk();
        else if (IsSyrup(source.Spot) && target.Spot == CafeSpot.Tray)
            session.AddSyrup(target.Index, SyrupId(source.Spot));
        else if (source.Spot == CafeSpot.Tray && target.Spot == CafeSpot.Trash)
            session.TrashCup(source.Index);
        else if (source.Spot == CafeSpot.Tray && target.Spot == CafeSpot.Tray)
            session.MoveCup(source.Index, target.Index);
        else if (source.Spot == CafeSpot.CroissantPlain && target.Spot == CafeSpot.Toaster)
            session.HeatCroissant(PastryType.Plain);
        else if (source.Spot == CafeSpot.CroissantChocolate && target.Spot == CafeSpot.Toaster)
            session.HeatCroissant(PastryType.Chocolate);
        else if (source.Spot == CafeSpot.CroissantCheese && target.Spot == CafeSpot.Toaster)
            session.HeatCroissant(PastryType.Cheese);
        else
            session.Status = "That doesn't go there.";
    }

    bool CanPick(CafeHotspot spot)
    {
        if (spot.Spot != CafeSpot.Tray)
            return true;
        if (session == null || spot.Index < 0 || spot.Index >= session.Trays.Length)
            return false;
        return session.Trays[spot.Index].Occupied;
    }

    void Pan()
    {
        if (cam == null)
            return;

        bool right = RightHeld();
        if (!right)
        {
            panning = false;
            ClampCamera();
            return;
        }

        Vector2 mouse = PointerScreen();
        if (!panning)
        {
            panning = true;
            lastMouse = mouse;
            return;
        }

        float units = cam.orthographicSize * 2f / Screen.height;
        float dx = (mouse.x - lastMouse.x) * units;
        cam.transform.position -= new Vector3(dx, 0f, 0f);
        lastMouse = mouse;
        ClampCamera();
    }

    void ClampCamera()
    {
        if (backdrop == null || cam == null)
            return;

        SpriteRenderer art = backdrop.GetComponent<SpriteRenderer>();
        if (art == null || art.sprite == null)
            return;

        float halfView = cam.orthographicSize * cam.aspect;
        float halfArt = art.bounds.extents.x;
        float min = art.bounds.center.x - halfArt + halfView;
        float max = art.bounds.center.x + halfArt - halfView;
        Vector3 pos = cam.transform.position;
        pos.x = min > max ? art.bounds.center.x : Mathf.Clamp(pos.x, min, max);
        cam.transform.position = pos;
    }

    void FollowGhost()
    {
        if (ghost == null || cam == null || !ghost.gameObject.activeSelf)
            return;

        ghost.position = MouseWorld();
    }

    Vector3 MouseWorld()
    {
        Vector3 screen = PointerScreen();
        screen.z = Mathf.Abs(cam.transform.position.z);
        Vector3 world = cam.ScreenToWorldPoint(screen);
        world.z = 0f;
        return world;
    }

    void SyncCups()
    {
        if (session == null || cupMarks == null)
            return;

        for (int i = 0; i < cupMarks.Length; i++)
        {
            if (cupMarks[i] == null)
                continue;
            bool show = i < session.Trays.Length && session.Trays[i].Occupied;
            cupMarks[i].enabled = show;
            if (!show)
                continue;

            TrayCup cup = session.Trays[i];
            Color color = cup.Hot ? new Color(0.93f, 0.35f, 0.30f) : new Color(0.55f, 0.82f, 0.95f);
            if (cup.Syrup != 0)
                color = Color.Lerp(color, SyrupColor(cup.Syrup), 0.55f);
            if (cup.Ice)
                color = Color.Lerp(color, new Color(0.8f, 0.95f, 1f), 0.35f);
            cupMarks[i].color = color;
        }
    }

    CafeHotspot HotspotUnderCursor()
    {
        Vector3 world = MouseWorld();
        Collider2D[] hits = Physics2D.OverlapPointAll(world);
        CafeHotspot found = null;
        for (int i = 0; i < hits.Length; i++)
        {
            CafeHotspot hotspot = hits[i].GetComponent<CafeHotspot>();
            if (hotspot == null || hotspot == held)
                continue;
            found = hotspot;
        }
        return found;
    }

    static bool IsSyrup(CafeSpot spot)
    {
        return spot == CafeSpot.SyrupChocolate
            || spot == CafeSpot.SyrupVanilla
            || spot == CafeSpot.SyrupStrawberry
            || spot == CafeSpot.SyrupHoney;
    }

    static int SyrupId(CafeSpot spot)
    {
        if (spot == CafeSpot.SyrupChocolate)
            return 1;
        if (spot == CafeSpot.SyrupVanilla)
            return 2;
        if (spot == CafeSpot.SyrupStrawberry)
            return 3;
        if (spot == CafeSpot.SyrupHoney)
            return 4;
        return 0;
    }

    static Color SyrupColor(int syrup)
    {
        if (syrup == 1)
            return new Color(0.40f, 0.22f, 0.14f);
        if (syrup == 2)
            return new Color(0.95f, 0.90f, 0.55f);
        if (syrup == 3)
            return new Color(0.90f, 0.35f, 0.45f);
        if (syrup == 4)
            return new Color(0.90f, 0.70f, 0.20f);
        return Color.white;
    }

    static Color SpotColor(CafeSpot spot, CafeSession session, int index)
    {
        if (spot == CafeSpot.Beans)
            return new Color(0.36f, 0.22f, 0.12f);
        if (spot == CafeSpot.Coffee)
            return new Color(0.28f, 0.14f, 0.08f);
        if (spot == CafeSpot.CupHot)
            return new Color(0.92f, 0.32f, 0.28f);
        if (spot == CafeSpot.CupCold)
            return new Color(0.45f, 0.75f, 0.95f);
        if (spot == CafeSpot.Straw)
            return new Color(0.55f, 0.75f, 0.30f);
        if (spot == CafeSpot.Ice)
            return new Color(0.75f, 0.92f, 1f);
        if (spot == CafeSpot.WaterHot)
            return new Color(0.95f, 0.55f, 0.30f);
        if (spot == CafeSpot.WaterCold)
            return new Color(0.40f, 0.70f, 0.95f);
        if (spot == CafeSpot.Milk)
            return new Color(0.96f, 0.96f, 0.92f);
        if (spot == CafeSpot.CroissantPlain)
            return new Color(0.93f, 0.78f, 0.45f);
        if (spot == CafeSpot.CroissantChocolate)
            return new Color(0.55f, 0.32f, 0.18f);
        if (spot == CafeSpot.CroissantCheese)
            return new Color(0.95f, 0.82f, 0.35f);
        if (IsSyrup(spot))
            return SyrupColor(SyrupId(spot));
        if (spot == CafeSpot.Tray && session != null && index >= 0 && index < session.Trays.Length && session.Trays[index].Occupied)
            return session.Trays[index].Hot ? new Color(0.92f, 0.32f, 0.28f) : new Color(0.45f, 0.75f, 0.95f);
        return new Color(0.85f, 0.75f, 0.6f);
    }
}
