using UnityEngine;
using UnityEngine.Rendering;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Reuses bounded surface geometry, merging repeated wetting and fading temporary marks before saturation.</summary>
    [AddComponentMenu("")]
    internal sealed class SurfaceMarkPool : MonoBehaviour
    {
        #region State
        private const int capacity = 1024;
        private const int reserve = 256;
        private static SurfaceMarkPool instance;
        private readonly SurfaceMark[] marks = new SurfaceMark[capacity];
        private readonly SurfaceMarkGeometry geometry = new SurfaceMarkGeometry();
        private Material material;
        private int cursor;
        private int active;
        private bool warned;
        private static readonly int colorId = Shader.PropertyToID("DepositColor");
        private static readonly int blendId = Shader.PropertyToID("DepositBlend");
        private static readonly int shapeId = Shader.PropertyToID("DepositShape");
        private static readonly int surfaceId = Shader.PropertyToID("DepositSurface");
        private static readonly int timingId = Shader.PropertyToID("DepositTiming");
        private static readonly int retirementId = Shader.PropertyToID("DepositRetirement");
        #endregion
        #region Methods
        #region Emission
        /// <summary>Deposits a bounded group of patches using ordinary or independently overridden timing.</summary>
        /// <param name="receiver">Exact contacted surface.</param>
        /// <param name="point">World-space contact position.</param>
        /// <param name="normal">Outward surface normal.</param>
        /// <param name="direction">Motion projected onto the receiving surface.</param>
        /// <param name="settings">Validated shared mark configuration.</param>
        /// <param name="palette">Current material or manual colours.</param>
        /// <param name="timing">Optional receiver-specific lifetime.</param>
        /// <returns>True when a mark was emitted or an existing wet patch refreshed.</returns>
        internal static bool Deposit(Collider receiver, Vector3 point, Vector3 normal, Vector3 direction,
            SurfaceTrailSettings settings, SurfacePalette palette, SurfaceMarkTiming? timing = null)
        {
            if (receiver == null || !receiver.enabled || !receiver.gameObject.activeInHierarchy || receiver.isTrigger)
                return false;
            if (instance == null)
            {
                GameObject root = new GameObject("Object Surface Marks");
                DontDestroyOnLoad(root);
                instance = root.AddComponent<SurfaceMarkPool>();
                instance.Initialize();
            }
            if (instance.material == null)
                return false;
            SurfaceMarkTiming duration = timing ?? new SurfaceMarkTiming(settings);
            if (instance.Rewet(receiver, point, normal, settings, palette, duration))
                return true;
            bool emitted = false;
            for (int index = 0; index < settings.Patches; index++)
                emitted |= instance.Emit(receiver, point, normal.normalized, direction, settings, palette, duration);
            return emitted;
        }

        /// <summary>Prepares managed slots once; native meshes are created lazily and reused.</summary>
        private void Initialize()
        {
            Shader shader = Resources.Load<Shader>("SurfaceDeposit");
            if (shader == null)
            {
                Debug.LogWarning("Surface deposits require the Objects Logic Studio/Surface Deposit shader.", this);
                return;
            }
            material = new Material(shader) { name = "Surface Deposit Runtime" };
            for (int index = 0; index < marks.Length; index++)
                marks[index] = new SurfaceMark();
        }

        /// <summary>Refreshes compatible nearby residue instead of spending a slot on every stationary hit.</summary>
        /// <param name="receiver">Receiving surface.</param>
        /// <param name="point">New contact position.</param>
        /// <param name="normal">Outward contact normal.</param>
        /// <param name="settings">Shared appearance identity.</param>
        /// <param name="palette">Emitter palette identity.</param>
        /// <param name="timing">Required lifetime policy.</param>
        /// <returns>True when an existing patch covers the contact.</returns>
        private bool Rewet(Collider receiver, Vector3 point, Vector3 normal, SurfaceTrailSettings settings,
            SurfacePalette palette, SurfaceMarkTiming timing)
        {
            float distance = settings.Radius.x * 0.5f;
            foreach (SurfaceMark mark in marks)
                if (mark.Receiver == receiver && !mark.Retiring && mark.Expires > Time.time
                    && mark.Settings == settings && mark.Palette == palette && mark.Timing.Matches(timing)
                    && Vector3.Dot(receiver.transform.TransformDirection(mark.LocalNormal), normal) > 0.95f
                    && (receiver.transform.TransformPoint(mark.LocalPoint) - point).sqrMagnitude <= distance * distance)
                {
                    Wet(mark, timing);
                    return true;
                }
            return false;
        }

        /// <summary>Projects one patch into a free slot while reserving capacity for incoming contacts.</summary>
        /// <param name="receiver">Contact collider retained until expiration.</param>
        /// <param name="point">Contact position.</param>
        /// <param name="normal">Receiver normal.</param>
        /// <param name="direction">Surface motion direction.</param>
        /// <param name="settings">Validated geometry configuration.</param>
        /// <param name="palette">Current colour composition.</param>
        /// <param name="timing">Resolved lifetime.</param>
        /// <returns>True when projection found a visible surface.</returns>
        private bool Emit(Collider receiver, Vector3 point, Vector3 normal, Vector3 direction,
            SurfaceTrailSettings settings, SurfacePalette palette, SurfaceMarkTiming timing)
        {
            for (int attempt = 0; attempt < marks.Length; attempt++)
            {
                SurfaceMark mark = marks[cursor];
                cursor = (cursor + 1) % marks.Length;
                if (mark.Receiver != null && mark.Expires > Time.time && mark.Receiver.enabled && mark.Receiver.gameObject.activeInHierarchy)
                    continue;
                Release(mark);
                mark.Prepare();
                if (!geometry.Build(mark.Mesh, receiver, point, normal, direction, settings))
                    return false;
                mark.Receiver = receiver;
                mark.Settings = settings;
                mark.Palette = palette;
                mark.LocalPoint = receiver.transform.InverseTransformPoint(point);
                mark.LocalNormal = receiver.transform.InverseTransformDirection(normal);
                mark.Retiring = false;
                mark.Properties.Clear();
                mark.Properties.SetColor(colorId, palette.Sample());
                mark.Properties.SetColor(blendId, palette.Sample());
                mark.Properties.SetVector(shapeId, new Vector4((float)settings.Shape, settings.Irregularity, Random.Range(0f, 100f), 0f));
                mark.Properties.SetVector(surfaceId, new Vector4(settings.Liquidity, settings.Gloss, settings.ColorBlend, settings.Opacity));
                Wet(mark, timing);
                active++;
                if (active >= capacity - reserve)
                    RetireOldest();
                return true;
            }
            if (!warned && !RetireOldest())
            {
                warned = true;
                Debug.LogWarning("Surface deposits reached the permanent-mark budget. Existing permanent marks are preserved; nearby wetting still refreshes them.", this);
            }
            return false;
        }
        #endregion
        #region Lifetime
        /// <summary>Restarts a deposit's lifetime without rebuilding its surface geometry.</summary>
        /// <param name="mark">New or repeatedly wetted patch.</param>
        /// <param name="timing">Resolved lifetime policy.</param>
        private static void Wet(SurfaceMark mark, SurfaceMarkTiming timing)
        {
            mark.Timing = timing;
            mark.WetTime = Time.time;
            mark.Expires = timing.Permanent ? float.PositiveInfinity : Time.time + timing.Lifetime;
            mark.Properties.SetVector(timingId, timing.ShaderValue(Time.time));
        }

        /// <summary>Starts an additional short fade before temporary deposits exhaust the reserve.</summary>
        /// <returns>True when at least one temporary deposit can free capacity soon.</returns>
        private bool RetireOldest()
        {
            SurfaceMark oldest = null;
            bool retiring = false;
            foreach (SurfaceMark mark in marks)
                if (mark.Settings != null && !mark.Timing.Permanent)
                {
                    retiring |= mark.Retiring;
                    if (!mark.Retiring && (oldest == null || mark.WetTime < oldest.WetTime))
                        oldest = mark;
                }
            if (oldest == null)
                return retiring;
            oldest.Retiring = true;
            oldest.Expires = Mathf.Min(oldest.Expires, Time.time + 0.12f);
            oldest.Properties.SetVector(retirementId, new Vector4(Time.time, 0.12f, 0f, 0f));
            return true;
        }

        /// <summary>Releases references once while preserving reusable geometry.</summary>
        /// <param name="mark">Expired or inactive receiver slot.</param>
        private void Release(SurfaceMark mark)
        {
            if (mark.Settings == null)
                return;
            active--;
            mark.Receiver = null;
            mark.Settings = null;
            mark.Palette = null;
            mark.Retiring = false;
        }

        /// <summary>Draws live marks in receiver-local space and returns inactive surfaces to the pool.</summary>
        private void LateUpdate()
        {
            foreach (SurfaceMark mark in marks)
                if (mark != null && mark.Settings != null)
                {
                    if (mark.Receiver == null || !mark.Receiver.enabled || !mark.Receiver.gameObject.activeInHierarchy || Time.time >= mark.Expires)
                        Release(mark);
                    else
                        Graphics.DrawMesh(mark.Mesh, mark.Receiver.transform.localToWorldMatrix, material, mark.Receiver.gameObject.layer,
                            null, 0, mark.Properties, ShadowCastingMode.Off, true);
                }
        }

        /// <summary>Releases native resources owned by this pool, including inactive slots.</summary>
        private void OnDestroy()
        {
            foreach (SurfaceMark mark in marks)
                mark?.Dispose();
            if (material != null)
                Destroy(material);
            if (instance == this)
                instance = null;
        }

        /// <summary>Clears residue at Play entry when managed static state is retained.</summary>
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
