namespace CatOnASkateboard.MenuStudio
{
    /// <summary>Selects the menu structure created in a scene.</summary>
    public enum MenuKind { Main, Pause }
    /// <summary>Identifies preauthored panels within a menu host.</summary>
    public enum MenuPageKind { Home, Settings, Credits, Confirmation }
    /// <summary>Identifies portable commands emitted by menu controls.</summary>
    public enum MenuCommand { Play, Resume, Restart, Settings, Credits, MainMenu, Quit, Back, ConfirmQuit }
    /// <summary>Selects transform feedback, clip feedback or their combination.</summary>
    public enum MenuMotionMode { None, Transform, Clips, TransformAndClips }
    /// <summary>Selects the RectTransform affected by interaction motion.</summary>
    public enum MenuMotionTarget { WholeButton, Content }
    /// <summary>Selects the authored visual content used by menu buttons.</summary>
    public enum MenuContentMode { Text, Image }
    /// <summary>Identifies graphic states shared by pointer and keyboard navigation.</summary>
    public enum MenuVisualPhase { Normal, Hover, Pressed, Disabled }
}
