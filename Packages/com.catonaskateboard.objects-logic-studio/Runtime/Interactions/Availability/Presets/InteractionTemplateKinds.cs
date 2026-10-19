namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Shares explicit interaction type mappings between local runtime bindings and preset authoring.</summary>
    public static class InteractionTemplateKinds
    {
        #region Methods

        #region Mapping

        /// <summary>Maps supported components explicitly so presets never depend on runtime reflection.</summary>
        /// <param name="feature">Existing ordinary interaction.</param>
        /// <returns>The stable serialized interaction kind, or null for unsupported components.</returns>
        public static InteractionTemplateKind? Of(ObjectInteraction feature)
        {
            // Rules cannot reference other rules, matching the existing unlock validation contract.
            return feature switch
            {
                ObjectHover => InteractionTemplateKind.Hover,
                ObjectGrab => InteractionTemplateKind.Grab,
                ObjectDrop => InteractionTemplateKind.Drop,
                ObjectThrow => InteractionTemplateKind.Throw,
                ObjectDispenser => InteractionTemplateKind.Dispenser,
                ObjectContainer => InteractionTemplateKind.Container,
                ObjectDialogue => InteractionTemplateKind.Dialogue,
                ObjectSlice => InteractionTemplateKind.Slice,
                ObjectContactModifier => InteractionTemplateKind.Contact,
                ObjectAvailableOrders => InteractionTemplateKind.AvailableOrders,
                ObjectDegradation => InteractionTemplateKind.ObjectDegradation,
                ObjectGravityGenerator => InteractionTemplateKind.GravityGenerator,
                ObjectElasticDeformation => InteractionTemplateKind.ElasticDeformation,
                ObjectDirtTrail => InteractionTemplateKind.DirtTrail,
                ObjectDeathbox => InteractionTemplateKind.Deathbox,
                ObjectPresenceCap => InteractionTemplateKind.PresenceCap,
                ObjectSpraySauce => InteractionTemplateKind.SpraySauce,
                ObjectTriggerAnimation => InteractionTemplateKind.TriggerAnimation,
                ObjectEject => InteractionTemplateKind.Eject,
                ObjectAmbient => InteractionTemplateKind.Ambient,
                ObjectOutline => InteractionTemplateKind.Outline,
                ObjectSpawnManager => InteractionTemplateKind.Spawn,
                ObjectAssemblyStation => InteractionTemplateKind.AssemblyStation,
                ObjectAssemblyProduct => InteractionTemplateKind.AssemblyProduct,
                _ => null
            };
        }


        #endregion

        #endregion
    }
}
