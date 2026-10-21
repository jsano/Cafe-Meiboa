using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CafeBoard : MonoBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField] Transform backdrop;
    [SerializeField] SpriteRenderer dragToken;
    [SerializeField] CafeHotspot[] hotspots;
    [SerializeField] SpriteRenderer[] cupMarks;
    [SerializeField] Collider2D bell;

    const float ShopDim = 0.4f;

    CafeSession session;
    bool built;
    bool dragging;
    bool panning;
    Vector3 lastMouse;
    readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    CafeHotspot held;
    Transform ghost;
    readonly List<CafeHotspot> trays = new List<CafeHotspot>();
    SpriteRenderer[] boardSprites = System.Array.Empty<SpriteRenderer>();
    Color[] boardColors = System.Array.Empty<Color>();
    SpriteRenderer waterMark;

    public void Attach(CafeSession cafeSession)
    {
        session = cafeSession;
        if (cam == null)
            cam = Camera.main;
        if (dragToken != null)
            dragToken.gameObject.SetActive(false);
        WireHotspots();
        EnsureWaterMark();
        ResolveBell();
        CacheBoardColors();
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

        bool shop = session != null && session.Phase == DayPhase.Shop;
        Pan();
        if (shop)
        {
            if (dragging)
                CancelDrag();
        }
        else if (LeftPressed() && !dragging && !PointerOverUi())
        {
            if (BellUnderCursor())
                session.RingBell();
            else
            {
                CafeHotspot spot = HotspotUnderCursor();
                if (spot != null)
                    HandleSpot(spot);
            }
        }
        else if (dragging && held != null && LeftReleased())
        {
            Release(held);
        }
        FollowGhost();
        SyncCups();
        ApplyDim(shop);
    }

    void CacheBoardColors()
    {
        boardSprites = System.Array.Empty<SpriteRenderer>();
        boardColors = System.Array.Empty<Color>();
        if (backdrop == null)
            return;

        SpriteRenderer[] all = backdrop.GetComponentsInChildren<SpriteRenderer>(true);
        int count = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (!IsCupMark(all[i]))
                count++;
        }

        boardSprites = new SpriteRenderer[count];
        boardColors = new Color[count];
        int write = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (IsCupMark(all[i]))
                continue;
            boardSprites[write] = all[i];
            boardColors[write] = all[i].color;
            write++;
        }
    }

    bool IsCupMark(SpriteRenderer renderer)
    {
        if (cupMarks == null || renderer == null)
            return false;
        for (int i = 0; i < cupMarks.Length; i++)
        {
            if (cupMarks[i] == renderer)
                return true;
        }
        return false;
    }

    void ApplyDim(bool shop)
    {
        float mul = shop ? ShopDim : 1f;
        for (int i = 0; i < boardSprites.Length; i++)
        {
            SpriteRenderer renderer = boardSprites[i];
            if (renderer == null)
                continue;
            Color color = boardColors[i];
            renderer.color = new Color(color.r * mul, color.g * mul, color.b * mul, color.a);
        }

        if (!shop || cupMarks == null)
            return;

        for (int i = 0; i < cupMarks.Length; i++)
        {
            SpriteRenderer mark = cupMarks[i];
            if (mark == null || !mark.enabled)
                continue;
            Color color = mark.color;
            mark.color = new Color(color.r * ShopDim, color.g * ShopDim, color.b * ShopDim, color.a);
        }
    }

    void CancelDrag()
    {
        dragging = false;
        held = null;
        ghost = null;
        if (dragToken != null)
            dragToken.gameObject.SetActive(false);
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
            if (hit == null || hit.GetComponentInParent<Graphic>() == null)
                continue;
            return true;
        }
        return false;
    }

    void HandleSpot(CafeHotspot spot)
    {
        if (MachineBusy(spot.Spot))
            return;
        if (IsClick(spot))
        {
            Click(spot);
            return;
        }

        Press(spot);
    }

    bool MachineBusy(CafeSpot spot)
    {
        if (session == null)
            return false;
        if (spot == CafeSpot.Grinder)
            return session.GrinderBusy;
        if (spot == CafeSpot.Espresso)
            return session.EspressoBusy;
        if (spot == CafeSpot.Toaster)
            return session.ToasterBusy;
        if (spot == CafeSpot.Coffee)
            return session.CoffeeBusy;
        return false;
    }

    bool IsClick(CafeHotspot spot)
    {
        if (spot.Spot == CafeSpot.WaterHot || spot.Spot == CafeSpot.WaterCold)
            return true;
        if (spot.Spot == CafeSpot.WaterTray)
            return session == null || !session.WaterCup.Occupied;
        if (spot.Spot == CafeSpot.Coffee)
            return session == null || session.CoffeeUses <= 0;
        return false;
    }

    void Click(CafeHotspot spot)
    {
        if (session == null)
            return;
        if (spot.Spot == CafeSpot.WaterHot)
            session.FillWaterCup(true);
        else if (spot.Spot == CafeSpot.WaterCold)
            session.FillWaterCup(false);
        else if (spot.Spot == CafeSpot.Coffee)
            session.StartCoffee();
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
        if (MachineBusy(target.Spot))
            return;

        if (source.Spot == CafeSpot.Beans && target.Spot == CafeSpot.Grinder)
            session.InsertBeans();
        else if (source.Spot == CafeSpot.CoffeePuck && target.Spot == CafeSpot.Espresso)
            session.LoadPuck();
        else if (source.Spot == CafeSpot.CoffeePuck)
            session.Status = "The puck goes in the espresso machine.";
        else if (source.Spot == CafeSpot.Espresso && target.Spot == CafeSpot.Tray)
            session.PourShot(target.Index);
        else if (source.Spot == CafeSpot.Espresso && target.Spot == CafeSpot.WaterTray)
            session.PourShotOnWater();
        else if (source.Spot == CafeSpot.Espresso)
            session.Status = "The shot goes in a cup.";
        else if (source.Spot == CafeSpot.Coffee && target.Spot == CafeSpot.Tray)
            session.PourCoffee(target.Index);
        else if (source.Spot == CafeSpot.Coffee && target.Spot == CafeSpot.WaterTray)
            session.PourCoffeeOnWater();
        else if (source.Spot == CafeSpot.CupHot && target.Spot == CafeSpot.Tray)
            session.PlaceCup(true, target.Index);
        else if (source.Spot == CafeSpot.CupCold && target.Spot == CafeSpot.Tray)
            session.PlaceCup(false, target.Index);
        else if (source.Spot == CafeSpot.CupHot && target.Spot == CafeSpot.WaterTray)
            session.InsertWaterCup(true);
        else if (source.Spot == CafeSpot.CupCold && target.Spot == CafeSpot.WaterTray)
            session.InsertWaterCup(false);
        else if (source.Spot == CafeSpot.Straw && target.Spot == CafeSpot.Tray)
            session.AddStraw(target.Index);
        else if (source.Spot == CafeSpot.Ice && target.Spot == CafeSpot.Tray)
            session.AddIce(target.Index);
        else if (source.Spot == CafeSpot.Milk && target.Spot == CafeSpot.SteamedMilk)
            session.SteamMilk();
        else if (IsSyrup(source.Spot) && target.Spot == CafeSpot.Tray)
            session.AddSyrup(target.Index, SyrupId(source.Spot));
        else if (source.Spot == CafeSpot.Tray && target.Spot == CafeSpot.Trash)
            session.TrashCup(source.Index);
        else if (source.Spot == CafeSpot.Tray && target.Spot == CafeSpot.Tray)
            session.MoveCup(source.Index, target.Index);
        else if (source.Spot == CafeSpot.Tray && target.Spot == CafeSpot.WaterTray)
            session.MoveCupToWater(source.Index);
        else if (source.Spot == CafeSpot.WaterTray && target.Spot == CafeSpot.Tray)
            session.PlaceWaterCup(target.Index);
        else if (source.Spot == CafeSpot.WaterTray && target.Spot == CafeSpot.Trash)
            session.TrashWaterCup();
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
        if (session == null)
            return false;
        if (spot.Spot == CafeSpot.Trash
            || spot.Spot == CafeSpot.Grinder
            || spot.Spot == CafeSpot.Toaster
            || spot.Spot == CafeSpot.SteamedMilk
            || spot.Spot == CafeSpot.WaterHot
            || spot.Spot == CafeSpot.WaterCold)
            return false;
        if (spot.Spot == CafeSpot.Coffee)
            return session.CoffeeUses > 0 && !session.CoffeeBusy;
        if (spot.Spot == CafeSpot.CoffeePuck)
            return session.PuckReady;
        if (spot.Spot == CafeSpot.Espresso)
            return session.EspressoShots > 0 && !session.EspressoBusy;
        if (spot.Spot == CafeSpot.WaterTray)
            return session.WaterCup.Occupied;
        if (spot.Spot != CafeSpot.Tray)
            return true;
        if (spot.Index < 0 || spot.Index >= session.Trays.Length)
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

            cupMarks[i].color = CupTint(session.Trays[i]);
        }

        if (waterMark != null)
        {
            bool show = session.WaterCup.Occupied;
            waterMark.enabled = show;
            if (show)
                waterMark.color = CupTint(session.WaterCup);
        }
    }

    void EnsureWaterMark()
    {
        if (waterMark != null || cupMarks == null || cupMarks.Length == 0 || cupMarks[0] == null)
            return;

        CafeHotspot tray = null;
        if (hotspots != null)
        {
            for (int i = 0; i < hotspots.Length; i++)
            {
                if (hotspots[i] != null && hotspots[i].Spot == CafeSpot.WaterTray)
                    tray = hotspots[i];
            }
        }
        if (tray == null)
            return;

        GameObject mark = new GameObject("WaterCupMark");
        mark.transform.SetParent(tray.transform, false);
        mark.transform.localPosition = new Vector3(0f, -1.8f, 0f);
        mark.transform.localScale = cupMarks[0].transform.localScale;
        waterMark = mark.AddComponent<SpriteRenderer>();
        waterMark.sprite = cupMarks[0].sprite;
        waterMark.sortingOrder = 6;
        waterMark.enabled = false;
    }

    void ResolveBell()
    {
        if (bell != null)
            return;

        GameObject found = GameObject.Find("Bell");
        if (found != null)
            bell = found.GetComponent<Collider2D>();
    }

    bool BellUnderCursor()
    {
        if (bell == null)
            return false;

        for (int i = 0; i < uiHits.Count; i++)
        {
            GameObject hit = uiHits[i].gameObject;
            if (hit != null && hit.GetComponent<Collider2D>() == bell)
                return true;
        }

        Vector3 world = MouseWorld();
        Collider2D[] hits = Physics2D.OverlapPointAll(world);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == bell)
                return true;
        }
        return false;
    }

    CafeHotspot HotspotUnderCursor()
    {
        Vector3 world = MouseWorld();
        Collider2D[] hits = Physics2D.OverlapPointAll(world);
        CafeHotspot found = null;
        float bestArea = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            CafeHotspot hotspot = hits[i].GetComponent<CafeHotspot>();
            if (hotspot == null || hotspot == held)
                continue;
            Vector2 size = hits[i].bounds.size;
            float area = Mathf.Abs(size.x * size.y);
            if (found != null && area >= bestArea)
                continue;
            found = hotspot;
            bestArea = area;
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
        if (spot == CafeSpot.CoffeePuck)
            return new Color(0.22f, 0.12f, 0.08f);
        if (spot == CafeSpot.Espresso)
            return new Color(0.32f, 0.16f, 0.08f);
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
            return CupTint(session.Trays[index]);
        if (spot == CafeSpot.WaterTray && session != null && session.WaterCup.Occupied)
            return CupTint(session.WaterCup);
        return new Color(0.85f, 0.75f, 0.6f);
    }

    static Color CupTint(TrayCup cup)
    {
        Color color = cup.Hot ? new Color(0.93f, 0.35f, 0.30f) : new Color(0.55f, 0.82f, 0.95f);
        if (cup.Coffee)
            color = Color.Lerp(color, new Color(0.28f, 0.14f, 0.08f), 0.65f);
        if (cup.Shot)
            color = Color.Lerp(color, new Color(0.18f, 0.08f, 0.04f), 0.45f);
        if (cup.Water == 1)
            color = Color.Lerp(color, new Color(0.95f, 0.55f, 0.30f), 0.35f);
        else if (cup.Water == 2)
            color = Color.Lerp(color, new Color(0.40f, 0.70f, 0.95f), 0.35f);
        if (cup.Syrup != 0)
            color = Color.Lerp(color, SyrupColor(cup.Syrup), 0.55f);
        if (cup.Ice)
            color = Color.Lerp(color, new Color(0.8f, 0.95f, 1f), 0.35f);
        return color;
    }
}
