using UnityEngine;

/*
 *  Attach to the Player GameObject. Hazards (spikes, pits, enemies) call Kill()
 *  on this component — e.g.  other.GetComponent<PlayerLife>()?.Kill();
 *
 *  Kill() is a no-op if the player is already dead, so it's safe to call
 *  from multiple hazards on the same frame.
 */
public class PlayerLife : MonoBehaviour
{
    public void Kill()
    {
        if (GameStateManager.Instance == null) return;
        if (GameStateManager.Instance.State != GameState.Playing) return;

        GameStateManager.Instance.Set(GameState.GameOver);
    }
}
