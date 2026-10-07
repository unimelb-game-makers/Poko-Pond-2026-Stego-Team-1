using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace PokoPond.UI.MainMenu
{
    /// <summary>
    /// Drives a looping background VideoPlayer into a RawImage (via a RenderTexture).
    /// If no VideoClip is assigned, falls back to a static texture so the menu still renders.
    /// </summary>
    [DisallowMultipleComponent]
    public class VideoBackground : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private RawImage    target;
        [SerializeField] private RenderTexture renderTexture;
        [SerializeField] private Texture2D   fallbackTexture;

        [Header("Clip")]
        [SerializeField] private VideoClip clip;

        private void Reset()
        {
            videoPlayer = GetComponent<VideoPlayer>();
        }

        private void Awake()
        {
            if (videoPlayer == null) videoPlayer = GetComponent<VideoPlayer>();
            if (videoPlayer != null)
            {
                videoPlayer.isLooping         = true;
                videoPlayer.playOnAwake       = true;
                videoPlayer.waitForFirstFrame = true;
                videoPlayer.audioOutputMode   = VideoAudioOutputMode.None;
                videoPlayer.renderMode        = VideoRenderMode.RenderTexture;

                if (clip != null) videoPlayer.clip = clip;
                if (renderTexture != null) videoPlayer.targetTexture = renderTexture;
            }

            // Decide the RawImage visibility. If we have nothing to show, hide the
            // layer entirely so the solid background beneath is what the player sees
            // (otherwise an uninitialised RenderTexture shows up as magenta/pink).
            ConfigureTargetVisibility();

            if (renderTexture != null)
            {
                ClearRenderTexture(renderTexture);
            }
        }

        private void ConfigureTargetVisibility()
        {
            if (target == null) return;

            bool hasClip     = videoPlayer != null && videoPlayer.clip != null;
            bool hasFallback = fallbackTexture != null;

            if (hasClip && renderTexture != null)
            {
                target.texture = renderTexture;
                target.enabled = true;
            }
            else if (hasFallback)
            {
                target.texture = fallbackTexture;
                target.enabled = true;
            }
            else
            {
                target.texture = null;
                target.enabled = false;
            }
        }

        private static void ClearRenderTexture(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, new Color(0x1a / 255f, 0x14 / 255f, 0x26 / 255f, 1f));
            RenderTexture.active = prev;
        }

        private void OnEnable()
        {
            if (videoPlayer != null && videoPlayer.clip != null && !videoPlayer.isPlaying)
            {
                videoPlayer.Play();
            }
        }

        private void OnDisable()
        {
            if (videoPlayer != null && videoPlayer.isPlaying)
            {
                videoPlayer.Stop();
            }
        }
    }
}
