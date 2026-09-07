using System.IO;

/// <summary>
/// Creates a WAV file byte array from raw 16-bit PCM audio data provided by FMOD.
/// </summary>
public static class SaveWav
{
    public static byte[] SaveFromPCM16(byte[] pcmData, int sampleRate, int channels)
    {
        if (pcmData == null || pcmData.Length == 0) return null;

        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            // Write the standard WAV Header
            writer.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"));
            writer.Write(36 + pcmData.Length); // File size minus 8 bytes
            writer.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.UTF8.GetBytes("fmt "));
            writer.Write(16); // Subchunk1Size (16 for PCM)
            writer.Write((short)1); // AudioFormat (1 for PCM)
            writer.Write((short)channels);
            writer.Write(sampleRate); // SampleRate
            writer.Write(sampleRate * channels * 2); // ByteRate
            writer.Write((short)(channels * 2)); // BlockAlign
            writer.Write((short)16); // BitsPerSample
            writer.Write(System.Text.Encoding.UTF8.GetBytes("data"));
            writer.Write(pcmData.Length); // Subchunk2Size

            // Write the raw PCM data we got from FMOD
            writer.Write(pcmData);

            // Return the complete memory stream as a byte array for Whisper
            return stream.ToArray();
        }
    }
}