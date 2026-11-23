using System;
using System.IO;
using UnityEngine;

namespace NED2024.Testing
{
    /// <summary>
    /// Records exactly what the player hears (the AudioListener's mix) into a 16-bit WAV file.
    /// It only runs when the game is started with "-recordaudio &lt;file.wav&gt;" and is used to make the gameplay video.
    /// A small text file next to the WAV stores the UTC time of the first audio block so it can be lined up with the screen capture.
    /// </summary>
    public class AudioRecorder : MonoBehaviour
    {
        private static FileStream stream;
        private static readonly object gate = new object();
        private static int sampleRate, channels;
        private static long dataBytes;
        private static string path;
        private static bool started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-recordaudio") continue;
                path = args[i + 1];
                var go = new GameObject("AudioRecorderHost");
                DontDestroyOnLoad(go);
                go.AddComponent<AudioRecorder>();
                sampleRate = AudioSettings.outputSampleRate;
                return;
            }
        }

        private void Update()
        {
            // Every scene has its own AudioListener: make sure the active one carries a tap that feeds the shared file.
            var listener = FindFirstObjectByType<AudioListener>();
            if (listener != null && listener.GetComponent<AudioTap>() == null) listener.gameObject.AddComponent<AudioTap>();
        }

        private void OnApplicationQuit() { Close(); }

        private static void Write(float[] data, int ch)
        {
            lock (gate)
            {
                if (!started)
                {
                    started = true;
                    channels = ch;
                    stream = new FileStream(path, FileMode.Create, FileAccess.Write);
                    stream.Write(new byte[44], 0, 44);   // header, filled in on close
                    File.WriteAllText(path + ".start.txt", DateTime.UtcNow.ToString("o"));
                }
                if (stream == null) return;
                var bytes = new byte[data.Length * 2];
                for (int i = 0; i < data.Length; i++)
                {
                    short s = (short)(Mathf.Clamp(data[i], -1f, 1f) * 32767f);
                    bytes[i * 2] = (byte)(s & 0xff); bytes[i * 2 + 1] = (byte)((s >> 8) & 0xff);
                }
                stream.Write(bytes, 0, bytes.Length);
                dataBytes += bytes.Length;
            }
        }

        private static void Close()
        {
            lock (gate)
            {
                if (stream == null) return;
                var w = new BinaryWriter(stream);
                stream.Seek(0, SeekOrigin.Begin);
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write((int)(36 + dataBytes));
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)channels);
                w.Write(sampleRate); w.Write(sampleRate * channels * 2); w.Write((short)(channels * 2)); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write((int)dataBytes);
                w.Flush(); stream.Close(); stream = null;
            }
        }

        /// <summary>Sits on the AudioListener and receives the final mix.</summary>
        private class AudioTap : MonoBehaviour
        {
            private void OnAudioFilterRead(float[] data, int ch) { Write(data, ch); }
        }
    }
}
