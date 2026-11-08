using SheltreSim.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// M14 — Opus-model review: raiders are Destroy()d on death with no spawner, so clearing
    /// every raider once leaves the world permanently pressure-free. Run AFTER raiders already
    /// exist in the scene (both "Tools > SheltreSim > Setup M6 Enemies" and M12's biome blend
    /// pass spawn raiders) — this tool doesn't create any raiders itself, it just drops a single
    /// <see cref="EnemySpawner"/> into the scene, which finds every existing raider for itself
    /// at Play-mode Start and takes over respawning for all of them.
    /// </summary>
    public static class M14RaiderRespawnSetup
    {
        [MenuItem("Tools/SheltreSim/Setup M14 Raider Respawn")]
        public static void SetupRaiderRespawn()
        {
            GameObject outdoorArea = GameObject.Find("OutdoorArea");
            Transform parent = outdoorArea != null ? outdoorArea.transform : null;

            GameObject manager = GameObject.Find("RaiderRespawnManager");
            if (manager == null)
            {
                manager = new GameObject("RaiderRespawnManager");
                if (parent != null)
                {
                    manager.transform.SetParent(parent, false);
                }
            }

            EnemySpawner spawner = manager.GetComponent<EnemySpawner>();
            if (spawner == null)
            {
                spawner = manager.AddComponent<EnemySpawner>();
            }

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log($"M14 raider respawn ready. '{manager.name}' carries an EnemySpawner that will find every raider present at Play-mode Start and respawn each one {spawner.respawnDelaySeconds}s after it dies, indefinitely.");
        }
    }
}
