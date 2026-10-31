using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CafeCat : MonoBehaviour
{
    public int Slot;

    SpriteRenderer body;
    TextMeshProUGUI bubble;
    TextMeshProUGUI detail;
    GameObject patienceRoot;
    Image patienceFill;

    void Awake()
    {
        body = GetComponent<SpriteRenderer>();
        if (body != null)
            body.sortingOrder = 8;

        Transform canvas = transform.Find("Canvas");
        if (canvas != null)
        {
            Canvas canvasUi = canvas.GetComponent<Canvas>();
            if (canvasUi != null)
            {
                canvasUi.overrideSorting = true;
                canvasUi.sortingOrder = 9;
                if (canvasUi.worldCamera == null && Camera.main != null)
                    canvasUi.worldCamera = Camera.main;
            }

            bubble = Label(canvas.Find("Bubble/Text"));
            detail = Label(canvas.Find("Text"));
            Transform patience = canvas.Find("Patience");
            if (patience != null)
            {
                patienceRoot = patience.gameObject;
                Transform fill = patience.Find("Fill");
                if (fill != null)
                    patienceFill = fill.GetComponent<Image>();
            }
        }
    }

    public void Show(CafeSession session, int index, bool shift)
    {
        Customer cat = session.Slots[index];
        bool visible = shift && cat.Occupied;
        if (!visible)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        if (bubble != null)
            bubble.text = session.OrderBubble(cat);

        if (cat.IsThief)
        {
            SetDetail("Hold", new Color(0.98f, 0.95f, 0.88f));
            SetPatience(true, Mathf.Clamp01(cat.ThiefHold / CafeBalance.ThiefHoldSeconds));
            Tint(new Color(0.16f, 0.14f, 0.22f));
            return;
        }

        if (!cat.Accepted)
            SetDetail("Click to take", new Color(0.24f, 0.14f, 0.10f));
        else if (cat.WantedPastry != PastryType.None)
            SetDetail("Drag croissant here", new Color(0.24f, 0.14f, 0.10f));
        else if (session.Focus == index && cat.DrinkReady)
            SetDetail("Drag cup here", new Color(0.24f, 0.14f, 0.10f));
        else
            SetDetail(cat.OrderRecipe.Steps, new Color(0.24f, 0.14f, 0.10f));

        bool waiting = cat.Accepted && cat.PatienceMax > 0f;
        SetPatience(waiting, waiting ? Mathf.Clamp01(cat.Patience / cat.PatienceMax) : 0f);

        Color bodyColor = KindColor(cat.Kind);
        if (session.Focus == index)
            bodyColor = Color.Lerp(bodyColor, Color.white, 0.38f);
        Tint(bodyColor);
    }

    void SetDetail(string text, Color color)
    {
        if (detail == null)
            return;
        detail.text = text;
        detail.color = color;
    }

    void SetPatience(bool show, float amount)
    {
        if (patienceRoot != null)
            patienceRoot.SetActive(show);
        if (!show || patienceFill == null)
            return;
        patienceFill.fillAmount = amount;
        patienceFill.color = Color.Lerp(new Color(0.75f, 0.22f, 0.16f), new Color(0.36f, 0.70f, 0.32f), amount);
    }

    void Tint(Color color)
    {
        if (body != null)
            body.color = color;
    }

    static TextMeshProUGUI Label(Transform child)
    {
        return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    static Color KindColor(CustomerKind kind)
    {
        if (kind == CustomerKind.Milk)
            return new Color(0.93f, 0.86f, 0.74f);
        if (kind == CustomerKind.Sweet)
            return new Color(0.90f, 0.48f, 0.62f);
        if (kind == CustomerKind.Thief)
            return new Color(0.16f, 0.14f, 0.22f);
        return new Color(0.86f, 0.52f, 0.22f);
    }
}
