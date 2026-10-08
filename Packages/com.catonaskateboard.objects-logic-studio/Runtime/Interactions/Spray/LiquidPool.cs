using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Bounds live sauce physics and reuses bodies without per-emission instantiation.</summary>
    [AddComponentMenu("")]
    internal sealed class LiquidPool : MonoBehaviour
    {
        #region State
        private const int capacity = 192;
        private static LiquidPool instance;
        private readonly LiquidDroplet[] droplets = new LiquidDroplet[capacity];
        private Mesh mesh;
        private Material material;
        private int cursor;
        #endregion
        #region Methods
        #region Emission
        /// <summary>Emits into a free slot, dropping excess visuals when the bounded pool is saturated.</summary>
        /// <param name="source">Emitter configuration and ownership.</param>
        /// <param name="position">World outlet position.</param>
        /// <param name="velocity">World initial velocity.</param>
        /// <param name="color">Sampled palette colour.</param>
        internal static void Emit(ObjectSpraySauce source, Vector3 position, Vector3 velocity, Color color)
        {
            if (instance == null)
            {
                GameObject root = new GameObject("Object Liquid Pool");
                DontDestroyOnLoad(root);
                instance = root.AddComponent<LiquidPool>();
                instance.Initialize();
            }
            if (instance.material == null)
                return;
            for (int attempt = 0; attempt < capacity; attempt++)
            {
                LiquidDroplet droplet = instance.droplets[instance.cursor];
                instance.cursor = (instance.cursor + 1) % capacity;
                if (droplet.gameObject.activeSelf)
                    continue;
                droplet.Launch(source, position, velocity, color);
                return;
            }
        }
        /// <summary>Rechecks live liquid when its emitter gains identity requirements during a pulse.</summary>
        /// <param name="root">Changed source hierarchy.</param>
        internal static void IncludeSource(Transform root)
        {
            if (instance == null)
                return;
            foreach (LiquidDroplet droplet in instance.droplets)
                if (droplet != null && droplet.gameObject.activeSelf && droplet.Source != null && droplet.Source.transform.IsChildOf(root))
                    ObjectGravityGenerator.IncludeSpawn(droplet.gameObject);
        }
        #endregion
        #region Resources
        /// <summary>Builds the shared render resources and fixed body pool once.</summary>
        private void Initialize()
        {
            Shader shader = Resources.Load<Shader>("SurfaceDeposit");
            if (shader == null)
            {
                Debug.LogWarning("Spray Sauce requires the SurfaceDeposit shader resource.", this);
                return;
            }
            material = new Material(shader) { name = "Liquid Runtime" };
            mesh = BuildSphere();
            for (int index = 0; index < droplets.Length; index++)
            {
                GameObject root = new GameObject("Sauce Droplet");
                root.SetActive(false);
                root.transform.SetParent(transform, false);
                droplets[index] = root.AddComponent<LiquidDroplet>();
                droplets[index].Initialize(mesh, material);
            }
        }
        /// <summary>Creates one compact smooth sphere shared by every liquid body.</summary>
        /// <returns>Unit-radius geometry with outward normals and UV coordinates.</returns>
        private static Mesh BuildSphere()
        {
            const int rings = 8;
            const int sectors = 12;
            Vector3[] vertices = new Vector3[(rings + 1) * (sectors + 1)];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[rings * sectors * 6];
            int write = 0;
            for (int ring = 0; ring <= rings; ring++)
                for (int sector = 0; sector <= sectors; sector++)
                {
                    int index = ring * (sectors + 1) + sector;
                    float latitude = Mathf.PI * ring / rings;
                    float longitude = Mathf.PI * 2f * sector / sectors;
                    vertices[index] = new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude),
                        Mathf.Sin(latitude) * Mathf.Sin(longitude));
                    uv[index] = new Vector2((float)sector / sectors, (float)ring / rings);
                    if (ring == rings || sector == sectors)
                        continue;
                    triangles[write++] = index;
                    triangles[write++] = index + 1;
                    triangles[write++] = index + sectors + 1;
                    triangles[write++] = index + 1;
                    triangles[write++] = index + sectors + 2;
                    triangles[write++] = index + sectors + 1;
                }
            Mesh result = new Mesh { name = "Sauce Sphere", vertices = vertices, normals = vertices, uv = uv, triangles = triangles };
            result.RecalculateBounds();
            return result;
        }
        /// <summary>Recycles expired liquid without changing active emission or allocating new bodies.</summary>
        private void LateUpdate()
        {
            foreach (LiquidDroplet droplet in droplets)
                if (droplet != null && droplet.gameObject.activeSelf && Time.time >= droplet.Expires)
                    droplet.Recycle();
                else if (droplet != null && droplet.gameObject.activeSelf && Time.timeScale > 0f)
                    droplet.UpdateShape();
        }
        /// <summary>Releases native geometry and gravity ownership when the pool exits.</summary>
        private void OnDestroy()
        {
            foreach (LiquidDroplet droplet in droplets)
                if (droplet != null)
                    droplet.Recycle();
            if (mesh != null)
                Destroy(mesh);
            if (material != null)
                Destroy(material);
            if (instance == this)
                instance = null;
        }
        /// <summary>Clears retained pool state before a new Play session.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            if (instance != null)
                Destroy(instance.gameObject);
            instance = null;
        }
        #endregion
        #endregion
    }
}
