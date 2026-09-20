namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Chooses whether the editing target comes directly from a Body or through a master.</summary>
    internal enum PlayerStudioSourceMode
    {
        /// <summary>Edits a Body asset independently of any master.</summary>
        Body,

        /// <summary>Edits the Body currently assigned to the selected master's active slot.</summary>
        Master
    }
}
