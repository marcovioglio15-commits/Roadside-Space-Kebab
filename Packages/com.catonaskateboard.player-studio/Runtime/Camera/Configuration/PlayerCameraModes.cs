namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Places the view at the target, behind it or at a fixed world pose.</summary>
    public enum PlayerCameraMode { FirstPerson, ThirdPerson, Fixed }

    /// <summary>Selects immediate positioning or exponential smoothing.</summary>
    public enum PlayerCameraFollow { Direct, Damped }

    /// <summary>Selects the horizontal frame used to interpret movement input.</summary>
    public enum PlayerMovementFrame { World, Player, Camera }

    /// <summary>Selects visual yaw without rotating the collision body.</summary>
    public enum PlayerModelFacing { Authored, Movement, Camera }
}
