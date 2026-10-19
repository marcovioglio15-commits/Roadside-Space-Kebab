using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Creates optional inward air streaks as editable prefab content before Play.</summary>
    internal static class BlackHoleAirAuthoring
    {
        #region Methods
        #region Authoring

        /// <summary>Prepares a sphere of air particles that the runtime steers towards the selected collider.</summary>
        /// <param name="owner">Open prefab branch receiving the visual effect.</param>
        /// <param name="settings">Current attraction range and particle speed.</param>
        /// <returns>New editable particle system; the caller stores its hierarchy route.</returns>
        internal static ParticleSystem Build(GameObject owner, BlackHoleSettings settings)
        {
            GameObject root = new GameObject("Black Hole Air");
            Undo.RegisterCreatedObjectUndo(root, "Prepare Black Hole Air");
            root.transform.SetParent(owner.transform, false);
            // Keep the authored emission radius in world metres even on scaled interaction prefabs.
            Vector3 scale = owner.transform.lossyScale;
            if (Mathf.Abs(scale.x) > 0f && Mathf.Abs(scale.y) > 0f && Mathf.Abs(scale.z) > 0f)
                root.transform.localScale = new Vector3(1f / Mathf.Abs(scale.x), 1f / Mathf.Abs(scale.y), 1f / Mathf.Abs(scale.z));
            Collider destination = settings.Resolve(owner.transform);
            if (destination != null)
                root.transform.position = settings.Destination(destination);
            ParticleSystem particles = Undo.AddComponent<ParticleSystem>(root);
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 256;
            main.startLifetime = Mathf.Max(0.5f, settings.Radius / settings.AirSpeed + 0.5f);
            main.startSpeed = 0f;
            main.startSize = 0.03f;
            main.startColor = new Color(0.75f, 0.9f, 1f, 0.4f);
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 45f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = settings.Radius;
            shape.radiusThickness = 0.3f;
            ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2f;
            renderer.velocityScale = 0.08f;
            renderer.sharedMaterial = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.defaultParticleMaterial
                : AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return particles;
        }

        #endregion
        #endregion
    }
}
