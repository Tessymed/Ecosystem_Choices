using UnityEngine;
using UnityEditor;
using System.IO;

public static class RiverSetupTool
{
    [MenuItem("Tools/River/Generate River Audio")]
    public static void GenerateRiverAudio()
    {
        string dir = "Assets/Audio/Environment";
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string path = Path.Combine(dir, "River_Water_Flow_Loop.wav");
        CreateProceduralRiverAudio(path, 12f, 44100);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        // Configure audio importer for VR performance
        AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer != null)
        {
            importer.forceToMono = false; // keep stereo for rich ambient feel
            importer.loadInBackground = true;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        Debug.Log($"[RiverSetupTool] Generated procedural river audio loop at '{path}'.");
    }

    private static void CreateProceduralRiverAudio(string filePath, float durationSeconds, int sampleRate)
    {
        int totalSamples = (int)(durationSeconds * sampleRate);
        short[] leftChannel = new short[totalSamples];
        short[] rightChannel = new short[totalSamples];

        System.Random rand = new System.Random(42);

        // Pink noise generator state
        double b0_L = 0, b1_L = 0, b2_L = 0, b3_L = 0, b4_L = 0, b5_L = 0, b6_L = 0;
        double b0_R = 0, b1_R = 0, b2_R = 0, b3_R = 0, b4_R = 0, b5_R = 0, b6_R = 0;

        // Resonant filter state for low gurgle
        double lowpassL = 0, lowpassR = 0;
        double midL = 0, midR = 0;

        for (int i = 0; i < totalSamples; i++)
        {
            // White noise (-1 to 1)
            double whiteL = rand.NextDouble() * 2.0 - 1.0;
            double whiteR = rand.NextDouble() * 2.0 - 1.0;

            // Paul Kellet's filtered pink noise approximation
            b0_L = 0.99886 * b0_L + whiteL * 0.0555179;
            b1_L = 0.99332 * b1_L + whiteL * 0.0750759;
            b2_L = 0.96900 * b2_L + whiteL * 0.1538520;
            b3_L = 0.86650 * b3_L + whiteL * 0.3104856;
            b4_L = 0.55000 * b4_L + whiteL * 0.5329522;
            b5_L = -0.7616 * b5_L - whiteL * 0.0168980;
            double pinkL = b0_L + b1_L + b2_L + b3_L + b4_L + b5_L + b6_L + whiteL * 0.5362;
            b6_L = whiteL * 0.115926;

            b0_R = 0.99886 * b0_R + whiteR * 0.0555179;
            b1_R = 0.99332 * b1_R + whiteR * 0.0750759;
            b2_R = 0.96900 * b2_R + whiteR * 0.1538520;
            b3_R = 0.86650 * b3_R + whiteR * 0.3104856;
            b4_R = 0.55000 * b4_R + whiteR * 0.5329522;
            b5_R = -0.7616 * b5_R - whiteR * 0.0168980;
            double pinkR = b0_R + b1_R + b2_R + b3_R + b4_R + b5_R + b6_R + whiteR * 0.5362;
            b6_R = whiteR * 0.115926;

            // Gentle water stream frequency shaping (low & mid filters)
            lowpassL += (pinkL * 0.12 - lowpassL) * 0.08;
            lowpassR += (pinkR * 0.12 - lowpassR) * 0.08;

            midL += (pinkL * 0.08 - midL) * 0.22;
            midR += (pinkR * 0.08 - midR) * 0.22;

            // Dynamic bubbling modulation (slow undulating frequencies)
            double t = (double)i / sampleRate;
            double gurgleModL = 0.85 + 0.15 * System.Math.Sin(2.0 * System.Math.PI * 1.8 * t + 0.3)
                                    + 0.10 * System.Math.Sin(2.0 * System.Math.PI * 3.4 * t);
            double gurgleModR = 0.85 + 0.15 * System.Math.Sin(2.0 * System.Math.PI * 1.6 * t + 1.2)
                                    + 0.10 * System.Math.Sin(2.0 * System.Math.PI * 3.1 * t);

            double sampleL = (lowpassL * 1.8 + midL * 0.9) * gurgleModL;
            double sampleR = (lowpassR * 1.8 + midR * 0.9) * gurgleModR;

            // Soft clamp to [-1, 1]
            sampleL = System.Math.Max(-0.95, System.Math.Min(0.95, sampleL));
            sampleR = System.Math.Max(-0.95, System.Math.Min(0.95, sampleR));

            leftChannel[i] = (short)(sampleL * 32767.0);
            rightChannel[i] = (short)(sampleR * 32767.0);
        }

        // Seamless Loop Crossfade over 1.5 seconds at the end
        int crossfadeSamples = (int)(1.5f * sampleRate);
        int startIndex = totalSamples - crossfadeSamples;
        for (int i = 0; i < crossfadeSamples; i++)
        {
            float ratio = (float)i / crossfadeSamples; // 0 to 1
            int endIdx = startIndex + i;
            int startIdx = i;

            double blendL = (1.0 - ratio) * leftChannel[endIdx] + ratio * leftChannel[startIdx];
            double blendR = (1.0 - ratio) * rightChannel[endIdx] + ratio * rightChannel[startIdx];

            leftChannel[endIdx] = (short)blendL;
            rightChannel[endIdx] = (short)blendR;
        }

        // Write WAV File
        using (FileStream fs = new FileStream(filePath, FileMode.Create))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            int byteRate = sampleRate * 2 * 2; // 2 channels * 16 bits
            int blockAlign = 2 * 2;
            int subChunk2Size = totalSamples * 2 * 2;

            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + subChunk2Size);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16); // PCM chunk size
            bw.Write((short)1); // PCM format
            bw.Write((short)2); // 2 channels (Stereo)
            bw.Write(sampleRate);
            bw.Write(byteRate);
            bw.Write((short)blockAlign);
            bw.Write((short)16); // 16 bits per sample

            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(subChunk2Size);

            for (int i = 0; i < totalSamples; i++)
            {
                bw.Write(leftChannel[i]);
                bw.Write(rightChannel[i]);
            }
        }
    }

    [MenuItem("Tools/River/Analyze Terrain Ridges")]
    public static void AnalyzeTerrainRidges()
    {
        Terrain t = Terrain.activeTerrain;
        if (t == null || t.terrainData == null)
        {
            Debug.LogError("[RiverSetupTool] No active terrain found!");
            return;
        }

        TerrainData td = t.terrainData;
        int res = td.heightmapResolution;
        float[,] heights = td.GetHeights(0, 0, res, res);

        float minH = float.MaxValue;
        float maxH = float.MinValue;
        int minX = 0, minZ = 0;

        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float h = heights[z, x];
                if (h < minH)
                {
                    minH = h;
                    minX = x;
                    minZ = z;
                }
                if (h > maxH) maxH = h;
            }
        }

        Vector3 minWorld = t.transform.position + new Vector3((float)minX / (res - 1) * td.size.x, minH * td.size.y, (float)minZ / (res - 1) * td.size.z);
        Debug.Log($"[RiverSetupTool] Terrain '{t.name}' Size: {td.size}. Height Range: {minH * td.size.y:F2}m to {maxH * td.size.y:F2}m. Lowest Point WorldPos: {minWorld}");

        // Sample a cross section along X at Z = 51.1 (the river plane location)
        float zNorm = 51.1f / td.size.z;
        int zSample = Mathf.Clamp((int)(zNorm * (res - 1)), 0, res - 1);
        Debug.Log($"[RiverSetupTool] Sampling Cross-Section at Z={51.1f}m (row {zSample}):");
        for (int xSample = 0; xSample < res; xSample += res / 10)
        {
            float worldX = (float)xSample / (res - 1) * td.size.x;
            float worldY = heights[zSample, xSample] * td.size.y;
            Debug.Log($"   X={worldX:F1}m -> Height={worldY:F2}m");
        }
    }
}
