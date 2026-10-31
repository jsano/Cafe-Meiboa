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
    [SerializeField] TextMeshProUGUI timingLabel;
    [SerializeField] GameObject dogRow;
    [SerializeField] Button payButton;
    [SerializeField] TextMeshProUGUI payLabel;
    [SerializeField] TextMeshProUGUI openLabel;

    [SerializeField] Button beansButton;
    [SerializeField] Button milkButton;
    [SerializeField] Button fasterButton;
    [SerializeField] Button patienceButton;
    [SerializeField] Button rentUpgradeButton;
    [SerializeField] Button timingButton;
    [SerializeField] Button openButton;
    [SerializeField] Button actionButton;
    [SerializeField] Button closeButton;
    [SerializeField] Button playAgainButton;

    [SerializeField] GameObject stationRoot;
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
    bool timingLayoutSaved;
    Vector2 savedTrackMin;
    Vector2 savedTrackMax;
    Vector2 savedTrackPivot;
    Vector2 savedTrackPos;
    Vector2 savedTrackSize;
    Vector2 savedButtonMin;
    Vector2 savedButtonMax;
    Vector2 savedButtonPivot;
    Vector2 savedButtonPos;
    Vector2 savedButtonSize;
    Vector2 savedStatusMin;
    Vector2 savedStatusMax;
    Vector2 savedStatusPivot;
    Vector2 savedStatusPos;
    Vector2 savedStatusSize;
    int savedTrackSibling;
    int savedButtonSibling;
    const float VerticalTrackTop = 18f;
    const float VerticalTrackHeight = 200f;
    const float VerticalContentLeft = 108f;
    GameObject syrupTimerRoot;
    Image syrupTimerFill;

    public void Bind(CafeSession cafeSession)
    {
        session = cafeSession;
        Wire(beansButton, session.BuyBeans);
        Wire(milkButton, session.BuyMilk);
        Wire(fasterButton, () => session.BuyUpgrade(UpgradeType.FasterBrew));
        Wire(patienceButton, () => session.BuyUpgrade(UpgradeType.LongerPatience));
        Wire(rentUpgradeButton, () => session.BuyUpgrade(UpgradeType.CheaperRent));
        Wire(timingButton, () => session.BuyUpgrade(UpgradeType.SlowerMark));

        Wire(payButton, session.PayRent);
        Wire(openButton, session.OpenCafe);
        PlaceDogInStation();
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
            + (session.Endless ? "        Health " + session.Health : "")
            + "        $" + session.Cash
            + "        " + session.QuestLabel()
            + "        " + (session.MachineBroken ? session.BrokenName + " BROKEN" : "Machines ok");

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
                ? "The counter is closed.\nStock the right side, mentally prepare yourself, then open when you are ready."
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
        if (timingLabel != null)
            timingLabel.text = session.UpgradeOffer(UpgradeType.SlowerMark);
        if (fasterButton != null)
            fasterButton.interactable = !session.UpgradeMaxed(UpgradeType.FasterBrew);
        if (patienceButton != null)
            patienceButton.interactable = !session.UpgradeMaxed(UpgradeType.LongerPatience);
        if (rentUpgradeButton != null)
            rentUpgradeButton.interactable = !session.UpgradeMaxed(UpgradeType.CheaperRent);
        if (timingButton != null)
            timingButton.interactable = !session.UpgradeMaxed(UpgradeType.SlowerMark);

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

    void PlaceDogInStation()
    {
        if (dogLifted || dogRow == null || payButton == null || stationRoot == null)
            return;

        Transform rows = stationRoot.transform.Find("Rows");
        if (rows == null)
            rows = stationRoot.transform;

        dogCaption = dogRow.GetComponentInChildren<TextMeshProUGUI>(true);
        if (dogCaption != null)
            dogCaption.enableWordWrapping = true;

        RectTransform dogRect = dogRow.GetComponent<RectTransform>();
        RectTransform payRect = payButton.GetComponent<RectTransform>();
        dogRect.SetParent(rows, false);
        payRect.SetParent(rows, false);
        FitStationRow(dogRect, dogRow.GetComponent<LayoutElement>(), 64f);
        FitStationRow(payRect, payButton.GetComponent<LayoutElement>(), 44f);
        dogRect.SetAsLastSibling();
        payRect.SetAsLastSibling();
        dogRow.SetActive(false);
        payButton.gameObject.SetActive(false);
        dogLifted = true;
    }

    void LayoutDog(bool vertical)
    {
        if (!dogLifted || dogRow == null || payButton == null)
            return;

        RectTransform dogRect = dogRow.GetComponent<RectTransform>();
        RectTransform payRect = payButton.GetComponent<RectTransform>();
        LayoutElement dogLayout = dogRow.GetComponent<LayoutElement>();
        LayoutElement payLayout = payButton.GetComponent<LayoutElement>();
        if (!vertical)
        {
            FitStationRow(dogRect, dogLayout, 64f);
            FitStationRow(payRect, payLayout, 44f);
            dogRect.SetAsLastSibling();
            payRect.SetAsLastSibling();
            return;
        }

        float right = 16f;
        float dogHeight = 64f;
        float payHeight = 44f;
        float gap = 8f;
        float statusBottom = VerticalTrackTop + VerticalTrackHeight + 16f + savedStatusSize.y;
        float dogCenter = statusBottom + gap + dogHeight * 0.5f;
        float payCenter = dogCenter + dogHeight * 0.5f + gap + payHeight * 0.5f;
        PlaceBelowTiming(dogRect, dogLayout, dogCenter, dogHeight, right);
        PlaceBelowTiming(payRect, payLayout, payCenter, payHeight, right);
    }

    static void FitStationRow(RectTransform rect, LayoutElement layout, float height)
    {
        if (rect == null)
            return;
        if (layout != null)
        {
            layout.ignoreLayout = false;
            layout.minHeight = height;
            layout.preferredHeight = height;
        }

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(550f, height);
    }

    static void PlaceBelowTiming(RectTransform rect, LayoutElement layout, float centerFromTop, float height, float right)
    {
        if (rect == null)
            return;
        if (layout != null)
            layout.ignoreLayout = true;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(-(VerticalContentLeft + right), height);
        rect.anchoredPosition = new Vector2((VerticalContentLeft - right) * 0.5f, -centerFromTop);
        rect.SetAsLastSibling();
    }

    void RefreshShift()
    {
        stationStatus.text = session.Status;
        actionLabel.text = ActionLabel();

        RememberTimingLayout();
        bool vertical = session.BrewTiming && (session.VerticalTiming || session.SyrupTiming);
        LayoutTimingTrack(vertical);

        float half = session.GreenHalfWidth;
        float markerPos = session.BrewTiming ? session.Marker01 : 0.5f;
        if (vertical)
        {
            greenZone.anchorMin = new Vector2(0.12f, 0.5f - half);
            greenZone.anchorMax = new Vector2(0.88f, 0.5f + half);
            marker.anchorMin = new Vector2(0f, markerPos);
            marker.anchorMax = new Vector2(1f, markerPos);
            marker.offsetMin = new Vector2(0f, -6f);
            marker.offsetMax = new Vector2(0f, 6f);
        }
        else
        {
            greenZone.anchorMin = new Vector2(0.5f - half, 0.12f);
            greenZone.anchorMax = new Vector2(0.5f + half, 0.88f);
            marker.anchorMin = new Vector2(markerPos, 0f);
            marker.anchorMax = new Vector2(markerPos, 1f);
            marker.offsetMin = new Vector2(-6f, 0f);
            marker.offsetMax = new Vector2(6f, 0f);
        }

        greenZone.offsetMin = Vector2.zero;
        greenZone.offsetMax = Vector2.zero;
        marker.gameObject.SetActive(session.BrewTiming);
        ShowTimingTrack(session.BrewTiming);
        RefreshSyrupTimer();
        LayoutDog(vertical);

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
            + (session.Endless ? "\nHealth " + session.Health : "")
            + thiefLine + dogLine + "\n\n"
            + "Beans " + session.Beans + "     Milk " + session.Milk + "\n"
            + session.PastryStockLabel() + "\n\n"
            + session.QuestLabel();
        closeButtonLabel.text = session.Endless || session.Day < 3 ? "Back to the shop" : "See the summary";
    }

    void RememberTimingLayout()
    {
        if (timingLayoutSaved || greenZone == null)
            return;

        RectTransform track = greenZone.parent as RectTransform;
        if (track == null)
            return;

        savedTrackMin = track.anchorMin;
        savedTrackMax = track.anchorMax;
        savedTrackPivot = track.pivot;
        savedTrackPos = track.anchoredPosition;
        savedTrackSize = track.sizeDelta;

        if (actionButton != null)
        {
            RectTransform buttonRect = actionButton.GetComponent<RectTransform>();
            savedButtonMin = buttonRect.anchorMin;
            savedButtonMax = buttonRect.anchorMax;
            savedButtonPivot = buttonRect.pivot;
            savedButtonPos = buttonRect.anchoredPosition;
            savedButtonSize = buttonRect.sizeDelta;
        }

        if (stationStatus != null)
        {
            RectTransform statusRect = stationStatus.rectTransform;
            savedStatusMin = statusRect.anchorMin;
            savedStatusMax = statusRect.anchorMax;
            savedStatusPivot = statusRect.pivot;
            savedStatusPos = statusRect.anchoredPosition;
            savedStatusSize = statusRect.sizeDelta;
        }

        savedTrackSibling = track.GetSiblingIndex();
        if (actionButton != null)
            savedButtonSibling = actionButton.transform.GetSiblingIndex();

        timingLayoutSaved = true;
    }

    void LayoutTimingTrack(bool vertical)
    {
        if (!timingLayoutSaved)
            return;

        RectTransform track = greenZone.parent as RectTransform;
        if (track == null)
            return;

        RectTransform buttonRect = actionButton != null ? actionButton.GetComponent<RectTransform>() : null;
        LayoutElement trackLayout = track.GetComponent<LayoutElement>();
        LayoutElement buttonLayout = actionButton != null ? actionButton.GetComponent<LayoutElement>() : null;
        RectTransform statusRect = stationStatus != null ? stationStatus.rectTransform : null;
        LayoutElement statusLayout = stationStatus != null ? stationStatus.GetComponent<LayoutElement>() : null;
        if (vertical)
        {
            if (trackLayout != null)
                trackLayout.ignoreLayout = true;
            track.anchorMin = new Vector2(0f, 1f);
            track.anchorMax = new Vector2(0f, 1f);
            track.pivot = new Vector2(0f, 1f);
            track.anchoredPosition = new Vector2(16f, -VerticalTrackTop);
            track.sizeDelta = new Vector2(76f, VerticalTrackHeight);
            track.SetAsLastSibling();

            if (buttonLayout != null)
                buttonLayout.ignoreLayout = true;
            if (buttonRect != null)
            {
                float right = 16f;
                float buttonY = VerticalTrackTop + VerticalTrackHeight * 0.5f;
                buttonRect.anchorMin = new Vector2(0f, 1f);
                buttonRect.anchorMax = new Vector2(1f, 1f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.sizeDelta = new Vector2(-(VerticalContentLeft + right), savedButtonSize.y);
                buttonRect.anchoredPosition = new Vector2((VerticalContentLeft - right) * 0.5f, -buttonY);
                buttonRect.SetAsLastSibling();
            }

            if (statusLayout != null)
                statusLayout.ignoreLayout = true;
            if (statusRect != null)
            {
                float right = 16f;
                float statusY = VerticalTrackTop + VerticalTrackHeight + 16f + savedStatusSize.y * 0.5f;
                statusRect.anchorMin = new Vector2(0f, 1f);
                statusRect.anchorMax = new Vector2(1f, 1f);
                statusRect.pivot = new Vector2(0.5f, 0.5f);
                statusRect.sizeDelta = new Vector2(-(VerticalContentLeft + right), savedStatusSize.y);
                statusRect.anchoredPosition = new Vector2((VerticalContentLeft - right) * 0.5f, -statusY);
            }
            return;
        }

        if (trackLayout != null)
            trackLayout.ignoreLayout = false;

        track.anchorMin = savedTrackMin;
        track.anchorMax = savedTrackMax;
        track.pivot = savedTrackPivot;
        track.anchoredPosition = savedTrackPos;
        track.sizeDelta = savedTrackSize;
        track.SetSiblingIndex(savedTrackSibling);

        if (buttonLayout != null)
            buttonLayout.ignoreLayout = false;
        if (buttonRect != null)
        {
            buttonRect.anchorMin = savedButtonMin;
            buttonRect.anchorMax = savedButtonMax;
            buttonRect.pivot = savedButtonPivot;
            buttonRect.anchoredPosition = savedButtonPos;
            buttonRect.sizeDelta = savedButtonSize;
            buttonRect.SetSiblingIndex(savedButtonSibling);
        }

        if (statusLayout != null)
            statusLayout.ignoreLayout = false;
        if (statusRect != null)
        {
            statusRect.anchorMin = savedStatusMin;
            statusRect.anchorMax = savedStatusMax;
            statusRect.pivot = savedStatusPivot;
            statusRect.anchoredPosition = savedStatusPos;
            statusRect.sizeDelta = savedStatusSize;
        }
    }

    void ShowTimingTrack(bool show)
    {
        if (greenZone == null)
            return;

        Image zone = greenZone.GetComponent<Image>();
        if (zone != null)
            zone.enabled = show;

        Image trackImage = greenZone.parent.GetComponent<Image>();
        if (trackImage != null)
            trackImage.enabled = show;
    }

    void RefreshSyrupTimer()
    {
        EnsureSyrupTimer();
        if (syrupTimerRoot == null)
            return;

        bool show = session.SyrupTiming;
        syrupTimerRoot.SetActive(show);
        if (!show)
            return;

        float right = 16f;
        float barHeight = 16f;
        float buttonBottom = VerticalTrackTop + VerticalTrackHeight * 0.5f + savedButtonSize.y * 0.5f;
        RectTransform rect = syrupTimerRoot.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(-(VerticalContentLeft + right), barHeight);
        rect.anchoredPosition = new Vector2((VerticalContentLeft - right) * 0.5f, -(buttonBottom + 10f + barHeight * 0.5f));
        rect.SetAsLastSibling();

        float amount = Mathf.Clamp01(session.SyrupLeft / CafeBalance.SyrupSeconds);
        RectTransform fillRect = syrupTimerFill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(amount, 1f);
        fillRect.offsetMin = new Vector2(3f * amount, 3f);
        fillRect.offsetMax = new Vector2(-3f * amount, -3f);
    }

    void EnsureSyrupTimer()
    {
        if (syrupTimerRoot != null || actionButton == null || greenZone == null)
            return;

        Sprite sprite = greenZone.GetComponent<Image>().sprite;
        Transform parent = actionButton.transform.parent;

        syrupTimerRoot = new GameObject("SyrupTimer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        syrupTimerRoot.transform.SetParent(parent, false);
        Image backing = syrupTimerRoot.GetComponent<Image>();
        backing.sprite = sprite;
        backing.color = new Color(0.14f, 0.09f, 0.07f, 1f);
        backing.raycastTarget = false;
        syrupTimerRoot.GetComponent<LayoutElement>().ignoreLayout = true;

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObject.transform.SetParent(syrupTimerRoot.transform, false);
        syrupTimerFill = fillObject.GetComponent<Image>();
        syrupTimerFill.sprite = sprite;
        syrupTimerFill.type = Image.Type.Simple;
        syrupTimerFill.color = new Color(0.42f, 0.72f, 0.38f, 1f);
        syrupTimerFill.raycastTarget = false;
        RectTransform fillRect = syrupTimerFill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);
        syrupTimerRoot.SetActive(false);
    }

    void PrepareStationForDrag()
    {
        if (stationRoot == null)
            return;

        Graphic[] graphics = stationRoot.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            bool onButton = graphics[i].GetComponentInParent<Button>(true) != null;
            graphics[i].raycastTarget = onButton;
        }
    }

    string SummaryText()
    {
        if (session.Endless)
        {
            return "Health ran out on day " + session.Day + ".\n\n"
                + "Cash $" + session.Cash + "\n"
                + "Tips $" + session.RunTips + "\n"
                + "Walkouts " + session.RunWalkouts + "\n"
                + "Upgrade: " + session.UpgradeLabel();
        }

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
        if (session.SyrupTiming)
            return "Mix";
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
