using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] CafeHud hud;
    [SerializeField] CafeBoard board;

    CafeSession session;

    void Start()
    {
        session = new CafeSession();
        session.Begin();
        hud.Bind(session);
        if (board != null)
            board.Attach(session);
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
