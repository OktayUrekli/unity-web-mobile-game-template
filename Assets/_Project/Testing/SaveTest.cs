using _Core.Save;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Project.Testing
{
    /// <summary>
    /// Keyboard harness for <see cref="SaveManager"/> in the test scene: S saves, L loads, D deletes.
    /// </summary>
    public class SaveTest : MonoBehaviour
    {
        private const string SaveKey = "save_test";

        [System.Serializable]
        private class TestData : SaveData
        {
            public int score;
            public string playerName;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.sKey.wasPressedThisFrame)
            {
                Save();
            }

            if (keyboard.lKey.wasPressedThisFrame)
            {
                Load();
            }

            if (keyboard.dKey.wasPressedThisFrame)
            {
                Delete();
            }
        }

        private void Save()
        {
            TestData data = new TestData
            {
                score = 100,
                playerName = "TestPlayer"
            };

            SaveManager.Instance.Save(SaveKey, data);

            Debug.Log("Save test completed.");
        }

        private void Load()
        {
            if (!SaveManager.Instance.Load(SaveKey, out TestData data))
            {
                Debug.Log("No save data found.");
                return;
            }

            Debug.Log(
                $"Load test completed. Score: {data.score}, Player: {data.playerName}");
        }

        private void Delete()
        {
            SaveManager.Instance.Delete(SaveKey);

            Debug.Log("Save deleted.");
        }
    }
}
