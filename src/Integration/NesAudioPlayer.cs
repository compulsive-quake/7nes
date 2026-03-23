using UnityEngine;
using SevenNes.Core;

namespace SevenNes.Integration
{
    public class NesAudioPlayer : MonoBehaviour
    {
        private Apu _apu;
        private bool _active;
        private float _lastSample;
        private float[] _monoBuffer;

        public void Init(Apu apu)
        {
            _apu = apu;

            var audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D sound
            audioSource.loop = true;
            audioSource.volume = 1f;

            // Create a silent clip to keep the AudioSource playing so OnAudioFilterRead fires
            var silentClip = AudioClip.Create("NES_APU", AudioSettings.outputSampleRate, 1, AudioSettings.outputSampleRate, false);
            float[] silence = new float[AudioSettings.outputSampleRate];
            silentClip.SetData(silence, 0);
            audioSource.clip = silentClip;
            audioSource.Play();

            // Tell the APU what sample rate Unity is using
            apu.SetSampleRate(AudioSettings.outputSampleRate);

            _active = true;
            Log.Out($"[7nes] Audio initialized at {AudioSettings.outputSampleRate} Hz");
        }

        public void SetActive(bool active)
        {
            _active = active;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (_apu == null || !_active)
            {
                // Fill with silence
                for (int i = 0; i < data.Length; i++)
                    data[i] = 0f;
                return;
            }

            int sampleFrames = data.Length / channels;
            if (_monoBuffer == null || _monoBuffer.Length < sampleFrames)
                _monoBuffer = new float[sampleFrames];
            int samplesRead = _apu.ReadSamples(_monoBuffer, sampleFrames);

            // Copy mono samples to all channels, holding last sample on underrun
            for (int i = 0; i < sampleFrames; i++)
            {
                float sample = i < samplesRead ? _monoBuffer[i] : _lastSample;
                if (i < samplesRead)
                    _lastSample = sample;
                for (int ch = 0; ch < channels; ch++)
                {
                    data[i * channels + ch] = sample;
                }
            }
        }

        void OnDestroy()
        {
            _active = false;
            _apu = null;
        }
    }
}
