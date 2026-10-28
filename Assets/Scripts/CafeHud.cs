using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
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
    [SerializeField] TextMeshProUGUI fasterLabel;
    [SerializeField] TextMeshProUGUI patienceLabel;
    [SerializeField] TextMeshProUGUI rentUpgradeLabel;
    [SerializeField] GameObject dogRow;
    [SerializeField] Button payButton;
    [SerializeField] TextMeshProUGUI payLabel;
    [SerializeField] TextMeshProUGUI openLabel;

    [SerializeField] Button beansButton;
    [SerializeField] Button milkButton;
    [SerializeField] Button fasterButton;
    [SerializeField] Button patienceButton;
    [SerializeField] Button rentUpgradeButton;
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

    public bool RepairHeld => repairHold != null && repairHold.Held;

    TextMeshProUGUI dogCaption;
    bool dogLifted;

    public void Bind(CafeSession cafeSession)
    {
        session = cafeSession;
        Wire(beansButton, session.BuyBeans);
        Wire(milkButton, session.BuyMilk);
        Wire(fasterButton, () => session.BuyUpgrade(UpgradeType.FasterBrew));
        Wire(patienceButton, () => session.BuyUpgrade(UpgradeType.LongerPatience));
        Wire(rentUpgradeButton, () => session.BuyUpgrade(UpgradeType.CheaperRent));

        Wire(payButton, session.PayRent);
        Wire(openButton, session.OpenCafe);
        LiftDogPrompt();
        Wire(actionButton, session.PressStation);
        Wire(closeButton, session.Continue);
        Wire(playAgainButton, () => SceneManager.LoadScene("Title"));
        PrepareStationForDrag();
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
            + "   " + (session.MachineBroken ? session.BrokenName + " BROKEN" : "Machines ok");

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

        RefreshDog();
    }

    void RefreshShop()
    {
        shopHint.text = session.Status + "\n" + session.StockAdvice();
        beansLabel.text = "Beans x4  ($" + CafeBalance.BeanPackCost + ")     have " + session.Beans;
        milkLabel.text = "Milk x4  ($" + CafeBalance.MilkPackCost + ")     have " + session.Milk;
        fasterLabel.text = session.UpgradeOffer(UpgradeType.FasterBrew);
        patienceLabel.text = session.UpgradeOffer(UpgradeType.LongerPatience);
        rentUpgradeLabel.text = session.UpgradeOffer(UpgradeType.CheaperRent);
        if (fasterButton != null)
            fasterButton.interactable = !session.UpgradeMaxed(UpgradeType.FasterBrew);
        if (patienceButton != null)
            patienceButton.interactable = !session.UpgradeMaxed(UpgradeType.LongerPatience);
        if (rentUpgradeButton != null)
            rentUpgradeButton.interactable = !session.UpgradeMaxed(UpgradeType.CheaperRent);

        openLabel.text = "Open cafe";
    }

    void RefreshDog()
    {
        bool show = session.Phase == DayPhase.Shift && session.DogPresent;
        if (dogRow != null)
            dogRow.SetActive(show);
        if (payButton != null)
            payButton.gameObject.SetActive(show);
        if (!show)
            return;

        int seconds = Mathf.CeilToInt(Mathf.Max(0f, session.DogTime));
        if (dogCaption != null)
            dogCaption.text = "Black dog. Pay or the " + session.BrokenName + " breaks.\n" + seconds + "s left.";
        if (payLabel != null)
            payLabel.text = "Pay the black dog  ($" + session.RentDue + ")";
    }

    void LiftDogPrompt()
    {
        if (dogLifted || dogRow == null || payButton == null || shopRoot == null)
            return;

        RectTransform parent = shopRoot.transform.parent as RectTransform;
        if (parent == null)
            return;

        dogCaption = dogRow.GetComponentInChildren<TextMeshProUGUI>(true);
        if (dogCaption != null)
            dogCaption.enableWordWrapping = true;
        PlacePrompt(dogRow.GetComponent<RectTransform>(), parent, 0.02f, 0.52f, 0.22f, 0.72f);
        PlacePrompt(payButton.GetComponent<RectTransform>(), parent, 0.02f, 0.40f, 0.22f, 0.51f);
        dogRow.SetActive(false);
        payButton.gameObject.SetActive(false);
        dogLifted = true;
    }

    static void PlacePrompt(RectTransform rect, RectTransform parent, float xMin, float yMin, float xMax, float yMax)
    {
        if (rect == null)
            return;

        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(xMin, yMin);
        rect.anchorMax = new Vector2(xMax, yMax);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.SetAsLastSibling();
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
        repairCaption.text = "Hold to repair";
    }

    void RefreshClose()
    {
        string thiefLine = "";
        if (session.Day == 2 && session.ThiefAppeared)
        {
            thiefLine = session.ThiefCaught
                ? "\nThe thief gave up."
                : "\nThief took $" + session.ThiefLostThisShift;
        }

        string dogLine = "";
        if (session.DogVisited)
        {
            dogLine = session.DogPaidThisShift
                ? "\nYou paid the dog."
                : "\nThe dog left unpaid.";
        }

        closeBody.text = "Day " + session.Day + " closed\n\n"
            + "Earned $" + session.ShiftEarned + "\n"
            + "Tips $" + session.ShiftTips + "\n"
            + "Walkouts " + session.ShiftWalkouts
            + thiefLine + dogLine + "\n\n"
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
        string rent = session.RentKept ? "Yes" : "No";
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
            return session.BrokenName + " broken";
        if (session.BrewTiming)
            return "Stop";
        if (session.FocusReady)
            return "Drag the cup to the cat";
        return "Build the drink";
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
