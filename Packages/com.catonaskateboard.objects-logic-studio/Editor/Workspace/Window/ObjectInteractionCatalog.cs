using System;
using UnityEngine;
using Categories = CatOnASkateboard.ObjectsLogicStudio.Editor.ObjectStudioNavigation.Categories;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Defines every supported interaction once for menus, search and preset capture.</summary>
    internal static class ObjectInteractionCatalog
    {
        #region Types

        /// <summary>Connects a runtime component to its display name, category and common search terms.</summary>
        internal readonly struct Entry
        {
            internal readonly Type Type;
            internal readonly string Name;
            internal readonly Categories Category;
            internal readonly string Keywords;

            #region Methods

            /// <summary>Builds one immutable interaction description.</summary>
            /// <param name="type">Supported runtime component.</param>
            /// <param name="name">Short English label.</param>
            /// <param name="category">Category used by the filter menu.</param>
            /// <param name="keywords">Extra words accepted by search.</param>
            internal Entry(Type type, string name, Categories category, string keywords)
            {
                Type = type;
                Name = name;
                Category = category;
                Keywords = keywords;
            }

            #endregion
        }

        #endregion

        #region Definitions

        internal static readonly Entry[] Entries =
        {
            new Entry(typeof(ObjectGrab), "Grab", Categories.Inventory, "pick up pickup carry hand"),
            new Entry(typeof(ObjectDrop), "Drop", Categories.Inventory, "release put down"),
            new Entry(typeof(ObjectThrow), "Throw", Categories.Inventory, "launch force trajectory"),
            new Entry(typeof(ObjectDispenser), "Dispenser", Categories.Inventory, "supply output refill stock"),
            new Entry(typeof(ObjectContainer), "Container", Categories.Inventory, "store inventory capacity deposit"),
            new Entry(typeof(ObjectTriggerAnimation), "Trigger Animation", Categories.Physics, "transform pose rotate return"),
            new Entry(typeof(ObjectEject), "Eject", Categories.Physics, "impulse black hole attraction"),
            new Entry(typeof(ObjectGravityGenerator), "Gravity Generator", Categories.Physics, "suspension weight force"),
            new Entry(typeof(ObjectElasticDeformation), "Elastic Deformation", Categories.Physics, "bend squash stretch recovery"),
            new Entry(typeof(ObjectContactModifier), "Modify by Contact", Categories.Processes, "cook consume snap price contact"),
            new Entry(typeof(ObjectSlice), "Slice", Categories.Processes, "cut sequence outputs"),
            new Entry(typeof(ObjectDegradation), "Degradation", Categories.Processes, "stages damage decay"),
            new Entry(typeof(ObjectSpraySauce), "Spray Sauce", Categories.Processes, "liquid stream droplets bottle"),
            new Entry(typeof(ObjectDirtTrail), "Dirt Trail", Categories.Processes, "surface marks wetness"),
            new Entry(typeof(ObjectDialogue), "Dialogue", Categories.Dialogue, "text pages entries tree speech"),
            new Entry(typeof(ObjectAvailableOrders), "Available Orders", Categories.Dialogue, "catalog recipes timer board"),
            new Entry(typeof(ObjectAssemblyStation), "Assembly Station", Categories.Assembly, "table magnets ingredients"),
            new Entry(typeof(ObjectAssemblyProduct), "Assembly Product", Categories.Assembly, "recipe ingredients finalize"),
            new Entry(typeof(ObjectSpawnManager), "Spawn Management", Categories.Spawning, "outputs day flow sources"),
            new Entry(typeof(ObjectDeathbox), "Deathbox", Categories.Spawning, "respawn return death"),
            new Entry(typeof(ObjectPresenceCap), "Presence Cap", Categories.Spawning, "population limit offscreen despawn"),
            new Entry(typeof(ObjectHover), "Hover", Categories.Feedback, "cursor label text detection"),
            new Entry(typeof(ObjectOutline), "Outline", Categories.Feedback, "edges highlight renderers"),
            new Entry(typeof(ObjectAmbient), "Play Ambient", Categories.Feedback, "radio audio playlist sound"),
            new Entry(typeof(ObjectInteractionUnlock), "Unlock", Categories.Feedback, "lock conditions availability rules")
        };

        #endregion

        #region Methods

        #region Lookup

        /// <summary>Orders required interaction components before the features that request them.</summary>
        /// <param name="type">Registered interaction type.</param>
        /// <returns>Creation priority shared by staged edits and master imports.</returns>
        internal static int Priority(Type type)
        {
            return type == typeof(ObjectGrab) || type == typeof(ObjectElasticDeformation) ? 0 : 1;
        }

        /// <summary>Finds metadata for one exact component type without scene searches.</summary>
        /// <param name="type">Runtime component type.</param>
        /// <returns>The registered entry, or an empty entry for unsupported types.</returns>
        internal static Entry Find(Type type)
        {
            foreach (Entry entry in Entries)
                if (entry.Type == type)
                    return entry;
            return default;
        }

        /// <summary>Returns the familiar label for a configured interaction.</summary>
        /// <param name="interaction">Existing component.</param>
        /// <returns>The registered label or component name.</returns>
        internal static string Name(ObjectInteraction interaction)
        {
            return interaction != null ? Find(interaction.GetType()).Name ?? interaction.GetType().Name : "Interaction";
        }

        #endregion

        #endregion
    }
}
