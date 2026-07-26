using System.Collections;
using UnityEngine;

namespace ProceduralTerrain.Effects
{
    [RequireComponent(typeof(Renderer))]
    public class TerrainFadeEffect : MonoBehaviour
    {
        [Header("Fade Settings")]
        public float fadeDuration = 0.6f;

        private Renderer localRenderer;
        private MaterialPropertyBlock propBlock;
        private Coroutine fadeCoroutine;
        private static readonly int AlphaID = Shader.PropertyToID("_Alpha");

        private void Awake()
        {
            localRenderer = GetComponent<Renderer>();
            propBlock = new MaterialPropertyBlock();

            // Start at 0 so it doesn't "snap" visible before the coroutine starts
            SetAlpha(0);
        }

        public void Play()
        {
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            fadeCoroutine = StartCoroutine(FadeRoutine());
        }

        private IEnumerator FadeRoutine()
        {
            float elapsed = 0;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / fadeDuration);
                SetAlpha(t);
                yield return null;
            }

            SetAlpha(1.0f);
            fadeCoroutine = null;
        }

        private void SetAlpha(float alpha)
        {
            propBlock.SetFloat(AlphaID, alpha);
            localRenderer.SetPropertyBlock(propBlock);
        }

        // Reset for pool reuse — must stop coroutine before GO deactivates
        public void ResetEffect()
        {
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }
            SetAlpha(0);
        }
    }
}
