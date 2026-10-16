using UnityEngine;

public class GameManager : MonoBehaviour
{
    CafeSession session;
    CafeHud hud;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureRunning()
    {
        if (FindAnyObjectByType<GameManager>() != null)
            return;

        GameObject host = new GameObject("GameManager");
        host.AddComponent<GameManager>();
    }

    void Start()
    {
        session = new CafeSession();
        session.Begin();
        hud = gameObject.AddComponent<CafeHud>();
        hud.Build(session);

        if (Camera.main != null)
            Camera.main.backgroundColor = new Color(0.95f, 0.89f, 0.78f);
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
