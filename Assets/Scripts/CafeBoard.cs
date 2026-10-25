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
    [SerializeField] Sprite hotCupSprite;
    [SerializeField] Sprite coldCupSprite;
    [SerializeField] Sprite plainCroissantSprite;
    [SerializeField] Sprite chocolateCroissantSprite;
    [SerializeField] Sprite cheeseCroissantSprite;
    [SerializeField] Sprite vanillaSyrupSprite;
    [SerializeField] Sprite chocolateSyrupSprite;
    [SerializeField] Sprite honeySyrupSprite;
    [SerializeField] Sprite strawberrySyrupSprite;
    [SerializeField] Sprite milkSprite;
    [SerializeField] Sprite iceSprite;
    [SerializeField] Sprite puckSprite;
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
    SpriteRenderer plateMark;
    SpriteRenderer toastMark;
    SpriteRenderer puckMark;
    SpriteRenderer espressoPuckMark;
    Sprite placeholderSprite;
    Vector3 placeholderScale;

    public void Attach(CafeSession cafeSession)
    {
        session = cafeSession;
        if (cam == null)
            cam = Camera.main;
        if (dragToken != null)
        {
            placeholderSprite = dragToken.sprite;
            placeholderScale = dragToken.transform.localScale;
            dragToken.gameObject.SetActive(false);
        }
        WireHotspots();
        EnsureStationMarks();
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
        SyncStations();
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
            if (!IsKeptSprite(all[i]))
                count++;
        }

        boardSprites = new SpriteRenderer[count];
        boardColors = new Color[count];
        int write = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (IsKeptSprite(all[i]))
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

    bool IsKeptSprite(SpriteRenderer renderer)
    {
        return IsCupMark(renderer) || renderer == waterMark || renderer == toastMark || renderer == plateMark;
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
        {
            RestorePlaceholder();
            dragToken.gameObject.SetActive(false);
        }
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
            if (hit.GetComponentInParent<CafeHotspot>() != null)
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
        ShowHeldSprite(spot);
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
        {
            RestorePlaceholder();
            dragToken.gameObject.SetActive(false);
        }
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
        else if (source.Spot == CafeSpot.Toaster && target.Spot == CafeSpot.Plate)
            session.PlateCroissant();
        else if (source.Spot == CafeSpot.Toaster)
            session.Status = "The croissant goes on the plate.";
        else
            session.Status = "That doesn't go there.";
    }

    bool CanPick(CafeHotspot spot)
    {
        if (session == null)
            return false;
        if (spot.Spot == CafeSpot.Trash
            || spot.Spot == CafeSpot.Grinder
            || spot.Spot == CafeSpot.SteamedMilk
            || spot.Spot == CafeSpot.WaterHot
            || spot.Spot == CafeSpot.WaterCold
            || spot.Spot == CafeSpot.Plate)
            return false;
        if (spot.Spot == CafeSpot.Coffee)
            return session.CoffeeUses > 0 && !session.CoffeeBusy;
        if (spot.Spot == CafeSpot.CoffeePuck)
            return session.PuckReady;
        if (spot.Spot == CafeSpot.Espresso)
            return session.EspressoShots > 0 && !session.EspressoBusy;
        if (spot.Spot == CafeSpot.Toaster)
            return session.ToasterReady != PastryType.None && !session.ToasterBusy;
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

            ShowCup(cupMarks[i], session.Trays[i]);
        }
    }

    void SyncStations()
    {
        if (session == null)
            return;

        EnsureStationMarks();
        ShowStationCup(waterMark, waterSpot, session.WaterCup);
        ShowStationPastry(toastMark, toastSpot, session.ToasterHeld);
        ShowStationPastry(plateMark, plateSpot, session.Plated);
        bool holdingPuck = dragging && held != null && held.Spot == CafeSpot.CoffeePuck;
        ShowStationSprite(puckMark, puckSpot, puckSprite, session.PuckReady && !holdingPuck);
        ShowStationSprite(espressoPuckMark, espressoSpot, puckSprite, session.EspressoBusy || session.EspressoShots > 0);
    }

    void ShowStationCup(SpriteRenderer mark, CafeHotspot spot, TrayCup cup)
    {
        if (mark == null)
            return;

        bool show = cup != null && cup.Occupied;
        mark.enabled = show;
        if (!show)
            return;

        mark.sortingOrder = 25;
        Sprite art = cup.Hot ? hotCupSprite : coldCupSprite;
        if (art != null)
            ShowArt(mark, art, spot.Spot == CafeSpot.WaterTray ? 0.62f : StationSize(spot, 0.85f));
        else
            mark.color = CupTint(cup);
        AlignToHotspot(mark, spot);
    }

    void ShowStationPastry(SpriteRenderer mark, CafeHotspot spot, PastryType pastry)
    {
        if (mark == null)
            return;

        bool show = pastry != PastryType.None;
        mark.enabled = show;
        if (!show)
            return;

        mark.sortingOrder = 25;
        Sprite art = CroissantSprite(pastry);
        if (art != null)
            ShowArt(mark, art, StationSize(spot, 0.95f));
        else
            mark.color = CroissantColor(pastry);
        AlignToHotspot(mark, spot);
    }

    void ShowStationSprite(SpriteRenderer mark, CafeHotspot spot, Sprite art, bool show)
    {
        if (mark == null)
            return;

        mark.enabled = show && art != null;
        if (!mark.enabled)
            return;

        mark.sortingOrder = 25;
        ShowArt(mark, art, StationSize(spot, 0.7f));
        AlignToHotspot(mark, spot);
    }

    void AlignToHotspot(SpriteRenderer mark, CafeHotspot spot)
    {
        if (spot == null || mark.sprite == null)
            return;

        Collider2D collider = spot.GetComponent<Collider2D>();
        Vector3 world = collider != null
            ? spot.transform.TransformPoint(collider.offset)
            : spot.transform.position;
        if (spot.Spot == CafeSpot.WaterTray)
            world += spot.transform.up * (spot.transform.lossyScale.y * -0.35f);
        mark.transform.position += world - mark.bounds.center;
    }

    float StationSize(CafeHotspot spot, float fallback)
    {
        if (spot == null)
            return fallback;

        Collider2D collider = spot.GetComponent<Collider2D>();
        if (collider == null)
            return fallback;

        float span = Mathf.Min(collider.bounds.size.x, collider.bounds.size.y);
        if (span < 0.05f)
            return fallback;
        return Mathf.Clamp(span * 0.62f, 0.55f, 1.35f);
    }

    void ShowHeldSprite(CafeHotspot spot)
    {
        Sprite art = HeldArt(spot);
        if (art == null)
        {
            RestorePlaceholder();
            dragToken.color = SpotColor(spot.Spot, session, spot.Index);
            return;
        }

        bool pastry = spot.Spot == CafeSpot.CroissantPlain
            || spot.Spot == CafeSpot.CroissantChocolate
            || spot.Spot == CafeSpot.CroissantCheese
            || spot.Spot == CafeSpot.Toaster;
        ShowArt(dragToken, art, pastry ? 0.8f : 0.72f);
    }

    void ShowCup(SpriteRenderer renderer, TrayCup cup)
    {
        Sprite art = cup.Hot ? hotCupSprite : coldCupSprite;
        if (art != null)
            ShowArt(renderer, art, 0.72f);
        else
            renderer.color = CupTint(cup);
    }

    Sprite HeldArt(CafeHotspot spot)
    {
        if (spot.Spot == CafeSpot.CupHot)
            return hotCupSprite;
        if (spot.Spot == CafeSpot.CupCold)
            return coldCupSprite;
        if (spot.Spot == CafeSpot.CroissantPlain)
            return plainCroissantSprite;
        if (spot.Spot == CafeSpot.CroissantChocolate)
            return chocolateCroissantSprite;
        if (spot.Spot == CafeSpot.CroissantCheese)
            return cheeseCroissantSprite;
        if (spot.Spot == CafeSpot.SyrupVanilla)
            return vanillaSyrupSprite;
        if (spot.Spot == CafeSpot.SyrupChocolate)
            return chocolateSyrupSprite;
        if (spot.Spot == CafeSpot.SyrupHoney)
            return honeySyrupSprite;
        if (spot.Spot == CafeSpot.SyrupStrawberry)
            return strawberrySyrupSprite;
        if (spot.Spot == CafeSpot.Milk)
            return milkSprite;
        if (spot.Spot == CafeSpot.Ice)
            return iceSprite;
        if (spot.Spot == CafeSpot.CoffeePuck)
            return puckSprite;
        if (spot.Spot == CafeSpot.Espresso && session.EspressoShots > 0)
            return puckSprite;
        if (spot.Spot == CafeSpot.Tray && spot.Index >= 0 && spot.Index < session.Trays.Length && session.Trays[spot.Index].Occupied)
            return session.Trays[spot.Index].Hot ? hotCupSprite : coldCupSprite;
        if (spot.Spot == CafeSpot.WaterTray && session.WaterCup.Occupied)
            return session.WaterCup.Hot ? hotCupSprite : coldCupSprite;
        if (spot.Spot == CafeSpot.Toaster)
            return CroissantSprite(session.ToasterHeld);
        return null;
    }

    Sprite CroissantSprite(PastryType pastry)
    {
        if (pastry == PastryType.Plain)
            return plainCroissantSprite;
        if (pastry == PastryType.Chocolate)
            return chocolateCroissantSprite;
        if (pastry == PastryType.Cheese)
            return cheeseCroissantSprite;
        return null;
    }

    void ShowArt(SpriteRenderer renderer, Sprite sprite, float worldSize)
    {
        renderer.sprite = sprite;
        renderer.color = Color.white;
    }

    void RestorePlaceholder()
    {
        if (dragToken == null)
            return;
        if (placeholderSprite != null)
            dragToken.sprite = placeholderSprite;
        if (placeholderScale != Vector3.zero)
            dragToken.transform.localScale = placeholderScale;
    }

    CafeHotspot waterSpot;
    CafeHotspot toastSpot;
    CafeHotspot plateSpot;
    CafeHotspot puckSpot;
    CafeHotspot espressoSpot;

    void EnsureStationMarks()
    {
        if (hotspots == null)
            return;

        RememberStation(hotspots);
        if (waterSpot == null || toastSpot == null || plateSpot == null || puckSpot == null || espressoSpot == null)
            RememberStation(FindObjectsByType<CafeHotspot>(FindObjectsInactive.Include, FindObjectsSortMode.None));

        if (waterSpot != null && waterMark == null)
            waterMark = MakeStationMark(waterSpot.transform, "WaterCupMark");
        if (toastSpot != null && toastMark == null)
            toastMark = MakeStationMark(toastSpot.transform, "ToastMark");
        if (plateSpot != null && plateMark == null)
            plateMark = MakeStationMark(plateSpot.transform, "PlateMark");
        if (puckSpot != null && puckMark == null)
            puckMark = MakeStationMark(puckSpot.transform, "PuckMark");
        if (espressoSpot != null && espressoPuckMark == null)
            espressoPuckMark = MakeStationMark(espressoSpot.transform, "EspressoPuckMark");
    }

    void RememberStation(CafeHotspot[] spots)
    {
        if (spots == null)
            return;

        for (int i = 0; i < spots.Length; i++)
        {
            CafeHotspot hotspot = spots[i];
            if (hotspot == null)
                continue;
            if (hotspot.Spot == CafeSpot.WaterTray)
                waterSpot = hotspot;
            else if (hotspot.Spot == CafeSpot.Toaster)
                toastSpot = hotspot;
            else if (hotspot.Spot == CafeSpot.Plate)
                plateSpot = hotspot;
            else if (hotspot.Spot == CafeSpot.CoffeePuck)
                puckSpot = hotspot;
            else if (hotspot.Spot == CafeSpot.Espresso)
                espressoSpot = hotspot;
        }
    }

    SpriteRenderer MakeStationMark(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            SpriteRenderer found = existing.GetComponent<SpriteRenderer>();
            if (found != null)
                return found;
        }

        GameObject mark = new GameObject(name);
        mark.transform.SetParent(parent, false);
        SpriteRenderer renderer = mark.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 25;
        renderer.enabled = false;
        if (cupMarks != null && cupMarks.Length > 0 && cupMarks[0] != null)
            renderer.sharedMaterial = cupMarks[0].sharedMaterial;
        return renderer;
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
        if (spot == CafeSpot.Toaster && session != null)
            return CroissantColor(session.ToasterReady);
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

    static Color CroissantColor(PastryType pastry)
    {
        if (pastry == PastryType.Chocolate)
            return new Color(0.55f, 0.32f, 0.18f);
        if (pastry == PastryType.Cheese)
            return new Color(0.95f, 0.82f, 0.35f);
        if (pastry == PastryType.Plain)
            return new Color(0.93f, 0.78f, 0.45f);
        return new Color(0.85f, 0.75f, 0.6f);
    }
}
