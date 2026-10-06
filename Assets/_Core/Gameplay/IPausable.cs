namespace _Core.Gameplay
{
    /// <summary>
    /// Defines a system that can pause and resume its gameplay logic.
    /// </summary>
    public interface IPausable
    {
        void Pause();
        void Resume();
    }
}