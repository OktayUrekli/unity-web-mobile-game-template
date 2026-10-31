namespace _Core.Gameplay
{
    /// <summary>
    /// Defines a system that can pause and resume its gameplay logic.
    /// </summary>
    public interface IPausable
    {
        /// <summary>
        /// Stops the system's gameplay logic until <see cref="Resume"/>.
        /// </summary>
        void Pause();

        /// <summary>
        /// Continues the gameplay logic stopped by <see cref="Pause"/>.
        /// </summary>
        void Resume();
    }
}