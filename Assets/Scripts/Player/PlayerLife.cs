using UnityEngine;
using UnityEngine.SceneManagement;

/*
 *  Attach to the Player GameObject. Hazards (spikes, pits, enemies) call Kill()
 *  on this component — e.g.  other.GetComponent<PlayerLife>()?.Kill();
 *
 *  Kill() is a no-op if the player is already dead, so it's safe to call
 *  from multiple hazards on the same frame.
 *
 *  Requires a GameStateManager in the scene to show a Game Over screen.
 *  If none is present, falls back to reloading the current scene directly.
 */
public class PlayerLife : MonoBehaviour
{
    private bool _dead;

    public void Kill()
    {
        if (_dead) return;
        _dead = true;

        Debug.Log($"[PlayerLife] Kill called. GameStateManager present: {GameStateManager.Instance != null}");

        if (GameStateManager.Instance != null)
        {
            Debug.Log($"[PlayerLife] Current state: {GameStateManager.Instance.State}");
            if (GameStateManager.Instance.State != GameState.Playing) return;
            GameStateManager.Instance.Set(GameState.GameOver);
        }
        else
        {
            Debug.LogWarning("[PlayerLife] No GameStateManager in scene — reloading scene as fallback. Add a GameStateManager GameObject to the scene for a proper Game Over screen.");
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
