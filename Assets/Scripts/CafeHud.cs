using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CafeHud : MonoBehaviour
{
    CafeSession session;

    Text hudText;
    Text leftNote;
    GameObject shopRoot;
    Text shopHint;
    Text beansLabel;
    Text milkLabel;
    Text syrupLabel;
    Text mechanicLabel;
    Text fasterLabel;
    Text patienceLabel;
    Text rentUpgradeLabel;
    readonly Text[] pastryLabels = new Text[3];
    Text prepShotLabel;
    Text prepMilkLabel;
    GameObject dogRow;
    Button payButton;
    Text payLabel;
    Text openLabel;

    GameObject stationRoot;
    Text stepsText;
    RectTransform greenZone;
    RectTransform marker;
    Text actionLabel;
    GameObject repairRoot;
    Image repairFill;
    Text repairCaption;
    HoldButton repairHold;
    Text stationStatus;

    GameObject closeRoot;
    Text closeBody;
    Text closeButtonLabel;
    GameObject summaryRoot;
    Text summaryBody;

    readonly SlotView[] slotViews = { new SlotView(), new SlotView() };

    static Sprite white;
    static Font font;

    public bool RepairHeld => repairHold != null && repairHold.Held;

    public void Build(CafeSession cafeSession)
    {
        session = cafeSession;
        EnsureFontAndSprite();
        EnsureEventSystem();

        GameObject canvasGo = new GameObject("CafeCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        RectTransform root = canvasGo.GetComponent<RectTransform>();
        Image backdrop = Panel(root, "Backdrop", Vector2.zero, Vector2.one, new Color(0.95f, 0.89f, 0.78f));
        backdrop.raycastTarget = false;

        Image floor = Panel(root, "Floor", new Vector2(0.04f, 0.06f), new Vector2(0.42f, 0.30f), new Color(0.45f, 0.30f, 0.20f));
        floor.raycastTarget = false;
        Text sign = Label(root, "Cafe Meiboa", 42, TextAnchor.MiddleLeft, new Color(0.28f, 0.16f, 0.10f));
        Place(sign.rectTransform, new Vector2(0.05f, 0.84f), new Vector2(0.40f, 0.93f));
        sign.raycastTarget = false;

        BuildBarista(root);
        leftNote = Label(root, "", 26, TextAnchor.UpperLeft, new Color(0.28f, 0.16f, 0.10f));
        Place(leftNote.rectTransform, new Vector2(0.06f, 0.34f), new Vector2(0.40f, 0.62f));
        leftNote.raycastTarget = false;

        BuildSlot(root, 0, new Vector2(0.05f, 0.36f), new Vector2(0.22f, 0.82f));
        BuildSlot(root, 1, new Vector2(0.24f, 0.36f), new Vector2(0.41f, 0.82f));

        hudText = Label(root, "", 22, TextAnchor.MiddleLeft, new Color(0.98f, 0.95f, 0.88f));
        Image hudBar = Panel(root, "HudBar", new Vector2(0f, 0.93f), Vector2.one, new Color(0.24f, 0.14f, 0.10f));
        hudBar.raycastTarget = false;
        hudText.transform.SetParent(hudBar.transform, false);
        Stretch((RectTransform)hudText.transform, 16f, 8f, 16f, 8f);
        hudText.raycastTarget = false;

        BuildShop(root);
        BuildStation(root);
        BuildClose(root);
        BuildSummary(root);
        Refresh();
    }

    public void Refresh()
    {
        if (session == null)
            return;

        bool shift = session.Phase == DayPhase.Shift;
        bool shop = session.Phase == DayPhase.Shop;
        shopRoot.SetActive(shop);
        stationRoot.SetActive(shift);
        closeRoot.SetActive(session.Phase == DayPhase.CloseOfDay);
        summaryRoot.SetActive(session.Phase == DayPhase.Summary);
        leftNote.gameObject.SetActive(!shift);

        hudText.text = "Day " + session.Day
            + "    $" + session.Cash
            + "    Beans " + session.Beans
            + "    Milk " + session.Milk
            + "    Pastry " + session.PastryCount
            + "    " + session.QuestLabel()
            + "    Prep " + session.PrepLabel()
            + "    " + (session.MachineBroken ? "Brewer BROKEN" : "Brewer ok");

        if (shop)
            RefreshShop();
        if (shift)
            RefreshShift();
        if (session.Phase == DayPhase.CloseOfDay)
            RefreshClose();
        if (session.Phase == DayPhase.Summary)
            summaryBody.text = SummaryText();

        if (!shift)
            leftNote.text = shop
                ? "The counter is closed.\nStock the right side, bake 3 croissants, then open."
                : "";

        for (int i = 0; i < slotViews.Length; i++)
            RefreshSlot(i, shift);
    }

    void RefreshShop()
    {
        shopHint.text = session.Status + "\n" + session.StockAdvice();
        beansLabel.text = "Beans x4  ($" + CafeBalance.BeanPackCost + ")     have " + session.Beans;
        milkLabel.text = "Milk x4  ($" + CafeBalance.MilkPackCost + ")     have " + session.Milk;
        syrupLabel.text = session.SyrupUnlocked
            ? "Syrup unlocked"
            : "Unlock syrup  ($" + CafeBalance.SyrupCost + ")";
        mechanicLabel.text = session.HasMechanic
            ? "Mechanic hired"
            : "Hire mechanic  ($" + CafeBalance.MechanicCost + ")";
        fasterLabel.text = UpgradeButton("Faster brew", "shorter timing, wider green", UpgradeType.FasterBrew);
        patienceLabel.text = UpgradeButton("Longer patience", "cats wait 23s", UpgradeType.LongerPatience);
        rentUpgradeLabel.text = UpgradeButton("Cheaper rent", "dog charges $20", UpgradeType.CheaperRent);

        for (int i = 0; i < pastryLabels.Length; i++)
            pastryLabels[i].text = "Croissant " + (i + 1) + ": " + session.PastryChoiceLabel(i);

        prepShotLabel.text = session.Prep == PrepType.Espresso ? "Slot: spare shot" : "Spare shot  (1 bean)";
        prepMilkLabel.text = session.Prep == PrepType.SteamedMilk ? "Slot: steamed milk" : "Steam milk  (1 milk)";

        dogRow.SetActive(session.Day == 3);
        payButton.gameObject.SetActive(session.Day == 3);
        payLabel.text = session.RentPaid
            ? "Rent paid"
            : "Pay the black dog  ($" + session.RentDue + ")";

        if (!session.CanOpen)
            openLabel.text = "Bake 3 croissants to open";
        else if (session.Day == 3 && !session.RentPaid)
            openLabel.text = "Open anyway";
        else
            openLabel.text = "Open cafe";
    }

    void RefreshShift()
    {
        stepsText.text = session.StepsLabel();
        stationStatus.text = session.Status;
        actionLabel.text = ActionLabel();

        float half = session.GreenHalfWidth;
        greenZone.anchorMin = new Vector2(0.5f - half, 0.12f);
        greenZone.anchorMax = new Vector2(0.5f + half, 0.88f);
        greenZone.offsetMin = Vector2.zero;
        greenZone.offsetMax = Vector2.zero;

        float markerX = session.BrewTiming ? session.Marker01 : 0.5f;
        marker.anchorMin = new Vector2(markerX, 0f);
        marker.anchorMax = new Vector2(markerX, 1f);
        marker.offsetMin = new Vector2(-6f, 0f);
        marker.offsetMax = new Vector2(6f, 0f);
        marker.gameObject.SetActive(session.BrewTiming);

        bool showRepair = session.MachineBroken;
        repairRoot.SetActive(showRepair);
        repairFill.fillAmount = Mathf.Clamp01(session.Repair01);
        repairCaption.text = session.HasMechanic ? "Mechanic repairing" : "Hold to repair";
    }

    void RefreshSlot(int index, bool shift)
    {
        SlotView view = slotViews[index];
        Customer cat = session.Slots[index];
        bool show = shift && cat.Occupied;
        view.Root.SetActive(show);
        if (!show)
            return;

        view.Bubble.text = session.OrderBubble(cat);
        if (cat.IsThief)
        {
            view.Name.text = "Click now";
            view.Name.color = new Color(0.98f, 0.95f, 0.88f);
            view.PatienceRoot.SetActive(false);
            view.Body.color = new Color(0.16f, 0.14f, 0.22f);
            view.EarLeft.color = new Color(0.10f, 0.09f, 0.14f);
            view.EarRight.color = view.EarLeft.color;
            return;
        }

        view.Name.text = cat.Accepted ? CafeSession.KindName(cat.Kind) : "Click to take";
        view.Name.color = new Color(0.24f, 0.14f, 0.10f);
        view.PatienceRoot.SetActive(cat.Accepted);
        if (cat.Accepted && cat.PatienceMax > 0f)
        {
            float ratio = Mathf.Clamp01(cat.Patience / cat.PatienceMax);
            view.PatienceFill.fillAmount = ratio;
            view.PatienceFill.color = Color.Lerp(new Color(0.75f, 0.22f, 0.16f), new Color(0.36f, 0.70f, 0.32f), ratio);
        }

        Color body = CatColor(cat.Kind);
        if (session.Focus == index)
            body = Color.Lerp(body, Color.white, 0.38f);
        view.Body.color = body;
        Color ear = Color.Lerp(body, Color.black, 0.25f);
        view.EarLeft.color = ear;
        view.EarRight.color = ear;
    }

    void RefreshClose()
    {
        string thiefLine = "";
        if (session.Day == 2 && session.ThiefAppeared)
        {
            thiefLine = session.ThiefCaught
                ? "\nYou caught the thief."
                : "\nThief took $" + session.ThiefLostThisShift;
        }

        closeBody.text = "Day " + session.Day + " closed\n\n"
            + "Earned $" + session.ShiftEarned + "\n"
            + "Tips $" + session.ShiftTips + "\n"
            + "Walkouts " + session.ShiftWalkouts
            + thiefLine + "\n\n"
            + "Beans " + session.Beans + "     Milk " + session.Milk + "\n"
            + session.PastryStockLabel() + "\n\n"
            + session.QuestLabel();
        closeButtonLabel.text = session.Day >= 3 ? "See the summary" : "Back to the shop";
    }

    string SummaryText()
    {
        string quest = session.QuestComplete ? "Cleared" : "Not cleared";
        string rent = session.RentPaid ? "Yes" : "No";
        return "Three days done.\n\n"
            + "Cash $" + session.Cash + "\n"
            + "Tips $" + session.RunTips + "\n"
            + "Walkouts " + session.RunWalkouts + "\n"
            + "Rent paid: " + rent + "\n"
            + "Quest: " + quest + "\n"
            + "Upgrade: " + session.UpgradeLabel();
    }

    string ActionLabel()
    {
        if (session.MachineBroken)
            return session.HasMechanic ? "Mechanic is repairing" : "Brewer broken";
        if (session.Focus < 0)
            return "Take an order";
        if (session.FocusReady)
            return "Click the cat to serve";
        if (!session.BrewLocked)
            return "Start drink";
        if (session.BrewTiming)
            return "Stop";
        if (session.CurrentStep == BrewStep.Beans)
            return "Add beans";
        if (session.CurrentStep == BrewStep.Milk)
            return "Steam milk";
        if (session.CurrentStep == BrewStep.Syrup)
            return "Add syrup";
        return "Start brew";
    }

    string UpgradeButton(string title, string detail, UpgradeType type)
    {
        if (session.Upgrade == type)
            return "Owned: " + title;
        if (session.Upgrade != UpgradeType.None)
            return title;
        return title + "  ($" + CafeBalance.UpgradeCost + ")\n" + detail;
    }

    void BuildBarista(RectTransform root)
    {
        Image body = Panel(root, "Barista", new Vector2(0.06f, 0.14f), new Vector2(0.16f, 0.32f), new Color(0.72f, 0.38f, 0.20f));
        body.raycastTarget = false;
        Image earL = Panel(body.rectTransform, "EarL", new Vector2(0.05f, 0.72f), new Vector2(0.38f, 1.18f), new Color(0.55f, 0.26f, 0.14f));
        Image earR = Panel(body.rectTransform, "EarR", new Vector2(0.62f, 0.72f), new Vector2(0.95f, 1.18f), new Color(0.55f, 0.26f, 0.14f));
        earL.raycastTarget = false;
        earR.raycastTarget = false;
        Text name = Label(body.rectTransform, "You", 20, TextAnchor.MiddleCenter, new Color(0.98f, 0.95f, 0.88f));
        Stretch(name.rectTransform, 0f, 0f, 0f, 0f);
        name.raycastTarget = false;
    }

    void BuildSlot(RectTransform root, int index, Vector2 min, Vector2 max)
    {
        int slot = index;
        Image body = Panel(root, "Cat" + index, min, max, CatColor(CustomerKind.Regular));
        Button button = body.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = body;
        button.onClick.AddListener(() => session.ClickSlot(slot));

        Image earL = Panel(body.rectTransform, "EarL", new Vector2(0.08f, 0.78f), new Vector2(0.34f, 1.08f), Color.black);
        Image earR = Panel(body.rectTransform, "EarR", new Vector2(0.66f, 0.78f), new Vector2(0.92f, 1.08f), Color.black);
        earL.raycastTarget = false;
        earR.raycastTarget = false;

        Image bubble = Panel(body.rectTransform, "Bubble", new Vector2(0.06f, 0.62f), new Vector2(0.94f, 0.96f), new Color(0.99f, 0.96f, 0.90f));
        bubble.raycastTarget = false;
        Text bubbleText = Label(bubble.rectTransform, "", 20, TextAnchor.MiddleCenter, new Color(0.24f, 0.14f, 0.10f));
        Stretch(bubbleText.rectTransform, 6f, 4f, 6f, 4f);
        bubbleText.raycastTarget = false;

        Image patienceBg = Panel(body.rectTransform, "Patience", new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.16f), new Color(0.20f, 0.12f, 0.10f));
        patienceBg.raycastTarget = false;
        Image patienceFill = Panel(patienceBg.rectTransform, "Fill", Vector2.zero, Vector2.one, new Color(0.36f, 0.70f, 0.32f));
        patienceFill.type = Image.Type.Filled;
        patienceFill.fillMethod = Image.FillMethod.Horizontal;
        patienceFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        patienceFill.raycastTarget = false;
        patienceFill.fillAmount = 1f;

        Text name = Label(body.rectTransform, "", 20, TextAnchor.MiddleCenter, new Color(0.98f, 0.95f, 0.88f));
        Place(name.rectTransform, new Vector2(0.06f, 0.20f), new Vector2(0.94f, 0.40f));
        name.raycastTarget = false;

        slotViews[index].Root = body.gameObject;
        slotViews[index].Body = body;
        slotViews[index].EarLeft = earL;
        slotViews[index].EarRight = earR;
        slotViews[index].Bubble = bubbleText;
        slotViews[index].Name = name;
        slotViews[index].PatienceRoot = patienceBg.gameObject;
        slotViews[index].PatienceFill = patienceFill;
    }

    void BuildShop(RectTransform root)
    {
        Image panel = Panel(root, "Shop", new Vector2(0.44f, 0.04f), new Vector2(0.98f, 0.91f), new Color(0.29f, 0.18f, 0.13f));
        shopRoot = panel.gameObject;
        RectTransform rows = Child(panel.rectTransform, "Rows");
        Stretch(rows, 0f, 0f, 0f, 0f);
        VerticalLayoutGroup layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 14, 14);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        shopHint = Info(rows, 96);
        beansLabel = ButtonLabel(MakeButton(rows, 40f, () => session.BuyBeans()));
        milkLabel = ButtonLabel(MakeButton(rows, 40f, () => session.BuyMilk()));
        syrupLabel = ButtonLabel(MakeButton(rows, 40f, () => session.BuySyrup()));
        mechanicLabel = ButtonLabel(MakeButton(rows, 40f, () => session.HireMechanic()));
        fasterLabel = ButtonLabel(MakeButton(rows, 48f, () => session.BuyUpgrade(UpgradeType.FasterBrew)));
        patienceLabel = ButtonLabel(MakeButton(rows, 48f, () => session.BuyUpgrade(UpgradeType.LongerPatience)));
        rentUpgradeLabel = ButtonLabel(MakeButton(rows, 48f, () => session.BuyUpgrade(UpgradeType.CheaperRent)));
        for (int i = 0; i < 3; i++)
        {
            int slot = i;
            pastryLabels[i] = ButtonLabel(MakeButton(rows, 36f, () => session.CyclePastry(slot)));
        }

        prepShotLabel = ButtonLabel(MakeButton(rows, 36f, () => session.PrepEspresso()));
        prepMilkLabel = ButtonLabel(MakeButton(rows, 36f, () => session.PrepMilk()));
        dogRow = BuildDogRow(rows);
        payButton = MakeButton(rows, 40f, () => session.PayRent());
        payLabel = ButtonLabel(payButton);
        openLabel = ButtonLabel(MakeButton(rows, 48f, () => session.OpenCafe()));
    }

    GameObject BuildDogRow(RectTransform parent)
    {
        RectTransform row = Child(parent, "Dog");
        LayoutElement element = row.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 64f;
        element.minHeight = 64f;
        Image swatch = Panel(row, "Swatch", new Vector2(0f, 0.12f), new Vector2(0f, 0.88f), new Color(0.08f, 0.08f, 0.08f));
        swatch.rectTransform.offsetMin = new Vector2(4f, 0f);
        swatch.rectTransform.offsetMax = new Vector2(68f, 0f);
        swatch.raycastTarget = false;
        Text caption = Label(row, "Black dog: pay rent, or the brewer starts broken. Cheaper rent applies if you buy it first.", 18, TextAnchor.MiddleLeft, new Color(0.98f, 0.95f, 0.88f));
        Place(caption.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f));
        caption.rectTransform.offsetMin = new Vector2(80f, 0f);
        caption.rectTransform.offsetMax = new Vector2(-8f, 0f);
        caption.raycastTarget = false;
        return row.gameObject;
    }

    void BuildStation(RectTransform root)
    {
        Image panel = Panel(root, "Station", new Vector2(0.44f, 0.04f), new Vector2(0.98f, 0.91f), new Color(0.29f, 0.18f, 0.13f));
        stationRoot = panel.gameObject;
        RectTransform rows = Child(panel.rectTransform, "Rows");
        Stretch(rows, 0f, 0f, 0f, 0f);
        VerticalLayoutGroup layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 18, 18);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        Text title = Info(rows, 36);
        title.text = "Brewer";
        title.alignment = TextAnchor.MiddleLeft;
        title.fontSize = 28;
        stepsText = Info(rows, 40);
        stepsText.alignment = TextAnchor.MiddleLeft;

        RectTransform track = Child(rows, "Track");
        LayoutElement trackLayout = track.gameObject.AddComponent<LayoutElement>();
        trackLayout.preferredHeight = 72f;
        trackLayout.minHeight = 72f;
        Image trackImage = track.gameObject.AddComponent<Image>();
        trackImage.sprite = white;
        trackImage.color = new Color(0.14f, 0.09f, 0.07f);
        greenZone = Panel(track, "Green", new Vector2(0.39f, 0.12f), new Vector2(0.61f, 0.88f), new Color(0.42f, 0.72f, 0.38f)).rectTransform;
        greenZone.GetComponent<Image>().raycastTarget = false;
        marker = Panel(track, "Marker", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Color(0.98f, 0.95f, 0.88f)).rectTransform;
        marker.offsetMin = new Vector2(-6f, 0f);
        marker.offsetMax = new Vector2(6f, 0f);
        marker.GetComponent<Image>().raycastTarget = false;

        actionLabel = ButtonLabel(MakeButton(rows, 64f, () => session.PressStation()));
        Button repairButton = MakeButton(rows, 56f, () => { });
        repairRoot = repairButton.gameObject;
        repairCaption = ButtonLabel(repairButton);
        repairCaption.text = "Hold to repair";
        repairHold = repairRoot.AddComponent<HoldButton>();
        repairFill = Panel(repairRoot.GetComponent<RectTransform>(), "Fill", Vector2.zero, Vector2.one, new Color(0.86f, 0.62f, 0.24f, 0.85f));
        repairFill.type = Image.Type.Filled;
        repairFill.fillMethod = Image.FillMethod.Horizontal;
        repairFill.raycastTarget = false;
        repairFill.fillAmount = 0f;
        repairFill.transform.SetSiblingIndex(1);
        repairCaption.transform.SetAsLastSibling();

        stationStatus = Info(rows, 120);
        stationStatus.alignment = TextAnchor.UpperLeft;
    }

    void BuildClose(RectTransform root)
    {
        Image dim = Panel(root, "Close", Vector2.zero, Vector2.one, new Color(0.10f, 0.06f, 0.04f, 0.72f));
        closeRoot = dim.gameObject;
        Image card = Panel(dim.rectTransform, "Card", new Vector2(0.28f, 0.18f), new Vector2(0.72f, 0.82f), new Color(0.36f, 0.22f, 0.16f));
        closeBody = Label(card.rectTransform, "", 26, TextAnchor.UpperLeft, new Color(0.98f, 0.95f, 0.88f));
        Place(closeBody.rectTransform, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.92f));
        closeBody.raycastTarget = false;
        closeButtonLabel = ButtonLabel(MakeAbsoluteButton(card.rectTransform, new Vector2(0.18f, 0.06f), new Vector2(0.82f, 0.18f), () => session.Continue()));
    }

    void BuildSummary(RectTransform root)
    {
        Image dim = Panel(root, "Summary", Vector2.zero, Vector2.one, new Color(0.10f, 0.06f, 0.04f, 0.78f));
        summaryRoot = dim.gameObject;
        Image card = Panel(dim.rectTransform, "Card", new Vector2(0.28f, 0.16f), new Vector2(0.72f, 0.84f), new Color(0.36f, 0.22f, 0.16f));
        Text title = Label(card.rectTransform, "Cafe Meiboa", 36, TextAnchor.MiddleCenter, new Color(0.98f, 0.95f, 0.88f));
        Place(title.rectTransform, new Vector2(0.08f, 0.84f), new Vector2(0.92f, 0.96f));
        title.raycastTarget = false;
        summaryBody = Label(card.rectTransform, "", 26, TextAnchor.UpperLeft, new Color(0.98f, 0.95f, 0.88f));
        Place(summaryBody.rectTransform, new Vector2(0.10f, 0.24f), new Vector2(0.90f, 0.82f));
        summaryBody.raycastTarget = false;
        Text again = ButtonLabel(MakeAbsoluteButton(card.rectTransform, new Vector2(0.18f, 0.06f), new Vector2(0.82f, 0.18f), () => session.Begin()));
        again.text = "Play again";
    }

    Text Info(RectTransform parent, float height)
    {
        RectTransform row = Child(parent, "Info");
        LayoutElement element = row.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        element.minHeight = height;
        Text text = Label(row, "", 20, TextAnchor.UpperLeft, new Color(0.98f, 0.95f, 0.88f));
        Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
        text.raycastTarget = false;
        return text;
    }

    Button MakeButton(RectTransform parent, float height, UnityAction onClick)
    {
        RectTransform row = Child(parent, "Button");
        LayoutElement element = row.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        element.minHeight = height;
        Image image = row.gameObject.AddComponent<Image>();
        image.sprite = white;
        image.color = Color.white;
        Button button = row.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.62f, 0.40f, 0.22f);
        colors.highlightedColor = new Color(0.74f, 0.50f, 0.28f);
        colors.pressedColor = new Color(0.46f, 0.28f, 0.16f);
        colors.disabledColor = new Color(0.40f, 0.32f, 0.26f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        button.onClick.AddListener(onClick);
        Text text = Label(row, "", 20, TextAnchor.MiddleCenter, new Color(0.98f, 0.95f, 0.88f));
        Stretch(text.rectTransform, 8f, 2f, 8f, 2f);
        text.raycastTarget = false;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 12;
        text.resizeTextMaxSize = 22;
        return button;
    }

    Button MakeAbsoluteButton(RectTransform parent, Vector2 min, Vector2 max, UnityAction onClick)
    {
        Image image = Panel(parent, "Button", min, max, new Color(0.62f, 0.40f, 0.22f));
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(onClick);
        Text text = Label(image.rectTransform, "", 24, TextAnchor.MiddleCenter, new Color(0.98f, 0.95f, 0.88f));
        Stretch(text.rectTransform, 8f, 4f, 8f, 4f);
        text.raycastTarget = false;
        return button;
    }

    static Text ButtonLabel(Button button) => button.GetComponentInChildren<Text>();

    static Color CatColor(CustomerKind kind)
    {
        if (kind == CustomerKind.Milk)
            return new Color(0.93f, 0.86f, 0.74f);
        if (kind == CustomerKind.Sweet)
            return new Color(0.90f, 0.48f, 0.62f);
        if (kind == CustomerKind.Thief)
            return new Color(0.16f, 0.14f, 0.22f);
        return new Color(0.86f, 0.52f, 0.22f);
    }

    static Image Panel(RectTransform parent, string name, Vector2 min, Vector2 max, Color color)
    {
        RectTransform rect = Child(parent, name);
        Place(rect, min, max);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = white;
        image.color = color;
        return image;
    }

    static Text Label(RectTransform parent, string value, int size, TextAnchor anchor, Color color)
    {
        RectTransform rect = Child(parent, "Text");
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = color;
        text.text = value;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    static RectTransform Child(RectTransform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static void Place(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    static void EnsureFontAndSprite()
    {
        if (white == null)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            white = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            white.hideFlags = HideFlags.HideAndDontSave;
        }

        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font == null)
            font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial", "Calibri" }, 32);
    }

    static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        GameObject system = new GameObject("EventSystem");
        system.AddComponent<EventSystem>();
        system.AddComponent<StandaloneInputModule>();
    }

    class SlotView
    {
        public GameObject Root;
        public Image Body;
        public Image EarLeft;
        public Image EarRight;
        public Text Bubble;
        public Text Name;
        public GameObject PatienceRoot;
        public Image PatienceFill;
    }
}
