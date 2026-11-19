using System;
using UnityEngine;

namespace ChineloArremessavel
{
    internal static class ChineloModel
    {
        public static GameObject Build(Transform parent, Material template)
        {
            GameObject root = new GameObject("ChineloVisual");
            root.transform.SetParent(parent, false);
            Material sole = MakeMaterial(template, new Color(0.025f, 0.055f, 0.10f));
            Material deck = MakeMaterial(template, new Color(0.05f, 0.38f, 0.75f));
            Material strap = MakeMaterial(template, new Color(1f, 0.76f, 0.08f));
            AddSole(root.transform, "Borracha", 0.060f, 0.148f, 0.016f, 0f, sole);
            AddSole(root.transform, "Palmilha", 0.057f, 0.144f, 0.004f, 0.016f, deck);
            AddStrap(root.transform, new Vector3(0, 0.045f, 0.073f), new Vector3(-0.054f, 0.026f, -0.014f), strap);
            AddStrap(root.transform, new Vector3(0, 0.045f, 0.073f), new Vector3(0.054f, 0.026f, -0.014f), strap);
            for (int i = 0; i < 6; i++)
            {
                GameObject groove = GameObject.CreatePrimitive(PrimitiveType.Cube);
                groove.name = "Friso";
                UnityEngine.Object.DestroyImmediate(groove.GetComponent<Collider>());
                groove.transform.SetParent(root.transform, false);
                groove.transform.localPosition = new Vector3(0, 0.0205f, -0.045f - i * 0.013f);
                groove.transform.localScale = new Vector3(0.064f, 0.001f, 0.002f);
                groove.GetComponent<MeshRenderer>().sharedMaterial = sole;
            }
            return root;
        }

        private static Material MakeMaterial(Material template, Color color)
        {
            Material result = template != null ? new Material(template) : new Material(Shader.Find("HDRP/Lit"));
            if (result.HasProperty("_BaseColorMap")) result.SetTexture("_BaseColorMap", Texture2D.whiteTexture);
            if (result.HasProperty("_BaseColor")) result.SetColor("_BaseColor", color);
            if (result.HasProperty("_Color")) result.SetColor("_Color", color);
            if (result.HasProperty("_Smoothness")) result.SetFloat("_Smoothness", 0.12f);
            if (result.HasProperty("_Metallic")) result.SetFloat("_Metallic", 0f);
            result.enableInstancing = true;
            return result;
        }

        private static void AddSole(Transform parent, string name, float width, float length, float thickness, float y, Material material)
        {
            const int count = 32;
            Vector3[] vertices = new Vector3[count * 2 + 2];
            int[] triangles = new int[count * 12];
            vertices[count * 2] = new Vector3(0, y, 0);
            vertices[count * 2 + 1] = new Vector3(0, y + thickness, 0);
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                float z = Mathf.Cos(angle);
                float x = Mathf.Sin(angle) * width * (0.83f + 0.17f * (z + 1f) * 0.5f);
                vertices[i] = new Vector3(x, y, z * length);
                vertices[i + count] = new Vector3(x, y + thickness, z * length);
                int j = (i + 1) % count;
                int k = i * 12;
                triangles[k] = count * 2; triangles[k + 1] = i; triangles[k + 2] = j;
                triangles[k + 3] = count * 2 + 1; triangles[k + 4] = j + count; triangles[k + 5] = i + count;
                triangles[k + 6] = i; triangles[k + 7] = i + count; triangles[k + 8] = j;
                triangles[k + 9] = j; triangles[k + 10] = i + count; triangles[k + 11] = j + count;
            }
            // The ring follows +Z toward +X; reverse every face for outward normals.
            for (int i = 0; i < triangles.Length; i += 3)
            { int swap = triangles[i + 1]; triangles[i + 1] = triangles[i + 2]; triangles[i + 2] = swap; }
            Mesh mesh = new Mesh();
            mesh.name = name;
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            GameObject piece = new GameObject(name);
            piece.transform.SetParent(parent, false);
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            piece.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void AddStrap(Transform parent, Vector3 start, Vector3 end, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            part.name = "TiraAmarela";
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = (start + end) * 0.5f;
            part.transform.localRotation = Quaternion.FromToRotation(Vector3.up, end - start);
            part.transform.localScale = new Vector3(0.014f, Vector3.Distance(start, end) * 0.5f, 0.014f);
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        public static Sprite MakeIcon()
        {
            Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float nx = (x - 31.5f) / 19f, ny = (y - 31.5f) / 29f;
                Color color = Color.clear;
                if (nx * nx + ny * ny < 1f) color = new Color(0.08f, 0.5f, 0.95f, 1f);
                float strapX = Math.Abs((y - 45f) * 0.7f);
                if (y >= 28 && y <= 46 && Math.Abs(Math.Abs(x - 31.5f) - strapX) < 2.5f)
                    color = new Color(1f, 0.8f, 0.12f, 1f);
                pixels[y * 64 + x] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);
        }
    }
}
