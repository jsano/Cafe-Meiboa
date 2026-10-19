using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleMenu : MonoBehaviour
{
    [SerializeField] Button newGameButton;
    [SerializeField] Button quitButton;

    void Awake()
    {
        if (newGameButton != null)
            newGameButton.onClick.AddListener(NewGame);
        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);
    }

    public void NewGame()
    {
        SceneManager.LoadScene("SampleScene");
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
