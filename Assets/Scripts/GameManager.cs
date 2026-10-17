using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] CafeHud hud;

    CafeSession session;

    void Awake()
    {
        if (hud == null)
            hud = GetComponent<CafeHud>();
    }

    void Start()
    {
        session = new CafeSession();
        session.Begin();
        hud.Bind(session);
    }

    void Update()
    {
        if (session.Phase == DayPhase.Shift)
        {
            session.SetRepairHeld(hud.RepairHeld);
            session.Tick(Time.deltaTime);
        }
        else
        {
            session.SetRepairHeld(false);
        }

        hud.Refresh();
    }
}
