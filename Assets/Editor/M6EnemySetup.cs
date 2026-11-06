using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SheltreSim.Enemies;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// Spawns M6 enemies into the existing outdoor area (same single scene as the rest
    /// of the project — no separate Unity scene). Run after
    /// "Tools > SheltreSim > Build M1 Bunker Scene" and
    /// "Tools > SheltreSim > Setup M4 Outdoor Area" have already produced a scene with
    /// a "Player" GameObject and an "OutdoorArea" parent.
    /// </summary>
    public static class M6EnemySetup
    {
        [MenuItem("Tools/SheltreSim/Setup M6 Enemies")]
        public static void SetupEnemies()
        {
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("No 'Player' GameObject found — run Tools > SheltreSim > Build M1 Bunker Scene first.");
                return;
            }

            GameObject outdoorArea = GameObject.Find("OutdoorArea");
            if (outdoorArea == null)
            {
                Debug.LogError("No 'OutdoorArea' found — run Tools > SheltreSim > Setup M4 Outdoor Area first.");
                return;
            }

            SpawnEnemy("Raider_1", new Vector3(0f, 1f, -11f), outdoorArea.transform);
            SpawnEnemy("Raider_2", new Vector3(8f, 1f, -20f), outdoorArea.transform);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("M6 enemies ready. 2 raiders patrol the outdoor area — they'll detect, chase, and attack on sight. There is currently no way to fight back; avoid or flee.");
        }

        private static void SpawnEnemy(string name, Vector3 position, Transform parent)
        {
            if (GameObject.Find(name) != null)
            {
                return;
            }

            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = name;
            enemy.transform.SetParent(parent, false);
            enemy.transform.position = position;
            enemy.AddComponent<EnemyAI>();
        }
    }
}
