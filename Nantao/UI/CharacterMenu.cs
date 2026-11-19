using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Nantao
{
    /// <summary>
    /// Menu de escolha: 10 cartas (5x2) + "Sem personagem". Cada personagem só pode
    /// ser de um jogador; quem já pegou aparece na carta. Clique com o mouse.
    /// </summary>
    internal sealed class CharacterMenu : IDisposable
    {
        private GameObject _root;
        private readonly List<Card> _cards = new List<Card>();
        private Text _footer;
        private Button _noneButton;
        private bool _setLook;
        private float _refresh;
        private float _closeAt = -1f;

        public bool IsOpen => _root != null && _root.activeSelf;

        private sealed class Card
        {
            public int Id;
            public Button Button;
            public Image Background;
            public Image Stripe;
            public Text Name;
            public Text Desc;
            public Text State;
        }

        public void Open()
        {
            if (_root == null) Build();
            EnsureEventSystem();
            _root.SetActive(true);
            _refresh = 0f;
            _closeAt = -1f;
        }

        /// <param name="restoreCursor">false quando outro menu (pausa) está assumindo o mouse.</param>
        public void Close(bool restoreCursor = true)
        {
            if (_root != null) _root.SetActive(false);
            if (restoreCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            var p = PlayerAgent.Local?.Player;
            if (_setLook && p != null) p.disableLookInput = false;
            _setLook = false;
        }

        public void Tick(PlayerAgent local)
        {
            if (!IsOpen) return;

            // Mouse livre e câmera parada enquanto o menu está aberto.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (!local.Player.disableLookInput) { local.Player.disableLookInput = true; _setLook = true; }

            if (_closeAt > 0f && Time.time >= _closeAt) { Close(); return; }

            _refresh -= Time.deltaTime;
            if (_refresh > 0f) return;
            _refresh = 0.2f;

            bool canSwap = local.CanSwapNow;
            foreach (var c in _cards)
            {
                var def = Characters.Get(c.Id);
                int owner = AgentNetwork.OwnerOf(c.Id);
                bool mine = local.CharacterId == c.Id;
                bool taken = owner >= 0 && owner != local.PlayerIndex;
                Color col = def.Color();

                c.Name.text = def.Name;
                c.Desc.text = def.Description;
                c.Stripe.color = col;

                if (!def.Implemented) { c.State.text = "EM BREVE"; c.State.color = new Color(0.6f, 0.6f, 0.6f); }
                else if (mine) { c.State.text = "VOCÊ"; c.State.color = Color.Lerp(col, Color.white, 0.4f); }
                else if (taken) { c.State.text = "OCUPADO · " + PlayerName(owner); c.State.color = new Color(1f, 0.45f, 0.35f); }
                else { c.State.text = "LIVRE"; c.State.color = new Color(0.6f, 1f, 0.6f); }

                c.Button.interactable = def.Implemented && !taken && !mine && canSwap;
                c.Background.color = mine ? new Color(col.r * 0.35f, col.g * 0.35f, col.b * 0.35f, 0.95f)
                    : def.Implemented ? new Color(0.09f, 0.1f, 0.13f, 0.95f) : new Color(0.06f, 0.06f, 0.07f, 0.9f);
            }
            _noneButton.interactable = canSwap && local.CharacterId != Characters.None;
            _footer.text = canSwap
                ? $"Clique pra escolher · cada personagem só pode ser de UM jogador · [{AgentHud.KeyName(ModConfig.MenuKey.Value)}] fecha"
                : "Só dá pra trocar com a nave em órbita.";
        }

        private static string PlayerName(int idx)
        {
            var sor = StartOfRound.Instance;
            if (sor == null || idx < 0 || idx >= sor.allPlayerScripts.Length) return "?";
            return sor.allPlayerScripts[idx].playerUsername;
        }

        private void Pick(int id)
        {
            AgentNetwork.RequestPick(id);
            Sounds.PlayUi(Sounds.Pick);
            _closeAt = Time.time + 0.35f;
        }

        // ---------------------------------------------------------------- construção

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("NantaoEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        private void Build()
        {
            _root = new GameObject("NantaoCharacterMenu");
            UnityEngine.Object.DontDestroyOnLoad(_root);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            _root.AddComponent<GraphicRaycaster>();

            var dim = Rect("Dim", _root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dim.gameObject.AddComponent<Image>().color = new Color(0.01f, 0.015f, 0.03f, 0.88f);

            var title = Label(_root.transform, "ESCOLHA SEU PERSONAGEM", 44, FontStyle.Bold, new Vector2(0, 400), new Vector2(1400, 60));
            title.color = Color.white;

            const float w = 300, h = 250, gap = 24;
            for (int i = 0; i < Characters.Count; i++)
            {
                int row = i / 5, col = i % 5;
                float x = (col - 2) * (w + gap);
                float y = 190 - row * (h + gap);
                _cards.Add(MakeCard(i, new Vector2(x, y), new Vector2(w, h)));
            }

            _noneButton = MakeButton(_root.transform, "Sem personagem", new Vector2(0, -330), new Vector2(320, 56),
                () => Pick(Characters.None));
            _footer = Label(_root.transform, "", 20, FontStyle.Normal, new Vector2(0, -400), new Vector2(1600, 40));
            _footer.color = new Color(0.75f, 0.78f, 0.85f);

            _root.SetActive(false);
        }

        private Card MakeCard(int id, Vector2 pos, Vector2 size)
        {
            var rt = Rect("Card" + id, _root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            var bg = rt.gameObject.AddComponent<Image>();
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
            colors.disabledColor = new Color(0.75f, 0.75f, 0.75f);
            btn.colors = colors;
            btn.onClick.AddListener(() => Pick(id));

            var stripe = Rect("Stripe", rt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -4), new Vector2(0, 8))
                .gameObject.AddComponent<Image>();

            var number = Label(rt, (id + 1).ToString("00"), 18, FontStyle.Bold, new Vector2(-118, 96), new Vector2(50, 30));
            number.color = new Color(1f, 1f, 1f, 0.4f);

            var name = Label(rt, "", 30, FontStyle.Bold, new Vector2(0, 60), new Vector2(280, 44));
            var desc = Label(rt, "", 17, FontStyle.Normal, new Vector2(0, -10), new Vector2(270, 90));
            desc.color = new Color(0.82f, 0.85f, 0.9f);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            var state = Label(rt, "", 18, FontStyle.Bold, new Vector2(0, -96), new Vector2(280, 30));

            return new Card { Id = id, Button = btn, Background = bg, Stripe = stripe, Name = name, Desc = desc, State = state };
        }

        private static Button MakeButton(Transform parent, string text, Vector2 pos, Vector2 size, Action onClick)
        {
            var rt = Rect("Button", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.16f, 0.17f, 0.22f, 0.95f);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => onClick());
            var t = Label(rt, text, 22, FontStyle.Bold, Vector2.zero, size);
            t.color = Color.white;
            return b;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static Text Label(Transform parent, string text, int size, FontStyle style, Vector2 pos, Vector2 box)
        {
            var rt = Rect("Text", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, box);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = AgentHud.UiFont();
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            t.color = Color.white;
            return t;
        }

        public void Dispose()
        {
            if (IsOpen) Close();
            if (_root != null) UnityEngine.Object.Destroy(_root);
        }
    }
}
