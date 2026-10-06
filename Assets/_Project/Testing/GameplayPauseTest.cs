using _Core.Gameplay;
using UnityEngine;

namespace _Project.Testing
{
    /// <summary>
    /// Logs the Pause/Resume calls a <see cref="GameplayPauseHandler"/> receives (pause menu, ads, app in the background).
    /// </summary>
    public class GameplayPauseTest : GameplayPauseHandler
    {
        public override void Pause()
        {
            Debug.Log("GameplayPauseTest: Pause received.");
        }

        public override void Resume()
        {
            Debug.Log("GameplayPauseTest: Resume received.");
        }
    }
}
