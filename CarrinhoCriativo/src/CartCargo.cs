using UnityEngine;

namespace CarrinhoCriativo
{
    internal sealed class CargoRecord
    {
        internal readonly GrabbableObject Item;
        internal readonly bool Grabbable;
        internal readonly bool Pocketed;
        internal readonly Vector3 Scale;
        private readonly Collider[] colliders;
        private readonly bool[] enabled;

        internal CargoRecord(GrabbableObject item)
        {
            Item = item; Grabbable = item.grabbable; Pocketed = item.isPocketed; Scale = item.transform.localScale;
            colliders = item.GetComponentsInChildren<Collider>(true); enabled = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++) { enabled[i] = colliders[i].enabled; colliders[i].enabled = false; }
            item.grabbable = false;
            // Container contents should not be offered by the chinelo's recall
            // helper as loose world items. Their meshes stay visible explicitly.
            item.isPocketed = true;
            item.EnableItemMeshes(true);
            Renderer[] renderers = item.GetComponentsInChildren<Renderer>();
            Bounds bounds = new Bounds(item.transform.position, Vector3.zero);
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (largest > .28f) item.transform.localScale = Scale * (.28f / largest);
        }

        internal void Place(Transform cart, int index)
        {
            if (Item == null) return;
            Vector3 slot = new Vector3(index % 2 == 0 ? -.135f : .135f, .57f + index / 8 * .10f, -.23f + index / 2 % 4 * .15f);
            Item.transform.position = cart.TransformPoint(slot);
            Item.transform.rotation = cart.rotation * Quaternion.Euler(Item.itemProperties.restingRotation);
            Item.fallTime = 1f; Item.reachedFloorTarget = true;
            Item.targetFloorPosition = Item.transform.localPosition;
            Item.startFallingPosition = Item.transform.localPosition;
        }

        internal void Restore(Vector3 position)
        {
            if (Item == null) return;
            Item.grabbable = Grabbable; Item.isPocketed = Pocketed;
            Item.transform.localScale = Scale;
            Item.transform.position = position;
            for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = enabled[i];
            Item.fallTime = 0; Item.reachedFloorTarget = false;
            Item.FallToGround(false, false, Vector3.zero);
        }
    }
}
