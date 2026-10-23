using UnityEngine;
using UnityEngine.SceneManagement;

/*
 *  Attach to a Canvas in the gameplay scene. Assign `panel` to the Game Over
 *  root GameObject (hidden by default). Wire the Restart button's OnClick to Restart().
 *
 *  The Canvas itself must stay active so this script can react to OnStateChanged.
 *  Only the `panel` child is toggled.
 */
public class GameOverUI : MonoBehaviour
{
    [Tooltip("Root GameObject of the Game Over screen. Should be inactive by default.")]
    [SerializeField] private GameObject panel;

    private void OnEnable()  => GameStateManager.OnStateChanged += HandleStateChanged;
    private void OnDisable() => GameStateManager.OnStateChanged -= HandleStateChanged;

    private ScreenFade screenFade;

    private void Start()
    {
        if (panel != null) panel.SetActive(false);
        screenFade = panel.GetComponent<ScreenFade>();
    }

    private void HandleStateChanged(GameState state)
    {
        if (panel != null) panel.SetActive(state == GameState.GameOver);
        screenFade.FadeToBlack(1.5f);
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void MainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("StartMenu");
    }
}
