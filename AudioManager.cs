using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;

namespace wargame
{
    public class AudioManager : IDisposable
    {
        private IWavePlayer musicPlayer;
        private AudioFileReader musicReader;

        public AudioManager(string musicFilePath)
        {
            musicPlayer = new WaveOutEvent();
            musicReader = new AudioFileReader(musicFilePath);
            musicPlayer.Init(musicReader);
            musicPlayer.PlaybackStopped += (s, e) =>
            {
                musicReader.Position = 0;
                musicPlayer.Play();
            };
            musicPlayer.Play();
        }

        public void PlaySoundEffect(string soundFilePath, float volume = 1.0f) // 0.0f — тишина, 1.0f — макс
        {
            var effectReader = new AudioFileReader(soundFilePath)
            {
                Volume = volume // громкость здесь!
            };
            var effectPlayer = new WaveOutEvent();
            effectPlayer.Init(effectReader);
            effectPlayer.PlaybackStopped += (s, e) =>
            {
                effectPlayer.Dispose();
                effectReader.Dispose();
            };
            effectPlayer.Play();
        }


        public void StopMusic()
        {
            musicPlayer?.Stop();
            musicReader?.Dispose();
            musicPlayer?.Dispose();
        }

        public void Dispose()
        {
            StopMusic();
        }
    }
}
