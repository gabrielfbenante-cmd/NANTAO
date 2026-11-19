using System.Collections.Generic;
using UnityEngine;

namespace CarrinhoCriativo
{
    internal sealed class Tubes
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();
        public void Add(Vector3 a, Vector3 b, float radius)
        {
            const int sides = 8;
            Vector3 forward = (b - a).normalized;
            Vector3 right = Vector3.Cross(forward, Mathf.Abs(forward.y) > 0.8f ? Vector3.forward : Vector3.up).normalized;
            Vector3 up = Vector3.Cross(right, forward).normalized;
            int first = vertices.Count;
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                Vector3 offset = (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
                vertices.Add(a + offset); vertices.Add(b + offset);
            }
            for (int i = 0; i < sides; i++)
            {
                int n = (i + 1) % sides;
                triangles.Add(first + i * 2); triangles.Add(first + n * 2); triangles.Add(first + i * 2 + 1);
                triangles.Add(first + n * 2); triangles.Add(first + n * 2 + 1); triangles.Add(first + i * 2 + 1);
            }
        }
        public GameObject Build(Transform parent, string name, Material material)
        {
            for (int i = 0; i < triangles.Count; i += 3)
            { int swap = triangles[i + 1]; triangles[i + 1] = triangles[i + 2]; triangles[i + 2] = swap; }
            Mesh mesh = new Mesh(); mesh.name = name;
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            GameObject obj = new GameObject(name); obj.transform.SetParent(parent, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = material;
            return obj;
        }
    }

    internal static class CartModel
    {
        internal static GameObject Build(bool authoritative, Material template, out Transform[] wheels)
        {
            GameObject root = new GameObject("CarrinhoDeSupermercado");
            Material metal = Material(template, new Color(0.58f, 0.66f, 0.72f), 0.75f);
            Material accent = Material(template, new Color(0.94f, 0.16f, 0.075f), 0.08f);
            Material rubber = Material(template, new Color(0.025f, 0.035f, 0.045f), 0f);
            Tubes frame = new Tubes();
            foreach (float x in new float[] { -0.28f, 0.28f })
            {
                frame.Add(new Vector3(x, .15f, -.42f), new Vector3(x, .15f, .40f), .018f);
                frame.Add(new Vector3(x, .15f, -.35f), new Vector3(x, 1.02f, -.48f), .015f);
                frame.Add(new Vector3(x, .18f, .32f), new Vector3(x, .50f, .28f), .014f);
                frame.Add(new Vector3(x, 1.02f, -.48f), new Vector3(x, 1.07f, -.64f), .016f);
            }
            frame.Add(new Vector3(-.28f, .15f, -.42f), new Vector3(.28f, .15f, -.42f), .017f);
            frame.Add(new Vector3(-.28f, .15f, .40f), new Vector3(.28f, .15f, .40f), .017f);
            for (int i = 0; i < 4; i++)
                frame.Add(new Vector3(-.27f, .18f, -.28f + i * .17f), new Vector3(.27f, .18f, -.28f + i * .17f), .008f);
            frame.Build(root.transform, "ChassiMetalico", metal);

            Tubes basket = new Tubes();
            for (int level = 0; level < 6; level++)
            {
                float t = level / 5f, y = .49f + t * .47f;
                float w = .25f + .075f * t, back = -.32f - .07f * t, front = .35f + .10f * t;
                basket.Add(new Vector3(-w, y, back), new Vector3(w, y, back), .005f);
                basket.Add(new Vector3(-w, y, front), new Vector3(w, y, front), .005f);
                basket.Add(new Vector3(-w, y, back), new Vector3(-w, y, front), .005f);
                basket.Add(new Vector3(w, y, back), new Vector3(w, y, front), .005f);
            }
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                float z0 = Mathf.Lerp(-.32f, .35f, t), z1 = Mathf.Lerp(-.39f, .45f, t);
                basket.Add(new Vector3(-.25f, .49f, z0), new Vector3(-.325f, .96f, z1), .0045f);
                basket.Add(new Vector3(.25f, .49f, z0), new Vector3(.325f, .96f, z1), .0045f);
                basket.Add(new Vector3(-.25f, .49f, z0), new Vector3(.25f, .49f, z0), .0045f);
            }
            for (int i = 0; i <= 7; i++)
            {
                float t = i / 7f;
                basket.Add(new Vector3(Mathf.Lerp(-.25f, .25f, t), .49f, -.32f), new Vector3(Mathf.Lerp(-.325f, .325f, t), .96f, -.39f), .0045f);
                basket.Add(new Vector3(Mathf.Lerp(-.25f, .25f, t), .49f, .35f), new Vector3(Mathf.Lerp(-.325f, .325f, t), .96f, .45f), .0045f);
                basket.Add(new Vector3(Mathf.Lerp(-.25f, .25f, t), .49f, -.32f), new Vector3(Mathf.Lerp(-.25f, .25f, t), .49f, .35f), .0045f);
            }
            basket.Build(root.transform, "CestoAramado", metal);
            Tubes handle = new Tubes();
            handle.Add(new Vector3(-.29f, 1.07f, -.64f), new Vector3(.29f, 1.07f, -.64f), .027f);
            handle.Build(root.transform, "PuxadorVermelho", accent);
            Tubes childSeat = new Tubes();
            childSeat.Add(new Vector3(-.22f, .77f, -.31f), new Vector3(.22f, .77f, -.31f), .018f);
            childSeat.Add(new Vector3(-.22f, .77f, -.12f), new Vector3(.22f, .77f, -.12f), .018f);
            childSeat.Build(root.transform, "Assento", accent);

            wheels = new Transform[4]; int wheel = 0;
            foreach (float x in new float[] { -.28f, .28f })
            foreach (float z in new float[] { -.40f, .37f })
            {
                GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                part.name = "Roda"; Object.DestroyImmediate(part.GetComponent<Collider>());
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = new Vector3(x, .095f, z);
                part.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                part.transform.localScale = new Vector3(.19f, .025f, .19f);
                part.GetComponent<MeshRenderer>().sharedMaterial = rubber;
                wheels[wheel++] = part.transform;
            }

            PhysicMaterial friction = new PhysicMaterial("BorrachaCarrinho");
            friction.dynamicFriction = .25f; friction.staticFriction = .35f; friction.bounciness = .025f;
            Box(root, new Vector3(0, .13f, 0), new Vector3(.62f, .20f, .88f), friction);
            Box(root, new Vector3(0, .48f, .015f), new Vector3(.59f, .045f, .82f), friction);
            Box(root, new Vector3(-.31f, .72f, .015f), new Vector3(.035f, .48f, .82f), friction);
            Box(root, new Vector3(.31f, .72f, .015f), new Vector3(.035f, .48f, .82f), friction);
            Box(root, new Vector3(0, .72f, .43f), new Vector3(.64f, .48f, .035f), friction);
            Box(root, new Vector3(0, .72f, -.37f), new Vector3(.64f, .48f, .035f), friction);
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 18f; body.drag = .55f; body.angularDrag = 2.5f;
            body.centerOfMass = new Vector3(0, .28f, 0);
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.isKinematic = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            return root;
        }

        private static void Box(GameObject root, Vector3 center, Vector3 size, PhysicMaterial material)
        { BoxCollider collider = root.AddComponent<BoxCollider>(); collider.center = center; collider.size = size; collider.sharedMaterial = material; }

        private static Material Material(Material source, Color color, float metallic)
        {
            Material result = source != null ? new Material(source) : new Material(Shader.Find("HDRP/Lit"));
            if (result.HasProperty("_BaseColorMap")) result.SetTexture("_BaseColorMap", Texture2D.whiteTexture);
            if (result.HasProperty("_BaseColor")) result.SetColor("_BaseColor", color);
            if (result.HasProperty("_Color")) result.SetColor("_Color", color);
            if (result.HasProperty("_Metallic")) result.SetFloat("_Metallic", metallic);
            if (result.HasProperty("_Smoothness")) result.SetFloat("_Smoothness", metallic > .5f ? .5f : .12f);
            result.enableInstancing = true;
            return result;
        }
    }
}
