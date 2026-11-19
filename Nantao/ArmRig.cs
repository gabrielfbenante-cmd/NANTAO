using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Nantao
{
    /// <summary>
    /// Um braço encontrado no esqueleto do jogo: ombro(upper) -> antebraço(lower) -> mão -> dedos.
    /// </summary>
    internal sealed class Arm
    {
        public bool IsLeft;
        public Transform Upper;
        public Transform Lower;
        public Transform Hand;
        public List<Transform[]> Fingers = new List<Transform[]>();
        public int ThumbIndex = -1;

        /// <summary>Ponto médio das pontas dos dedos (sem o polegar).</summary>
        public Vector3 FingerTip()
        {
            Vector3 sum = Vector3.zero;
            int n = 0;
            for (int i = 0; i < Fingers.Count; i++)
            {
                if (i == ThumbIndex) continue;
                var chain = Fingers[i];
                sum += chain[chain.Length - 1].position;
                n++;
            }
            return n > 0 ? sum / n : Hand.position + Hand.up * 0.05f;
        }

        /// <summary>Comprimento aproximado da mão, usado pra escalar os efeitos.</summary>
        public float HandLength()
        {
            float d = Vector3.Distance(Hand.position, FingerTip());
            return d > 1e-4f ? d : 0.08f;
        }
    }

    /// <summary>
    /// Acha os braços dentro de um modelo do jogo e aplica a pose de corrida
    /// apontando cada osso numa direção (independe do eixo interno de cada osso).
    /// </summary>
    internal sealed class ArmRig
    {
        public Arm Left;
        public Arm Right;
        public readonly string Label;

        public bool Valid => Left != null && Right != null;

        private static readonly Regex HandName =
            new Regex(@"^(hand[\._ ]?(l|r|left|right)|(l|r|left|right)[\._ ]?hand)$", RegexOptions.IgnoreCase);

        private ArmRig(string label) { Label = label; }

        public static ArmRig Build(string label, IEnumerable<Transform> bones)
        {
            var rig = new ArmRig(label);
            var list = bones.Where(b => b != null).Distinct().ToList();

            var hands = list.Where(b => HandName.IsMatch(b.name)).ToList();
            if (hands.Count < 2)
            {
                // Plano B: qualquer coisa com "hand" no nome (menos "handle"/IK/target).
                hands = list.Where(b =>
                {
                    string n = b.name.ToLowerInvariant();
                    return n.Contains("hand") && !n.Contains("handle") && !n.Contains("target") && !n.Contains("ik");
                }).ToList();
            }

            foreach (var hand in hands)
            {
                if (hand.parent == null || hand.parent.parent == null) continue;
                bool left = IsLeftName(hand.name);
                var arm = new Arm
                {
                    IsLeft = left,
                    Hand = hand,
                    Lower = hand.parent,
                    Upper = hand.parent.parent,
                };
                CollectFingers(arm);
                if (left && rig.Left == null) rig.Left = arm;
                else if (!left && rig.Right == null) rig.Right = arm;
            }

            if (ModConfig.DebugLog.Value)
            {
                Plugin.Log.LogInfo($"[{label}] ossos: {string.Join(", ", list.Select(b => b.name))}");
                Plugin.Log.LogInfo($"[{label}] esquerdo: {Describe(rig.Left)} | direito: {Describe(rig.Right)}");
            }
            return rig;
        }

        private static string Describe(Arm a) =>
            a == null ? "NÃO ENCONTRADO" : $"{a.Upper.name} > {a.Lower.name} > {a.Hand.name} ({a.Fingers.Count} dedos, polegar={a.ThumbIndex})";

        private static bool IsLeftName(string name)
        {
            string n = name.ToLowerInvariant();
            return n.EndsWith(".l") || n.EndsWith("_l") || n.EndsWith(" l") || n.EndsWith("left")
                   || n.StartsWith("l.") || n.StartsWith("l_") || n.StartsWith("left");
        }

        private static void CollectFingers(Arm arm)
        {
            foreach (Transform child in arm.Hand)
            {
                var chain = new List<Transform> { child };
                var cur = child;
                while (cur.childCount > 0)
                {
                    cur = cur.GetChild(0);
                    chain.Add(cur);
                }
                if (chain.Count >= 2) arm.Fingers.Add(chain.ToArray());
            }

            // Polegar = dedo que aponta mais "pro lado" em relação aos outros.
            if (arm.Fingers.Count >= 4)
            {
                Vector3 avg = Vector3.zero;
                foreach (var f in arm.Fingers) avg += (f[f.Length - 1].position - arm.Hand.position).normalized;
                avg.Normalize();
                float worst = float.MaxValue;
                for (int i = 0; i < arm.Fingers.Count; i++)
                {
                    var f = arm.Fingers[i];
                    float dot = Vector3.Dot((f[f.Length - 1].position - arm.Hand.position).normalized, avg);
                    if (dot < worst) { worst = dot; arm.ThumbIndex = i; }
                }
            }
        }

        /// <summary>
        /// Gira <paramref name="bone"/> para que o segmento até <paramref name="childPos"/>
        /// aponte para <paramref name="targetDir"/>, misturando com a animação original pelo peso.
        /// </summary>
        public static void Aim(Transform bone, Vector3 childPos, Vector3 targetDir, float weight)
        {
            if (weight <= 0f) return;
            Vector3 cur = childPos - bone.position;
            if (cur.sqrMagnitude < 1e-10f || targetDir.sqrMagnitude < 1e-10f) return;
            Quaternion delta = Quaternion.FromToRotation(cur, targetDir);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, delta, Mathf.Clamp01(weight)) * bone.rotation;
        }

        /// <summary>Aplica a pose num braço. As direções são no espaço do <paramref name="frame"/> (x=direita, y=cima, z=frente).</summary>
        public static void Pose(Arm arm, Transform frame, Vector3 upperDir, Vector3 lowerDir, Vector3 handDir,
                                float weight, float fingerWeight)
        {
            if (arm == null || weight <= 0f) return;

            Aim(arm.Upper, arm.Lower.position, frame.TransformDirection(upperDir), weight);
            Aim(arm.Lower, arm.Hand.position, frame.TransformDirection(lowerDir), weight);

            Vector3 handWorld = frame.TransformDirection(handDir);
            if (arm.Fingers.Count > 0)
                Aim(arm.Hand, arm.FingerTip(), handWorld, weight);

            // Dedos esticados (a "mão de faca" com energia).
            float fw = weight * fingerWeight;
            if (fw <= 0f) return;
            for (int i = 0; i < arm.Fingers.Count; i++)
            {
                float w = i == arm.ThumbIndex ? fw * 0.35f : fw;
                var chain = arm.Fingers[i];
                for (int j = 0; j < chain.Length - 1; j++)
                {
                    Vector3 target = handWorld;
                    if (i == arm.ThumbIndex)
                    {
                        // Polegar só se aproxima um pouco da direção da mão.
                        Vector3 cur = chain[j + 1].position - chain[j].position;
                        target = Vector3.Slerp(cur.normalized, handWorld.normalized, 0.5f);
                    }
                    Aim(chain[j], chain[j + 1].position, target, w);
                }
            }
        }
    }
}
