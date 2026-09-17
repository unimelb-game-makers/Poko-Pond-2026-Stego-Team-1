using UnityEngine;
using UnityEngine.SceneManagement;

// Added by PropTilemapSpawner only to doors with an Exit Scene override.
// Check the body's centre beyond the door, so a stray soft-body point cannot
// finish a room while the player is still on the approach side.
[RequireComponent(typeof(Door))]
public class SceneDoorExit : MonoBehaviour
{
    [SerializeField] private string nextScene;
    [SerializeField] private bool exitToLeft;
    private Door door;
    private bool loading;
    private bool reportedMissingScene;

    public string NextScene => nextScene;
    public bool ExitToLeft => exitToLeft;
    public void Configure(string sceneName, bool toLeft = false)
    {
        nextScene = sceneName;
        exitToLeft = toLeft;
    }

    private void Awake() => door = GetComponent<Door>();

    private void LateUpdate()
    {
        if (loading || !door.IsUnlocked || !door.IsOpen || Time.timeScale == 0f) return;
        if (GameStateManager.Instance != null && GameStateManager.Instance.State != GameState.Playing) return;

        Vector2 centre = (Vector2)transform.position + new Vector2(exitToLeft ? -1f : 1f, 1.5f);
        Vector2 size = new Vector2(1.2f, 3f);
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(centre, size, 0f,
                     LayerMask.GetMask("Player", "SoftBodyPoint")))
        {
            SoftBodyPlayer player = hit.GetComponent<SoftBodyPointRef>()?.owner;
            if (player == null) player = hit.GetComponentInParent<SoftBodyPlayer>();
            if (player == null || !player.InputEnabled) continue;
            Vector2 delta = player.Center - centre;
            if (Mathf.Abs(delta.x) > size.x * 0.5f || Mathf.Abs(delta.y) > size.y * 0.5f) continue;

            if (string.IsNullOrWhiteSpace(nextScene) || !Application.CanStreamedLevelBeLoaded(nextScene))
            {
                if (!reportedMissingScene)
                    Debug.LogError($"[SceneDoorExit] Destination '{nextScene}' is not enabled in the build scene list.", this);
                reportedMissingScene = true;
                return;
            }

            loading = true;
            // Each destination owns a fresh player: room entry resets to liquid,
            // as specified by the level design's humidifier transition.
            SceneManager.LoadSceneAsync(nextScene, LoadSceneMode.Single);
            return;
        }
    }
}
