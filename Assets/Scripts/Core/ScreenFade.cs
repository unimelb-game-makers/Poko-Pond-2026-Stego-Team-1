using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScreenFade : MonoBehaviour
{
    private Image fadeImage;

    void Awake()
    {
        fadeImage = GetComponent<Image>();
    }

    // Call this from other scripts to start fading out to black
    // Then pick your niche lyrics here
    public void FadeToBlack(float duration)
    {
        StartCoroutine(FadeRoutine(1f, duration));
    }

    // Call this to clear the black screen
    public void FadeFromBlack(float duration)
    {
        Debug.Log("Called FadeFromBlack");
        StartCoroutine(FadeRoutine(0f, duration));
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        Color color = fadeImage.color;
        float startAlpha = color.a;
        float time = 0;

        while (time < duration)
        {
            time += Time.deltaTime;
            // Smoothly interpolate the alpha value
            color.a = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
            fadeImage.color = color;
            yield return null; // Wait for the next frame
        }

        // Snap safely to the final target alpha
        color.a = targetAlpha;
        fadeImage.color = color;
    }
}