using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace NeonRun
{
    /// <summary>Materiais brilhantes criados em tempo de execução (o jogo usa HDRP).</summary>
    internal static class NeonMaterials
    {
        private static Shader _unlit;
        private static bool _searched;

        private static Shader FindUnlit()
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
            if (NeonConfig.DebugLog.Value) Plugin.Log.LogInfo($"Shader dos efeitos: {(_unlit != null ? _unlit.name : "nenhum")}");
            return _unlit;
        }

        public static Material Create(Color color)
        {
            var shader = FindUnlit();
            if (shader == null) return null;
            var m = new Material(shader) { name = "NeonRunGlow" };
            SetColor(m, color);
            if (shader.name.StartsWith("HDRP"))
            {
                try { HDMaterial.ValidateMaterial(m); } catch (Exception) { /* versão do HDRP sem esse método */ }
            }
            return m;
        }

        public static void SetColor(Material m, Color color)
        {
            if (m == null) return;
            Color hdr = color * Mathf.Max(0.1f, NeonConfig.GlowIntensity.Value);
            hdr.a = 1f;
            if (m.HasProperty("_UnlitColor")) m.SetColor("_UnlitColor", hdr);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", hdr);
            if (m.HasProperty("_Color")) m.SetColor("_Color", hdr);
        }
    }

    /// <summary>Raios, luz e rastro de energia numa mão.</summary>
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
        private readonly Light _light;
        private readonly HDAdditionalLightData _hdLight;
        private bool _wasOn;

        private sealed class Bolt
        {
            public LineRenderer Line;
            public Vector3[] Local = new Vector3[BoltPoints];
            public Vector3[] World = new Vector3[BoltPoints];
            public float Timer;
            public bool Visible;
        }

        public HandFx(Arm arm, int layer, Transform owner)
        {
            _arm = arm;
            _root = new GameObject("NeonRunHandFx");
            _root.transform.SetParent(owner, false);
            _root.layer = layer;

            _matPrimary = NeonMaterials.Create(NeonConfig.Primary);
            _matSecondary = NeonMaterials.Create(NeonConfig.Secondary);

            int count = Mathf.Clamp(NeonConfig.BoltsPerHand.Value, 0, 12);
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Bolt" + i);
                go.layer = layer;
                go.transform.SetParent(_root.transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = BoltPoints;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.numCapVertices = 0;
                lr.sharedMaterial = UnityEngine.Random.value < 0.75f ? _matPrimary : _matSecondary;
                lr.enabled = false;
                _bolts.Add(new Bolt { Line = lr });
            }

            // Marcador na ponta dos dedos (rastro sai daqui).
            var tip = new GameObject("NeonRunTip");
            tip.layer = layer;
            tip.transform.SetParent(_root.transform, false);
            _tipMarker = tip.transform;

            if (NeonConfig.HandTrails.Value)
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

            if (NeonConfig.HandLights.Value)
            {
                try
                {
                    var lightGo = new GameObject("NeonRunLight");
                    lightGo.transform.SetParent(_root.transform, false);
                    _hdLight = lightGo.AddHDLight(HDLightTypeAndShape.Point);
                    _light = lightGo.GetComponent<Light>();
                    _hdLight.EnableShadows(false);
                    _hdLight.affectsVolumetric = false;
                    _hdLight.SetColor(NeonConfig.Primary);
                    _hdLight.SetRange(4f);
                    _hdLight.SetIntensity(0f);
                    _light.enabled = false;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Não consegui criar a luz das mãos: " + e.Message);
                    _light = null;
                    _hdLight = null;
                }
            }
        }

        public void Recolor()
        {
            NeonMaterials.SetColor(_matPrimary, NeonConfig.Primary);
            NeonMaterials.SetColor(_matSecondary, NeonConfig.Secondary);
            if (_hdLight != null) _hdLight.SetColor(NeonConfig.Primary);
        }

        /// <param name="intensity">0 = desligado, 1 = cheio.</param>
        public void Tick(float intensity, float dt)
        {
            if (_arm == null || _arm.Hand == null) return;
            bool on = intensity > 0.02f;

            float handLen = _arm.HandLength();
            float size = Mathf.Max(0.05f, NeonConfig.BoltSize.Value);
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
                _light.enabled = on;
                if (on)
                {
                    _light.transform.position = Vector3.Lerp(_arm.Hand.position, tip, 0.5f);
                    float flicker = 0.7f + 0.3f * Mathf.PerlinNoise(Time.time * 22f, _arm.IsLeft ? 0f : 7f);
                    _hdLight.SetIntensity(220f * intensity * flicker * Mathf.Max(0f, NeonConfig.HandLightIntensity.Value));
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

                // Pontos guardados no espaço da mão: o raio "gruda" na mão mesmo correndo rápido.
                for (int i = 0; i < BoltPoints; i++) b.World[i] = _arm.Hand.TransformPoint(b.Local[i]);
                b.Line.SetPositions(b.World);
                b.Line.widthMultiplier = width;
            }
            _wasOn = on;
        }

        private void Regenerate(Bolt b, float len, float intensity)
        {
            b.Timer = UnityEngine.Random.Range(0.035f, 0.09f);
            b.Visible = UnityEngine.Random.value < 0.55f + 0.4f * intensity;
            if (!b.Visible) return;

            // Começa num osso aleatório da mão/dedos e salta pra fora.
            Vector3 start = RandomHandPoint();
            Vector3 dir = UnityEngine.Random.onUnitSphere;
            Vector3 toTip = (_arm.FingerTip() - _arm.Hand.position).normalized;
            dir = (dir + toTip * 0.6f).normalized;
            Vector3 end = start + dir * len * UnityEngine.Random.Range(0.5f, 1.4f);

            Vector3 side = Vector3.Cross(dir, UnityEngine.Random.onUnitSphere).normalized;
            Vector3 side2 = Vector3.Cross(dir, side);
            for (int i = 0; i < BoltPoints; i++)
            {
                float t = i / (float)(BoltPoints - 1);
                Vector3 p = Vector3.Lerp(start, end, t);
                float jitter = (i == 0 || i == BoltPoints - 1) ? 0f : len * 0.18f;
                p += side * UnityEngine.Random.Range(-jitter, jitter) + side2 * UnityEngine.Random.Range(-jitter, jitter);
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
}
