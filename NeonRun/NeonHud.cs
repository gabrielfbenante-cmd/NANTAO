using System;
using UnityEngine;
using UnityEngine.UI;

namespace NeonRun
{
    /// <summary>Efeitos de tela só do jogador local: vinheta elétrica, linhas de velocidade e barra de energia.</summary>
    internal sealed class NeonHud : IDisposable
    {
        private readonly GameObject _root;
        private readonly RawImage _vignette;
        private readonly RawImage _streaks;
        private readonly GameObject _barRoot;
        private readonly RectTransform _barFill;
        private readonly Image _barFillImage;
        private float _streakTimer;

        private static Texture2D _vignetteTex, _streakTex;

        public NeonHud()
        {
            _root = new GameObject("NeonRunHud");
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            _vignette = FullScreen("Vignette", VignetteTex());
            _streaks = FullScreen("Streaks", StreakTex());

            _barRoot = new GameObject("EnergyBar", typeof(RectTransform), typeof(Image));
            _barRoot.transform.SetParent(_root.transform, false);
            var bg = (RectTransform)_barRoot.transform;
            bg.anchorMin = bg.anchorMax = new Vector2(0.5f, 0f);
            bg.pivot = new Vector2(0.5f, 0.5f);
            bg.sizeDelta = new Vector2(260, 7);
            bg.anchoredPosition = new Vector2(0, 150);
            _barRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(_barRoot.transform, false);
            _barFill = (RectTransform)fill.transform;
            _barFill.anchorMin = new Vector2(0f, 0f);
            _barFill.anchorMax = new Vector2(0f, 1f);
            _barFill.pivot = new Vector2(0f, 0.5f);
            _barFill.sizeDelta = new Vector2(260, 0);
            _barFillImage = fill.GetComponent<Image>();
        }

        private RawImage FullScreen(string name, Texture tex)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(_root.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            img.color = Color.clear;
            return img;
        }

        /// <param name="weight">0..1 corrida ligada.</param>
        /// <param name="move">0..1 quão rápido está se movendo.</param>
        /// <param name="energy">0..1 energia.</param>
        public void Tick(bool visible, float weight, float move, float energy, float dt)
        {
            bool showScreen = visible && NeonConfig.ScreenEffects.Value && weight > 0.001f;
            _vignette.enabled = showScreen;
            _streaks.enabled = showScreen;
            if (showScreen)
            {
                Color c = NeonConfig.Primary;
                float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 15f, 3f);
                c.a = 0.55f * weight * flicker;
                _vignette.color = c;

                Color s = Color.Lerp(NeonConfig.Primary, Color.white, 0.5f);
                s.a = 0.35f * weight * move;
                _streaks.color = s;
                _streakTimer -= dt;
                if (_streakTimer <= 0f)
                {
                    _streakTimer = 0.05f;
                    _streaks.rectTransform.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 360f));
                    float sc = UnityEngine.Random.Range(1.4f, 1.6f);
                    _streaks.rectTransform.localScale = new Vector3(sc, sc, 1f);
                }
            }

            bool showBar = visible && (weight > 0.001f || energy < 0.999f);
            _barRoot.SetActive(showBar);
            if (showBar)
            {
                _barFill.sizeDelta = new Vector2(260f * Mathf.Clamp01(energy), 0f);
                Color fc = weight > 0.001f ? NeonConfig.Primary : new Color(0.6f, 0.6f, 0.6f);
                if (energy < NeonConfig.MinEnergyToActivate.Value && weight <= 0.001f) fc = new Color(0.8f, 0.25f, 0.2f);
                _barFillImage.color = fc;
            }
        }

        private static Texture2D VignetteTex()
        {
            if (_vignetteTex != null) return _vignetteTex;
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "NeonRunVignette" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f;
                float dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1.25f, r));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return _vignetteTex = tex;
        }

        private static Texture2D StreakTex()
        {
            if (_streakTex != null) return _streakTex;
            const int n = 512;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "NeonRunStreaks" };
            var px = new Color32[n * n];
            var rng = new System.Random(42);
            const int rays = 90;
            var angle = new float[rays];
            var width = new float[rays];
            var start = new float[rays];
            for (int i = 0; i < rays; i++)
            {
                angle[i] = (float)(rng.NextDouble() * Math.PI * 2);
                width[i] = 0.004f + (float)rng.NextDouble() * 0.01f;
                start[i] = 0.55f + (float)rng.NextDouble() * 0.3f;
            }
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f;
                float dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = 0f;
                if (r > 0.45f)
                {
                    float ang = Mathf.Atan2(dy, dx);
                    for (int i = 0; i < rays; i++)
                    {
                        if (r < start[i]) continue;
                        float d = Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, angle[i] * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                        if (d < width[i])
                        {
                            float along = Mathf.InverseLerp(start[i], 1.1f, r);
                            a = Mathf.Max(a, (1f - d / width[i]) * along);
                        }
                    }
                }
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return _streakTex = tex;
        }

        public void Dispose()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
        }
    }
}
