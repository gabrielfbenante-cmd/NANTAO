using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Nantao
{
    /// <summary>Materiais criados em tempo de execução (o jogo usa HDRP).</summary>
    internal static class Mats
    {
        private static Shader _unlit;
        private static bool _searched;

        private static Shader Unlit()
        {
            if (_searched) return _unlit;
            _searched = true;
            _unlit = Shader.Find("HDRP/Unlit");
            if (_unlit == null)
            {
                foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
                {
                    if (m != null && m.shader != null && m.shader.name == "HDRP/Unlit") { _unlit = m.shader; break; }
                }
            }
            if (_unlit == null) _unlit = Shader.Find("Unlit/Color");
            if (_unlit == null) _unlit = Shader.Find("Sprites/Default");
            if (ModConfig.DebugLog.Value) Plugin.Log.LogInfo($"Shader dos efeitos: {(_unlit != null ? _unlit.name : "nenhum")}");
            return _unlit;
        }

        /// <summary>Material brilhante opaco (raios, anéis, linhas).</summary>
        public static Material Glow(Color color, float intensity = -1f)
        {
            var shader = Unlit();
            if (shader == null) return null;
            var m = new Material(shader) { name = "NantaoGlow" };
            SetGlow(m, color, intensity);
            Validate(m);
            return m;
        }

        public static void SetGlow(Material m, Color color, float intensity = -1f)
        {
            if (m == null) return;
            float k = intensity > 0f ? intensity : Mathf.Max(0.1f, ModConfig.GlowIntensity.Value);
            Color hdr = color * k;
            hdr.a = 1f;
            SetColor(m, hdr);
        }

        /// <summary>Material translúcido, visível dos dois lados (fumaça em esferas).</summary>
        public static Material Transparent(Color color)
        {
            var shader = Unlit();
            if (shader == null) return null;
            var m = new Material(shader) { name = "NantaoTransparent" };
            if (m.HasProperty("_SurfaceType")) m.SetFloat("_SurfaceType", 1f);
            if (m.HasProperty("_BlendMode")) m.SetFloat("_BlendMode", 0f);
            if (m.HasProperty("_DoubleSidedEnable")) m.SetFloat("_DoubleSidedEnable", 1f);
            if (m.HasProperty("_CullMode")) m.SetFloat("_CullMode", 0f);
            if (m.HasProperty("_CullModeForward")) m.SetFloat("_CullModeForward", 0f);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_TransparentZWrite")) m.SetFloat("_TransparentZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            SetColor(m, color);
            Validate(m);
            return m;
        }

        public static void SetColor(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_UnlitColor")) m.SetColor("_UnlitColor", c);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        private static void Validate(Material m)
        {
            if (!m.shader.name.StartsWith("HDRP")) return;
            try { HDMaterial.ValidateMaterial(m); } catch (Exception) { /* versão sem esse método */ }
        }
    }

    /// <summary>Utilidades pra montar efeitos com linhas e luzes.</summary>
    internal static class FxUtil
    {
        public static LineRenderer Line(Transform parent, string name, Material mat, int points, bool worldSpace, int layer = 0)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = worldSpace;
            lr.positionCount = points;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCapVertices = 2;
            lr.sharedMaterial = mat;
            return lr;
        }

        public static LineRenderer Ring(Transform parent, string name, Material mat, float radius, float width, int segments = 40)
        {
            var lr = Line(parent, name, mat, segments, false);
            lr.loop = true;
            lr.widthMultiplier = width;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
            return lr;
        }

        /// <summary>Cria uma luz pontual HDRP (ou null se não der).</summary>
        public static HDAdditionalLightData PointLight(Transform parent, Color color, float range)
        {
            try
            {
                var go = new GameObject("NantaoLight");
                go.transform.SetParent(parent, false);
                var hd = go.AddHDLight(HDLightTypeAndShape.Point);
                hd.EnableShadows(false);
                hd.affectsVolumetric = false;
                hd.SetColor(color);
                hd.SetRange(range);
                hd.SetIntensity(0f);
                return hd;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Não consegui criar luz: " + e.Message);
                return null;
            }
        }
    }

    /// <summary>Raios, luz e rastro de energia numa mão (usado por vários personagens).</summary>
    internal sealed class HandFx : IDisposable
    {
        private const int BoltPoints = 7;

        private readonly Arm _arm;
        private readonly GameObject _root;
        private readonly List<Bolt> _bolts = new List<Bolt>();
        private readonly Material _matPrimary;
        private readonly Material _matSecondary;
        private readonly TrailRenderer _trail;
        private readonly Transform _tipMarker;
        private readonly HDAdditionalLightData _light;
        private readonly float _jitter;
        private bool _wasOn;

        private sealed class Bolt
        {
            public LineRenderer Line;
            public readonly Vector3[] Local = new Vector3[BoltPoints];
            public readonly Vector3[] World = new Vector3[BoltPoints];
            public float Timer;
            public bool Visible;
        }

        /// <param name="jitter">1 = raio elétrico em zigue-zague; perto de 0 = filetes suaves.</param>
        public HandFx(Arm arm, int layer, Transform owner, Color primary, Color secondary, int bolts, bool trail, float jitter = 1f)
        {
            _arm = arm;
            _jitter = jitter;
            _root = new GameObject("NantaoHandFx") { layer = layer };
            _root.transform.SetParent(owner, false);

            _matPrimary = Mats.Glow(primary);
            _matSecondary = Mats.Glow(secondary);

            for (int i = 0; i < Mathf.Clamp(bolts, 0, 12); i++)
            {
                var lr = FxUtil.Line(_root.transform, "Bolt" + i, _matPrimary, BoltPoints, true, layer);
                lr.numCapVertices = 0;
                lr.enabled = false;
                _bolts.Add(new Bolt { Line = lr });
            }

            var tip = new GameObject("NantaoTip") { layer = layer };
            tip.transform.SetParent(_root.transform, false);
            _tipMarker = tip.transform;

            if (trail)
            {
                _trail = tip.AddComponent<TrailRenderer>();
                _trail.time = 0.14f;
                _trail.minVertexDistance = 0.005f;
                _trail.shadowCastingMode = ShadowCastingMode.Off;
                _trail.receiveShadows = false;
                _trail.sharedMaterial = _matPrimary;
                _trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                _trail.emitting = false;
            }

            if (ModConfig.HandLights.Value)
            {
                _light = FxUtil.PointLight(_root.transform, primary, 4f);
                if (_light != null) _light.gameObject.SetActive(false);
            }
        }

        public void Tick(float intensity, float dt)
        {
            if (_arm == null || _arm.Hand == null) return;
            bool on = intensity > 0.02f;

            float handLen = _arm.HandLength();
            float size = Mathf.Max(0.05f, ModConfig.BoltSize.Value);
            Vector3 tip = _arm.FingerTip();
            _tipMarker.position = tip;
            _root.transform.position = _arm.Hand.position;

            if (_trail != null)
            {
                if (on && !_wasOn) _trail.Clear();
                _trail.emitting = intensity > 0.3f;
                _trail.widthMultiplier = handLen * 0.45f * size;
            }

            if (_light != null)
            {
                _light.gameObject.SetActive(on);
                if (on)
                {
                    _light.transform.position = Vector3.Lerp(_arm.Hand.position, tip, 0.5f);
                    float flicker = 0.7f + 0.3f * Mathf.PerlinNoise(Time.time * 22f, _arm.IsLeft ? 0f : 7f);
                    _light.SetIntensity(220f * intensity * flicker * Mathf.Max(0f, ModConfig.HandLightIntensity.Value));
                }
            }

            float width = handLen * 0.07f * size;
            foreach (var b in _bolts)
            {
                if (!on) { b.Line.enabled = false; b.Visible = false; continue; }
                b.Timer -= dt;
                if (b.Timer <= 0f) Regenerate(b, handLen * size, intensity);
                b.Line.enabled = b.Visible;
                if (!b.Visible) continue;
                // Pontos no espaço da mão: o raio "gruda" na mão mesmo em movimento.
                for (int i = 0; i < BoltPoints; i++) b.World[i] = _arm.Hand.TransformPoint(b.Local[i]);
                b.Line.SetPositions(b.World);
                b.Line.widthMultiplier = width;
            }
            _wasOn = on;
        }

        private void Regenerate(Bolt b, float len, float intensity)
        {
            b.Timer = UnityEngine.Random.Range(0.035f, 0.09f) * Mathf.Lerp(3f, 1f, _jitter);
            b.Visible = UnityEngine.Random.value < 0.55f + 0.4f * intensity;
            if (!b.Visible) return;

            Vector3 start = RandomHandPoint();
            Vector3 toTip = (_arm.FingerTip() - _arm.Hand.position).normalized;
            Vector3 dir = (UnityEngine.Random.onUnitSphere + toTip * 0.6f).normalized;
            Vector3 end = start + dir * len * UnityEngine.Random.Range(0.5f, 1.4f);

            Vector3 side = Vector3.Cross(dir, UnityEngine.Random.onUnitSphere).normalized;
            Vector3 side2 = Vector3.Cross(dir, side);
            for (int i = 0; i < BoltPoints; i++)
            {
                float t = i / (float)(BoltPoints - 1);
                Vector3 p = Vector3.Lerp(start, end, t);
                float j = (i == 0 || i == BoltPoints - 1) ? 0f : len * 0.18f * _jitter;
                p += side * UnityEngine.Random.Range(-j, j) + side2 * UnityEngine.Random.Range(-j, j);
                b.Local[i] = _arm.Hand.InverseTransformPoint(p);
            }
            b.Line.sharedMaterial = UnityEngine.Random.value < 0.75f ? _matPrimary : _matSecondary;
        }

        private Vector3 RandomHandPoint()
        {
            if (_arm.Fingers.Count == 0) return _arm.Hand.position;
            var chain = _arm.Fingers[UnityEngine.Random.Range(0, _arm.Fingers.Count)];
            return chain[UnityEngine.Random.Range(0, chain.Length)].position;
        }

        public void Dispose()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
            if (_matPrimary != null) UnityEngine.Object.Destroy(_matPrimary);
            if (_matSecondary != null) UnityEngine.Object.Destroy(_matSecondary);
        }
    }

    /// <summary>Par de HandFx que segue o esqueleto certo do jogador (1ª pessoa local ou corpo).</summary>
    internal sealed class HandsFx : IDisposable
    {
        private readonly PlayerAgent _agent;
        private readonly Func<Color> _primary, _secondary;
        private readonly Func<int> _bolts;
        private readonly bool _trail;
        private readonly float _jitter;
        private HandFx _left, _right;
        private ArmRig _rig;

        public HandsFx(PlayerAgent agent, Func<Color> primary, Func<Color> secondary, Func<int> bolts, bool trail, float jitter = 1f)
        {
            _agent = agent;
            _primary = primary;
            _secondary = secondary;
            _bolts = bolts;
            _trail = trail;
            _jitter = jitter;
        }

        /// <param name="left">intensidade da mão esquerda (0..1)</param>
        /// <param name="right">intensidade da mão direita (0..1)</param>
        public void Tick(float left, float right, float dt)
        {
            var target = _agent.FxRig;
            if (target == null || !target.Valid)
            {
                Clear();
                return;
            }
            if (_rig != target)
            {
                if (left <= 0.001f && right <= 0.001f && _left == null) return; // cria só quando precisar
                Clear();
                int layer = _agent.FxLayer;
                _left = new HandFx(target.Left, layer, _agent.transform, _primary(), _secondary(), _bolts(), _trail, _jitter);
                _right = new HandFx(target.Right, layer, _agent.transform, _primary(), _secondary(), _bolts(), _trail, _jitter);
                _rig = target;
            }
            _left.Tick(left, dt);
            _right.Tick(right, dt);
        }

        private void Clear()
        {
            _left?.Dispose();
            _right?.Dispose();
            _left = _right = null;
            _rig = null;
        }

        public void Dispose() => Clear();
    }

    /// <summary>Explosão rápida de linhas + clarão (teleporte, reviver, fumaça).</summary>
    internal sealed class BurstFx : MonoBehaviour
    {
        private readonly List<LineRenderer> _rays = new List<LineRenderer>();
        private readonly List<Vector3> _dirs = new List<Vector3>();
        private LineRenderer _column;
        private HDAdditionalLightData _light;
        private Material _mat;
        private float _t;
        private float _life;
        private float _size;

        public static void Spawn(Vector3 pos, Color color, float size = 1f, float life = 0.45f)
        {
            var go = new GameObject("NantaoBurst");
            go.transform.position = pos;
            go.AddComponent<BurstFx>().Init(color, size, life);
        }

        private void Init(Color color, float size, float life)
        {
            _size = size;
            _life = life;
            _mat = Mats.Glow(color);
            for (int i = 0; i < 16; i++)
            {
                var lr = FxUtil.Line(transform, "Ray", _mat, 2, true);
                _rays.Add(lr);
                var d = UnityEngine.Random.onUnitSphere;
                d.y = Mathf.Abs(d.y) * 0.8f + 0.1f;
                _dirs.Add(d.normalized);
            }
            _column = FxUtil.Line(transform, "Column", _mat, 2, true);
            _light = FxUtil.PointLight(transform, color, 8f * size);
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _life);
            float fade = 1f - k;
            Vector3 c = transform.position;
            for (int i = 0; i < _rays.Count; i++)
            {
                float r0 = Mathf.Lerp(0.2f, 1.4f, k) * _size;
                float r1 = r0 + 0.5f * _size * fade;
                _rays[i].SetPosition(0, c + _dirs[i] * r0);
                _rays[i].SetPosition(1, c + _dirs[i] * r1);
                _rays[i].widthMultiplier = 0.035f * _size * fade;
            }
            _column.SetPosition(0, c);
            _column.SetPosition(1, c + Vector3.up * 2.4f * _size * (0.3f + k));
            _column.widthMultiplier = 0.25f * _size * fade;
            if (_light != null) _light.SetIntensity(2500f * fade * fade * _size);
            if (_t >= _life) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }
    }

    /// <summary>Projétil brilhante em arco da mão até o alvo; chama onLand quando chega.</summary>
    internal sealed class ArcProjectile : MonoBehaviour
    {
        private Vector3 _from, _to;
        private float _travel, _t, _height;
        private Action<Vector3> _onLand;
        private Material _mat;
        private LineRenderer _core;

        public static void Spawn(Vector3 from, Vector3 to, float travel, Color color, Action<Vector3> onLand, float size = 1f)
        {
            var go = new GameObject("NantaoProjectile");
            go.transform.position = from;
            var p = go.AddComponent<ArcProjectile>();
            p._from = from;
            p._to = to;
            p._travel = Mathf.Max(0.05f, travel);
            p._onLand = onLand;
            p._height = Mathf.Min(4f, Vector3.Distance(from, to) * 0.18f);
            p.Build(color, size);
        }

        private void Build(Color color, float size)
        {
            _mat = Mats.Glow(color, 3f);
            var trail = gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = _mat;
            trail.time = 0.25f;
            trail.widthMultiplier = 0.1f * size;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            _core = FxUtil.Line(transform, "Core", _mat, 2, false);
            _core.widthMultiplier = 0.12f * size;
            _core.SetPosition(0, Vector3.up * -0.06f * size);
            _core.SetPosition(1, Vector3.up * 0.06f * size);
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _travel);
            transform.position = Vector3.Lerp(_from, _to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * _height);
            if (k < 1f) return;
            try { _onLand?.Invoke(_to); }
            catch (Exception e) { Plugin.Log.LogWarning("Projétil: " + e.Message); }
            _core.enabled = false;
            enabled = false;
            Destroy(gameObject, 0.3f);
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }
    }
}
