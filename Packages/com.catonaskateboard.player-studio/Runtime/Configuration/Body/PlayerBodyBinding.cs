namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Chooses whether the host only reads Body data or also configures a native capsule.</summary>
    public enum PlayerBodyBinding
    {
        /// <summary>Reads the preset without changing any existing scene component.</summary>
        ConfigurationOnly,

        /// <summary>Applies Body dimensions to an explicitly assigned CharacterController.</summary>
        CharacterController
    }
}
