using System;
using UnityEngine;
using UnityEngine.UI;

namespace Nantao
{
    /// <summary>
    /// HUD do jogador local: nome do personagem, status da habilidade, barra, avisos e efeitos de tela.
    /// A cada frame a habilidade "pede" o que mostrar (BeginFrame → chamadas → EndFrame).
    /// </summary>
    internal sealed class AgentHud : IDisposable
    {
        private readonly GameObject _root;
        private readonly RawImage _vignette, _streaks, _smokeFill;
        private readonly Text _title, _status, _toast;
        private readonly GameObject _barRoot;
        private readonly RectTransform _barFill;
        private readonly Image _barFillImage;
        private float _streakTimer;
        private float _toastUntil;

        // Pedidos do frame
        private Color _vigColor; private float _vigAlpha;
        private Color _strColor; private float _strAlpha;
        private string _statusText; private Color _statusColor;
        private bool _barOn; private float _barValue; private Color _barColor;

        private static Texture2D _vignetteTex, _streakTex, _whiteTex;
        private static Font _font;

        public AgentHud()
        {
            _root = new GameObject("NantaoHud");
            UnityEngine.Object.DontDestroyOnLoad(_root);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            _smokeFill = FullScreen("Smoke", WhiteTex());
            _vignette = FullScreen("Vignette", VignetteTex());
            _streaks = FullScreen("Streaks", StreakTex());

            _title = MakeText("Title", new Vector2(0, 186), 26, FontStyle.Bold);
            _status = MakeText("Status", new Vector2(0, 128), 20, FontStyle.Normal);
            _toast = MakeText("Toast", new Vector2(0, 260), 24, FontStyle.Bold);

            _barRoot = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            _barRoot.transform.SetParent(_root.transform, false);
            var bg = (RectTransform)_barRoot.transform;
            bg.anchorMin = bg.anchorMax = new Vector2(0.5f, 0f);
            bg.sizeDelta = new Vector2(260, 7);
            bg.anchoredPosition = new Vector2(0, 156);
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

        // ---------------------------------------------------------------- API do frame

        public void BeginFrame()
        {
            _vigAlpha = 0f;
            _strAlpha = 0f;
            _statusText = null;
            _barOn = false;
        }

        public void Vignette(Color c, float alpha) { _vigColor = c; _vigAlpha = Mathf.Max(_vigAlpha, alpha); }
        public void Streaks(Color c, float alpha) { _strColor = c; _strAlpha = alpha; }
        public void Status(string text, Color c) { _statusText = text; _statusColor = c; }
        public void Bar(float value, Color c) { _barOn = true; _barValue = Mathf.Clamp01(value); _barColor = c; }

        public void Toast(string text, Color c, float seconds = 2.2f)
        {
            _toast.text = text;
            _toast.color = c;
            _toastUntil = Time.time + seconds;
        }

        public void EndFrame(bool visible, PlayerAgent agent, float dt)
        {
            var p = agent.Player;
            bool alive = !p.isPlayerDead;
            bool fx = visible && ModConfig.ScreenEffects.Value;

            // Dentro da fumaça: tela fica cinza (garante que bloqueia a visão).
            float inside = alive && p.gameplayCamera != null ? SmokeCloud.Inside(p.gameplayCamera.transform.position) : 0f;
            _smokeFill.enabled = visible && inside > 0.01f;
            if (_smokeFill.enabled)
            {
                Color c = ModConfig.Smoke;
                c.a = 0.93f * inside;
                _smokeFill.color = c;
            }

            _vignette.enabled = fx && alive && _vigAlpha > 0.001f;
            if (_vignette.enabled) { var c = _vigColor; c.a = _vigAlpha; _vignette.color = c; }

            _streaks.enabled = fx && alive && _strAlpha > 0.001f;
            if (_streaks.enabled)
            {
                var c = _strColor; c.a = _strAlpha; _streaks.color = c;
                _streakTimer -= dt;
                if (_streakTimer <= 0f)
                {
                    _streakTimer = 0.05f;
                    _streaks.rectTransform.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 360f));
                    float sc = UnityEngine.Random.Range(1.4f, 1.6f);
                    _streaks.rectTransform.localScale = new Vector3(sc, sc, 1f);
                }
            }

            var def = Characters.Get(agent.CharacterId);
            _title.enabled = visible && alive;
            if (def != null)
            {
                _title.text = def.Name.ToUpperInvariant();
                _title.color = Color.Lerp(def.Color(), Color.white, 0.25f);
            }
            else
            {
                _title.text = agent.CanSwapNow ? $"Sem personagem · [{KeyName(ModConfig.MenuKey.Value)}] escolher" : "";
                _title.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
            }

            _status.enabled = visible && alive && _statusText != null;
            if (_status.enabled)
            {
                _status.text = _statusText.Replace("[X]", $"[{KeyName(ModConfig.AbilityKey.Value)}]")
                                          .Replace("segure X", $"segure {KeyName(ModConfig.AbilityKey.Value)}");
                _status.color = _statusColor;
            }

            _barRoot.SetActive(visible && alive && _barOn);
            if (_barOn)
            {
                _barFill.sizeDelta = new Vector2(260f * _barValue, 0f);
                _barFillImage.color = _barColor;
            }

            _toast.enabled = visible && Time.time < _toastUntil;
        }

        /// <summary>"&lt;Keyboard&gt;/x" → "X"</summary>
        public static string KeyName(string binding)
        {
            if (string.IsNullOrEmpty(binding)) return "?";
            int i = binding.LastIndexOf('/');
            return (i >= 0 ? binding.Substring(i + 1) : binding).ToUpperInvariant();
        }

        // ---------------------------------------------------------------- construção

        internal static Font UiFont()
        {
            if (_font != null) return _font;
            foreach (var name in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
            {
                try { _font = Resources.GetBuiltinResource<Font>(name); } catch (Exception) { _font = null; }
                if (_font != null) break;
            }
            return _font;
        }

        private Text MakeText(string name, Vector2 pos, int size, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(_root.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(1000, 40);
            rt.anchoredPosition = pos;
            var t = go.GetComponent<Text>();
            t.font = UiFont();
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var o = go.GetComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.8f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
            t.enabled = false;
            return t;
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
            img.enabled = false;
            return img;
        }

        private static Texture2D WhiteTex()
        {
            if (_whiteTex != null) return _whiteTex;
            _whiteTex = new Texture2D(2, 2);
            _whiteTex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            _whiteTex.Apply();
            return _whiteTex;
        }

        private static Texture2D VignetteTex()
        {
            if (_vignetteTex != null) return _vignetteTex;
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
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
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            var rng = new System.Random(42);
            const int rays = 90;
            var angle = new float[rays];
            var width = new float[rays];
            var start = new float[rays];
            for (int i = 0; i < rays; i++)
            {
                angle[i] = (float)(rng.NextDouble() * 360.0);
                width[i] = 0.004f + (float)rng.NextDouble() * 0.01f;
                start[i] = 0.55f + (float)rng.NextDouble() * 0.3f;
            }
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = 0f;
                if (r > 0.45f)
                {
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    for (int i = 0; i < rays; i++)
                    {
                        if (r < start[i]) continue;
                        float d = Mathf.Abs(Mathf.DeltaAngle(ang, angle[i])) * Mathf.Deg2Rad;
                        if (d < width[i]) a = Mathf.Max(a, (1f - d / width[i]) * Mathf.InverseLerp(start[i], 1.1f, r));
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
