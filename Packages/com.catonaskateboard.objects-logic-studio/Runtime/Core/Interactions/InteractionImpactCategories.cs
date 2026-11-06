using CatOnASkateboard.PlayerStudio;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Maps existing interaction cards to the categories used by weapon impact filters.</summary>
    internal static class InteractionImpactCategories
    {
        #region Methods

        #region Categories

        /// <summary>Identifies the existing card without inspecting its runtime type metadata.</summary>
        /// <param name="interaction">Enabled component on the struck object.</param>
        /// <returns>The category for this card, or None for an unlisted interaction.</returns>
        internal static PlayerToolImpactInteraction Get(ObjectInteraction interaction)
        {
            return interaction switch
            {
                ObjectSingleInteraction single => GetSingle(single.Kind),
                ObjectExtendedInteraction extended => GetExtended(extended.Kind),
                ObjectHover => PlayerToolImpactInteraction.Hover,
                _ => PlayerToolImpactInteraction.None
            };
        }

        /// <summary>Maps input cards through their stable interaction kind.</summary>
        /// <param name="kind">Kind used by the single interaction card.</param>
        /// <returns>The matching impact category.</returns>
        private static PlayerToolImpactInteraction GetSingle(SingleInteractionKind kind)
        {
            return kind switch
            {
                SingleInteractionKind.Grab => PlayerToolImpactInteraction.Grab,
                SingleInteractionKind.Drop => PlayerToolImpactInteraction.Drop,
                SingleInteractionKind.Throw => PlayerToolImpactInteraction.Throw,
                SingleInteractionKind.Dispenser => PlayerToolImpactInteraction.Dispenser,
                SingleInteractionKind.Container => PlayerToolImpactInteraction.Container,
                SingleInteractionKind.TriggerAnimation => PlayerToolImpactInteraction.TriggerAnimation,
                SingleInteractionKind.Eject => PlayerToolImpactInteraction.Eject,
                _ => PlayerToolImpactInteraction.None
            };
        }

        /// <summary>Maps extended and passive cards through their stable interaction kind.</summary>
        /// <param name="kind">Kind used by the extended interaction card.</param>
        /// <returns>The matching impact category.</returns>
        private static PlayerToolImpactInteraction GetExtended(ExtendedInteractionKind kind)
        {
            return kind switch
            {
                ExtendedInteractionKind.ModifyByContact => PlayerToolImpactInteraction.ModifyByContact,
                ExtendedInteractionKind.Dialogue => PlayerToolImpactInteraction.Dialogue,
                ExtendedInteractionKind.Outline => PlayerToolImpactInteraction.Outline,
                ExtendedInteractionKind.Unlock => PlayerToolImpactInteraction.Unlock,
                ExtendedInteractionKind.AssemblyStation => PlayerToolImpactInteraction.AssemblyStation,
                ExtendedInteractionKind.AssemblyProduct => PlayerToolImpactInteraction.AssemblyProduct,
                ExtendedInteractionKind.SpawnManagement => PlayerToolImpactInteraction.SpawnManagement,
                ExtendedInteractionKind.Slice => PlayerToolImpactInteraction.Slice,
                ExtendedInteractionKind.PlayAmbient => PlayerToolImpactInteraction.PlayAmbient,
                ExtendedInteractionKind.AvailableOrders => PlayerToolImpactInteraction.AvailableOrders,
                ExtendedInteractionKind.ObjectDegradation => PlayerToolImpactInteraction.ObjectDegradation,
                ExtendedInteractionKind.GravityGenerator => PlayerToolImpactInteraction.GravityGenerator,
                ExtendedInteractionKind.ElasticDeformation => PlayerToolImpactInteraction.ElasticDeformation,
                ExtendedInteractionKind.DirtTrail => PlayerToolImpactInteraction.DirtTrail,
                ExtendedInteractionKind.SpraySauce => PlayerToolImpactInteraction.SpraySauce,
                ExtendedInteractionKind.Deathbox => PlayerToolImpactInteraction.Deathbox,
                ExtendedInteractionKind.PresenceCap => PlayerToolImpactInteraction.PresenceCap,
                _ => PlayerToolImpactInteraction.None
            };
        }

        #endregion

        #endregion
    }
}
