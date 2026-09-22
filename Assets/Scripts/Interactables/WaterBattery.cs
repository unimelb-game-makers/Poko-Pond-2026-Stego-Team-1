using System.Collections.Generic;
using UnityEngine;

public class WaterBattery : MonoBehaviour
{
    [Header("Player")] public List<GameObject> Players;
    public LayerMask PlayerSoftBodyLayer;

    [Tooltip("SpriteRenderer used for the continuously looping fan animation.")]
    [SerializeField] private SpriteRenderer fanRenderer;

    [Tooltip("sprites in their left-to-right source-sheet order, for each state.")]
    [SerializeField] private Sprite[] animationFramesVacuum;
    [SerializeField] private Sprite[] animationFramesProcess;
    [SerializeField] private Sprite[] animationFramesIdle;
    [SerializeField] private Sprite[] animationFramesEject;


    [Tooltip("Playback speed for the always-on fan animation.")]
    [SerializeField, Min(0.1f)] private float animationFramesPerSecond = 8f;

    private int _animationFrame;
    private float _animationTimer;
    private int _animationState;
    private Sprite[] animationFrames;

    void Start()
    {
        animationFrames = animationFramesIdle;
        _animationState = 0;
    }

    // Update is called once per frame
    void Update()
    {
        _animationTimer += Time.deltaTime * animationFramesPerSecond;
        if (_animationTimer < 1f)
            return;

        int elapsedFrames = Mathf.FloorToInt(_animationTimer);
        _animationTimer -= elapsedFrames;
        _animationFrame = (_animationFrame + elapsedFrames) % animationFrames.Length;

        if (_animationState == 2 && _animationFrame >= 9)
        {
            _animationFrame = 7; // Quick hack for looping at the end
        }
        if (_animationState == 3 && _animationFrame >= 3)
        {
            _animationState = 0; // Quick hack for one-shot animation
        }

        SetAnimationFrame(_animationFrame, animationFrames);

        if (_animationState == 0)
        {
            animationFrames = animationFramesIdle;
        }
        else if(_animationState == 1)
        {
            animationFrames = animationFramesVacuum;
        }
        if (_animationState == 2)
        {
            animationFrames = animationFramesProcess;
        }
        else if (_animationState == 3)
        {
            animationFrames = animationFramesEject;
        }

    }

    void FixedUpdate()
    {
        foreach (var player in Players)
        {
            // Expensive state checking, only do in the few frames it's needed
            if (_animationState != 0)
            {
                if (!player.GetComponent<SoftBodyPlayer>().getVacuumState())
                {
                    if (player.GetComponent<Rigidbody2D>().linearVelocity.x > 1.0f)
                    {
                        _animationState = 3;
                        Debug.Log("STOP BATTERY");
                    }
                }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision) {
        if ((1 << collision.gameObject.layer) == PlayerSoftBodyLayer.value)
        {
            SoftBodyPlayer player = null;

            // Check for direct SoftBodyPlayer component
            if (collision.TryGetComponent<SoftBodyPlayer>(out var softBodyPlayer)) {
                player = softBodyPlayer;
            }
            // Check for SoftBodyPointRef to find owner
            else if (collision.TryGetComponent<SoftBodyPointRef>(out var softBodyPointRef)) {
                var ownerObj = softBodyPointRef.owner.transform.gameObject;
                if (ownerObj.TryGetComponent<SoftBodyPlayer>(out var ownerPlayer)) {
                    player = ownerPlayer;
                }
            }

            if (player != null) {
                // Avoid duplicate entries if already tracked
                if (!Players.Contains(player.gameObject)) {
                    Players.Add(player.gameObject);
                    player.applyVaccum(true, transform.position);
                    if(_animationState < 1) _animationState = 1;
                }
            }
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if ((1 << collision.gameObject.layer) == PlayerSoftBodyLayer.value)
        {
            if (Players.Count > 0)
            {
                if (Mathf.Abs(Players[0].transform.position.x-transform.position.x) <= 0.2f)
                {
                    if(_animationState < 2) _animationState = 2;
                } else if (_animationState >= 2)
                {
                    _animationState = 3;
                }
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if ((1 << collision.gameObject.layer) == PlayerSoftBodyLayer.value)
        {
            SoftBodyPlayer player = null;

            // Check for direct SoftBodyPlayer component
            if (collision.TryGetComponent<SoftBodyPlayer>(out var softBodyPlayer)) {
                player = softBodyPlayer;
            }
            // Check for SoftBodyPointRef to find owner
            else if (collision.TryGetComponent<SoftBodyPointRef>(out var softBodyPointRef)) {
                var ownerObj = softBodyPointRef.owner.transform.gameObject;
                if (ownerObj.TryGetComponent<SoftBodyPlayer>(out var ownerPlayer)) {
                    player = ownerPlayer;
                }
            }

            if (player != null) {
                // Remove from list and stop vacuum effect
                Players.Remove(player.gameObject);
                player.applyVaccum(false, Vector2.zero);
                _animationState = 3;
            }
        }
    }

    private void SetAnimationFrame(int index, Sprite[] animationFrames)
    {
        if (fanRenderer == null || animationFrames == null || index < 0 || index >= animationFrames.Length)
            return;

        if (animationFrames[index] != null)
            fanRenderer.sprite = animationFrames[index];
    }


}

