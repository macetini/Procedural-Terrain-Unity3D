using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class TerrainFadeEffect : MonoBehaviour
{
    [Header("Fade Settings")]
    public float fadeDuration = 0.6f;

    private Renderer _renderer;
    private MaterialPropertyBlock _propBlock;
    private Coroutine _fadeCoroutine;
    private static readonly int AlphaID = Shader.PropertyToID("_Alpha");

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _propBlock = new MaterialPropertyBlock();

        // Start at 0 so it doesn't "snap" visible before the coroutine starts
        SetAlpha(0);
    }

    public void Play()
    {
        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        float elapsed = 0;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            SetAlpha(t);
            yield return null;
        }

        SetAlpha(1.0f);
        _fadeCoroutine = null;
    }

    private void SetAlpha(float alpha)
    {
        // We use GetPropertyBlock to preserve other properties (like Triplanar scales)
        _renderer.GetPropertyBlock(_propBlock);
        _propBlock.SetFloat(AlphaID, alpha);
        _renderer.SetPropertyBlock(_propBlock);
    }

    // Optional: Reset alpha if the chunk is pooled/reused
    public void ResetEffect()
    {
        SetAlpha(0);
    }
}
