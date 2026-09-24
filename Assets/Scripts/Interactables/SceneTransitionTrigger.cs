using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Invisible trigger prefab: any player body part that touches its box sends
// the player to the configured scene. Detects soft-body points the same way
// as SceneDoorExit/Humidifier, since the player has no single collider.
[RequireComponent(typeof(BoxCollider2D))]
public class SceneTransitionTrigger : MonoBehaviour
{
    [SerializeField] private string targetScene;
#if UNITY_EDITOR
    [SerializeField] private SceneAsset targetSceneAsset;
#endif

    private BoxCollider2D trigger;
    private bool loading;
    private bool reportedMissingScene;

    private void Awake()
    {
        trigger = GetComponent<BoxCollider2D>();
        trigger.isTrigger = true;
    }

    private void Update()
    {
        if (loading || Time.timeScale == 0f) return;
        if (GameStateManager.Instance != null && GameStateManager.Instance.State != GameState.Playing) return;

        Bounds b = trigger.bounds;
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(b.center, b.size, 0f,
                     LayerMask.GetMask("Player", "SoftBodyPoint")))
        {
            SoftBodyPlayer player = hit.GetComponent<SoftBodyPointRef>()?.owner;
            if (player == null) player = hit.GetComponentInParent<SoftBodyPlayer>();
            if (player == null) continue;

            if (string.IsNullOrWhiteSpace(targetScene) || !Application.CanStreamedLevelBeLoaded(targetScene))
            {
                if (!reportedMissingScene)
                    Debug.LogError($"[SceneTransitionTrigger] Destination '{targetScene}' is not enabled in the build scene list.", this);
                reportedMissingScene = true;
                return;
            }

            loading = true;
            SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single);
            return;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (targetSceneAsset != null) targetScene = targetSceneAsset.name;
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null) return;
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.offset, box.size);
    }
#endif
}
