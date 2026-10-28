using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleMenu : MonoBehaviour
{
    [SerializeField] Button aboutButton;
    [SerializeField] GameObject aboutPanel;
    [SerializeField] Button backButton;
    [SerializeField] Button casualButton;
    [SerializeField] Button endlessButton;
    [SerializeField] Button quitButton;

    void Awake()
    {
        if (aboutButton != null)
            aboutButton.onClick.AddListener(() => AboutVisible(true));
        if (backButton != null)
            backButton.onClick.AddListener(() => AboutVisible(false));
        if (casualButton != null)
            casualButton.onClick.AddListener(NewGame);
        //if (endlessButton != null)
          //  endlessButton.onClick.AddListener(StartEndless); 
        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);
    }

    public void AboutVisible(bool visible)
    {
        aboutPanel.SetActive(visible);
    }

    public void NewGame()
    {
        SceneManager.LoadScene("Game");
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
