namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Provides the input driver with a common contract for actions independent of the carry slot.</summary>
    public abstract class ObjectCommandInteraction : ObjectTargetedInteraction
    {
        #region Methods

        #region Execution

        /// <summary>Checks availability without starting motion or modifying contacted objects.</summary>
        /// <returns>True when this command may compete for a performed input action.</returns>
        internal abstract bool CanExecute();

        /// <summary>Commits the selected command after the shared driver validates reach and visibility.</summary>
        /// <returns>True when the input performed an actual operation.</returns>
        internal abstract bool Execute();

        #endregion

        #endregion
    }
}
