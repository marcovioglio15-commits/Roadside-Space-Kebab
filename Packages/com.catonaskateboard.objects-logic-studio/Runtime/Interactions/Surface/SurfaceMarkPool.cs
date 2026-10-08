using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Retains finite surface deposits independently of their sources with bounded reusable world geometry.</summary>
    [AddComponentMenu("")]
    internal sealed class SurfaceMarkPool : MonoBehaviour
    {
        #region State

        private const int capacity = 256;
        private static SurfaceMarkPool instance;
        private readonly SurfaceMark[] marks = new SurfaceMark[capacity];
        private readonly SurfaceMarkGeometry geometry = new SurfaceMarkGeometry();
        private MaterialPropertyBlock properties;
        private Material material;
        private int cursor;
        private static readonly int colorId = Shader.PropertyToID("DepositColor");
        private static readonly int blendId = Shader.PropertyToID("DepositBlend");
        private static readonly int shapeId = Shader.PropertyToID("DepositShape");
        private static readonly int surfaceId = Shader.PropertyToID("DepositSurface");
        private static readonly int timingId = Shader.PropertyToID("DepositTiming");

        #endregion
        #region Methods
        #region Emission

        /// <summary>Deposits a bounded group of patches using a palette prepared at this contact boundary.</summary>
        /// <param name="receiver">Exact contacted surface.</param>
        /// <param name="point">World-space contact position.</param>
        /// <param name="normal">Outward surface normal.</param>
        /// <param name="direction">Motion direction projected onto the receiving surface.</param>
        /// <param name="settings">Validated shared mark configuration.</param>
        /// <param name="palette">Current material or manual colours.</param>
        internal static void Deposit(Collider receiver, Vector3 point, Vector3 normal, Vector3 direction,
            SurfaceTrailSettings settings, SurfacePalette palette)
        {
            if (receiver == null || !receiver.enabled || !receiver.gameObject.activeInHierarchy || receiver.isTrigger)
                return;
            if (instance == null)
            {
                GameObject root = new GameObject("Object Surface Marks");
                DontDestroyOnLoad(root);
                instance = root.AddComponent<SurfaceMarkPool>();
                instance.Initialize();
            }
            if (instance.material == null)
                return;
            for (int index = 0; index < settings.Patches; index++)
                instance.Emit(receiver, point, normal.normalized, direction, settings, palette);
        }

        /// <summary>Allocates the bounded pool once instead of constructing renderers at every impact.</summary>
        private void Initialize()
        {
            properties = new MaterialPropertyBlock();
            Shader shader = Resources.Load<Shader>("SurfaceDeposit");
            if (shader == null)
            {
                Debug.LogWarning("Surface deposits require the Objects Logic Studio/Surface Deposit shader.", this);
                return;
            }
            material = new Material(shader) { name = "Surface Deposit Runtime" };
            for (int index = 0; index < marks.Length; index++)
            {
                GameObject root = new GameObject("Surface Mark");
                root.layer = 2;
                root.SetActive(false);
                root.transform.SetParent(transform, false);
                marks[index] = root.AddComponent<SurfaceMark>();
                marks[index].Initialize(material);
            }
        }

        /// <summary>Finds a free slot without abruptly replacing a still-visible deposit.</summary>
        /// <param name="receiver">Contact collider retained until expiration.</param>
        /// <param name="point">Contact position.</param>
        /// <param name="normal">Receiver normal.</param>
        /// <param name="direction">Surface motion direction.</param>
        /// <param name="settings">Validated geometry and fade configuration.</param>
        /// <param name="palette">Current colour composition.</param>
        private void Emit(Collider receiver, Vector3 point, Vector3 normal, Vector3 direction, SurfaceTrailSettings settings, SurfacePalette palette)
        {
            for (int attempt = 0; attempt < marks.Length; attempt++)
            {
                SurfaceMark mark = marks[cursor];
                cursor = (cursor + 1) % marks.Length;
                if (mark == null || mark.Receiver != null && mark.Expires > Time.time)
                    continue;
                if (!geometry.Build(mark.Mesh, receiver, point, normal, direction, settings))
                    return;
                mark.Receiver = receiver;
                mark.Expires = Time.time + settings.Lifetime;
                properties.Clear();
                properties.SetColor(colorId, palette.Sample());
                properties.SetColor(blendId, palette.Sample());
                properties.SetVector(shapeId, new Vector4((float)settings.Shape, settings.Irregularity, Random.Range(0f, 100f), 0f));
                properties.SetVector(surfaceId, new Vector4(settings.Liquidity, settings.Gloss, settings.ColorBlend, settings.Opacity));
                properties.SetVector(timingId, new Vector4(Time.time, settings.Lifetime, settings.FadeDuration, 0f));
                mark.Renderer.SetPropertyBlock(properties);
                mark.Renderer.GetPropertyBlock(mark.Properties);
                mark.gameObject.SetActive(true);
                return;
            }
        }

        #endregion
        #region Lifetime

        /// <summary>Recycles fully faded marks; shader time performs the actual smooth fade.</summary>
        private void LateUpdate()
        {
            foreach (SurfaceMark mark in marks)
                if (mark != null && mark.gameObject.activeSelf
                    && (mark.Receiver == null || !mark.Receiver.enabled || !mark.Receiver.gameObject.activeInHierarchy || Time.time >= mark.Expires))
                {
                    mark.gameObject.SetActive(false);
                    mark.Receiver = null;
                }
                else if (mark != null && mark.gameObject.activeSelf)
                    Graphics.DrawMesh(mark.Mesh, mark.Receiver.transform.localToWorldMatrix, material, mark.Receiver.gameObject.layer,
                        null, 0, mark.Properties, UnityEngine.Rendering.ShadowCastingMode.Off, true);
        }

        /// <summary>Destroys pooled geometry even when an active mark currently follows another scene object.</summary>
        private void OnDestroy()
        {
            foreach (SurfaceMark mark in marks)
                if (mark != null)
                    Destroy(mark.gameObject);
            if (material != null)
                Destroy(material);
            if (instance == this)
                instance = null;
        }

        /// <summary>Resets the pool at Play entry when managed static state is retained.</summary>
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
