using UnityEditor;
using UnityEngine;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// Import settings for everything ArtSource/audio generates. Music streams, so it isn't held
    /// in memory; effects are decoded up front, so a footstep has no decode latency.
    /// </summary>
    public class AudioImportSettings : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Audio/")) return;

            AudioImporter importer = (AudioImporter)assetImporter;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;

            if (name.StartsWith("music_"))
            {
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.55f;
            }
            else if (name == "amb_bunker" || name == "generator_hum" || name == "machine_hum" || name == "fan_loop" || name == "turret_laser" ||
                     name == "motor_buggy" || name == "motor_van" || name == "bike_chain")
            {
                // A pure mains hum exposes any gap at the loop point, so no Vorbis encoder delay here.
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.ADPCM;
            }
            else if (IsAmbienceLoop(name))
            {
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.5f;
            }
            else
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
            }

            importer.defaultSampleSettings = settings;
            importer.loadInBackground = false;
        }

        private static bool IsAmbienceLoop(string name) =>
            name == "amb_wasteland" || name == "amb_forest" || name == "amb_desert" ||
            name == "amb_snow" || name == "amb_marsh" || name == "amb_highlands" ||
            name == "amb_rain" || name == "amb_night";
    }
}
