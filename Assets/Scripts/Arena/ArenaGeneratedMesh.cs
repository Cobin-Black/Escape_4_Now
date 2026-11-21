using UnityEngine;

namespace Escape4Now.Arena
{
    // Generated decorative meshes are owned by their instance, never by the material or prefab asset.
    public sealed class ArenaGeneratedMesh : MonoBehaviour
    {
        private Mesh mesh;
        public void Own(Mesh value)
        {
            mesh = value;
        }

        // Release the generated mesh when its owning object is removed.
        private void OnDestroy()
        {
            if (mesh != null)
                Destroy(mesh);
        }
    }
}
