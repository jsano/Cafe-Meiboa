using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CafeHud : MonoBehaviour
{
    CafeSession session;

    [SerializeField] TextMeshProUGUI hudText;
    [SerializeField] TextMeshProUGUI leftNote;
    [SerializeField] GameObject shopRoot;
    [SerializeField] TextMeshProUGUI shopHint;
    [SerializeField] TextMeshProUGUI beansLabel;
    [SerializeField] TextMeshProUGUI milkLabel;
    [SerializeField] TextMeshProUGUI syrupLabel;
    [SerializeField] TextMeshProUGUI mechanicLabel;
    [SerializeField] TextMeshProUGUI fasterLabel;
    [SerializeField] TextMeshProUGUI patienceLabel;
    [SerializeField] TextMeshProUGUI rentUpgradeLabel;
    [SerializeField] TextMeshProUGUI[] pastryLabels = new TextMeshProUGUI[3];
    [SerializeField] TextMeshProUGUI prepShotLabel;
    [SerializeField] TextMeshProUGUI prepMilkLabel;
    [SerializeField] GameObject dogRow;
    [SerializeField] Button payButton;
    [SerializeField] TextMeshProUGUI payLabel;
    [SerializeField] TextMeshProUGUI openLabel;

    [SerializeField] Button beansButton;
    [SerializeField] Button milkButton;
    [SerializeField] Button syrupButton;
    [SerializeField] Button mechanicButton;
    [SerializeField] Button fasterButton;
    [SerializeField] Button patienceButton;
    [SerializeField] Button rentUpgradeButton;
    [SerializeField] Button[] pastryButtons = new Button[3];
    [SerializeField] Button prepShotButton;
    [SerializeField] Button prepMilkButton;
    [SerializeField] Button openButton;
    [SerializeField] Button actionButton;
    [SerializeField] Button closeButton;
    [SerializeField] Button playAgainButton;

    [SerializeField] GameObject stationRoot;
    [SerializeField] TextMeshProUGUI stepsText;
    [SerializeField] RectTransform greenZone;
    [SerializeField] RectTransform marker;
    [SerializeField] TextMeshProUGUI actionLabel;
    [SerializeField] GameObject repairRoot;
    [SerializeField] Image repairFill;
    [SerializeField] TextMeshProUGUI repairCaption;
    [SerializeField] HoldButton repairHold;
    [SerializeField] TextMeshProUGUI stationStatus;

    [SerializeField] GameObject closeRoot;
    [SerializeField] TextMeshProUGUI closeBody;
    [SerializeField] TextMeshProUGUI closeButtonLabel;
    [SerializeField] GameObject summaryRoot;
    [SerializeField] TextMeshProUGUI summaryBody;

    [SerializeField] SlotView[] slotViews = { new SlotView(), new SlotView() };

    public bool RepairHeld => repairHold != null && repairHold.Held;

    readonly List<RaycastResult> slotHits = new List<RaycastResult>();

    public int SlotUnderPointer()
    {
        if (EventSystem.current == null)
            return -1;

        PointerEventData data = new PointerEventData(EventSystem.current);
        if (Mouse.current != null)
            data.position = Mouse.current.position.ReadValue();
        else
            data.position = Input.mousePosition;

        slotHits.Clear();
        EventSystem.current.RaycastAll(data, slotHits);
        for (int h = 0; h < slotHits.Count; h++)
        {
            GameObject hit = slotHits[h].gameObject;
            if (hit == null)
                continue;

            for (int i = 0; i < slotViews.Length; i++)
            {
                GameObject root = slotViews[i].Root;
                if (root == null)
                    continue;
                if (hit == root || hit.transform.IsChildOf(root.transform))
                    return i;
            }
        }

        return -1;
    }

    public void Bind(CafeSession cafeSession)
    {
        session = cafeSession;
        Wire(beansButton, session.BuyBeans);
        Wire(milkButton, session.BuyMilk);
        Wire(syrupButton, session.BuySyrup);
        Wire(mechanicButton, session.HireMechanic);
        Wire(fasterButton, () => session.BuyUpgrade(UpgradeType.FasterBrew));
        Wire(patienceButton, () => session.BuyUpgrade(UpgradeType.LongerPatience));
        Wire(rentUpgradeButton, () => session.BuyUpgrade(UpgradeType.CheaperRent));
        for (int i = 0; i < pastryButtons.Length; i++)
        {
            int slot = i;
            Wire(pastryButtons[i], () => session.CyclePastry(slot));
        }

        Wire(prepShotButton, session.PrepEspresso);
        Wire(prepMilkButton, session.PrepMilk);
        Wire(payButton, session.PayRent);
        Wire(openButton, session.OpenCafe);
        Wire(actionButton, session.PressStation);
        Wire(closeButton, session.Continue);
        Wire(playAgainButton, session.Begin);
        PrepareStationForDrag();
        for (int i = 0; i < slotViews.Length; i++)
        {
            int slot = i;
            Button button = slotViews[i].Root != null ? slotViews[i].Root.GetComponent<Button>() : null;
            Wire(button, () => session.ClickSlot(slot));
            if (slotViews[i].Name != null)
            {
                slotViews[i].Name.enableWordWrapping = true;
                slotViews[i].Name.overflowMode = TextOverflowModes.Overflow;
            }
        }

        Refresh();
    }

    static void Wire(Button button, UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
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
            + "   $" + session.Cash
            + "   Beans " + session.Beans
            + "   Coffee " + session.CoffeeUses
            + "   Milk " + session.Milk
            + "   Pastry " + session.PastryCount
            + "   " + session.QuestLabel()
            + "   Prep " + session.PrepLabel()
            + "   " + (session.MachineBroken ? "Brewer BROKEN" : "Brewer ok");

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
                ? "The counter is closed.\nStock the right side, then open when you are ready."
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

        if (session.Day == 3 && !session.RentPaid)
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

        if (actionButton != null)
            actionButton.gameObject.SetActive(session.BrewTiming);

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

        if (!cat.Accepted)
            view.Name.text = "Click to take";
        else if (cat.WantedPastry != PastryType.None)
            view.Name.text = "Drag croissant here";
        else if (session.Focus == index && cat.DrinkReady)
            view.Name.text = "Drag cup here";
        else
            view.Name.text = DrinkRecipes.Get(cat.Drink).Steps;
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

    void PrepareStationForDrag()
    {
        if (stationRoot == null)
            return;

        RectTransform rect = stationRoot.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.62f, 0.5f);
        rect.anchorMax = new Vector2(0.99f, 0.92f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Graphic[] graphics = stationRoot.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            bool onButton = graphics[i].GetComponentInParent<Button>(true) != null;
            graphics[i].raycastTarget = onButton;
        }
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
        if (session.BrewTiming)
            return "Stop";
        if (session.FocusReady)
            return "Drag the cup to the cat";
        return "Build the drink";
    }

    string UpgradeButton(string title, string detail, UpgradeType type)
    {
        if (session.Upgrade == type)
            return "Owned: " + title;
        if (session.Upgrade != UpgradeType.None)
            return title;
        return title + "  ($" + CafeBalance.UpgradeCost + ")\n" + detail;
    }

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

    [Serializable]
    class SlotView
    {
        public GameObject Root;
        public Image Body;
        public Image EarLeft;
        public Image EarRight;
        public TextMeshProUGUI Bubble;
        public TextMeshProUGUI Name;
        public GameObject PatienceRoot;
        public Image PatienceFill;
    }
}
