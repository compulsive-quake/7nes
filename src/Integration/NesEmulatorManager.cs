using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using SevenNes.Core;

namespace SevenNes.Integration
{
    public class NesEmulatorManager
    {
        private static NesEmulatorManager _instance;
        public static NesEmulatorManager Instance => _instance ??= new NesEmulatorManager();

        private Nes _nes;
        private Texture2D _screenTexture;
        private bool _isRunning;
        private bool _hasLoadedRom;
        private string[] _romFiles;
        private int _currentRomIndex;
        private string _currentRomItemName;
        private Color32[] _colorBuffer;
        private NesAudioPlayer _audioPlayer;
        private GameObject _audioObject;

        // Frame timing: NES NTSC runs at ~60.0988 Hz
        private const double NtscFrameRate = 1789773.0 / 29780.5;
        private const double SecondsPerFrame = 1.0 / NtscFrameRate;
        private double _frameTimeAccumulator;

        public bool IsRunning => _isRunning;
        public bool HasLoadedRom => _hasLoadedRom;
        public Texture2D ScreenTexture => _screenTexture;
        public string CurrentRomName => _nes?.CurrentRomName ?? "No ROM";
        public string CurrentRomItemName => _currentRomItemName;

        public NesEmulatorManager()
        {
            _nes = new Nes();
            _screenTexture = new Texture2D(256, 240, TextureFormat.RGBA32, false);
            _screenTexture.filterMode = FilterMode.Point;
            _colorBuffer = new Color32[256 * 240];
            InitAudio();
            RefreshRomList();
        }

        public void RefreshRomList()
        {
            if (Directory.Exists(ModInit.RomsPath))
                _romFiles = Directory.GetFiles(ModInit.RomsPath, "*.nes");
            else
                _romFiles = new string[0];
        }

        public string[] GetRomList()
        {
            return _romFiles;
        }

        public bool LoadRom(int index)
        {
            if (_romFiles == null || index < 0 || index >= _romFiles.Length)
                return false;

            try
            {
                _currentRomIndex = index;
                _nes.LoadRom(_romFiles[index]);
                _isRunning = true;
                _hasLoadedRom = true;
                _frameTimeAccumulator = 0;
                if (_audioPlayer != null)
                    _audioPlayer.SetActive(true);
                Log.Out($"[7nes] Loaded ROM: {Path.GetFileNameWithoutExtension(_romFiles[index])}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[7nes] Failed to load ROM: {ex.Message}");
                _isRunning = false;
                return false;
            }
        }

        public bool LoadRomByItemName(string itemName)
        {
            if (itemName == _currentRomItemName && _hasLoadedRom)
                return true;

            if (_romFiles == null || _romFiles.Length == 0)
                RefreshRomList();

            if (_romFiles == null) return false;

            for (int i = 0; i < _romFiles.Length; i++)
            {
                if (NesCartridgeItems.GetItemName(_romFiles[i]) == itemName)
                {
                    if (LoadRom(i))
                    {
                        _currentRomItemName = itemName;
                        return true;
                    }
                    return false;
                }
            }

            Log.Warning($"[7nes] No ROM file found for cartridge: {itemName}");
            return false;
        }

        public bool LoadNextRom()
        {
            if (_romFiles == null || _romFiles.Length == 0) return false;
            _currentRomIndex = (_currentRomIndex + 1) % _romFiles.Length;
            return LoadRom(_currentRomIndex);
        }

        public bool LoadPreviousRom()
        {
            if (_romFiles == null || _romFiles.Length == 0) return false;
            _currentRomIndex = (_currentRomIndex - 1 + _romFiles.Length) % _romFiles.Length;
            return LoadRom(_currentRomIndex);
        }

        public void RunFrame()
        {
            if (!_isRunning) return;

            _frameTimeAccumulator += Time.deltaTime;

            // Clamp accumulator to prevent spiral-of-death (max 3 frames catch-up)
            if (_frameTimeAccumulator > SecondsPerFrame * 3)
                _frameTimeAccumulator = SecondsPerFrame * 3;

            bool ran = false;
            while (_frameTimeAccumulator >= SecondsPerFrame)
            {
                _frameTimeAccumulator -= SecondsPerFrame;
                try
                {
                    _nes.RunFrame();
                    ran = true;
                }
                catch (Exception ex)
                {
                    Log.Error($"[7nes] Emulator error: {ex.Message}");
                    _isRunning = false;
                    return;
                }
            }

            if (ran)
                UpdateTexture();
        }

        public void UnloadRom()
        {
            _isRunning = false;
            _hasLoadedRom = false;
            _currentRomItemName = null;
            if (_audioPlayer != null)
                _audioPlayer.SetActive(false);
            Log.Out("[7nes] ROM unloaded");
        }

        public void Stop()
        {
            _isRunning = false;
            if (_audioPlayer != null)
                _audioPlayer.SetActive(false);
        }

        public bool Resume()
        {
            if (_hasLoadedRom)
            {
                _isRunning = true;
                _frameTimeAccumulator = 0;
                if (_audioPlayer != null)
                    _audioPlayer.SetActive(true);
                return true;
            }
            return false;
        }

        public void Reset()
        {
            if (_isRunning)
                _nes.Reset();
        }

        // Button indices: 0=A, 1=B, 2=Select, 3=Start, 4=Up, 5=Down, 6=Left, 7=Right
        public void SetButton(int button, bool pressed)
        {
            _nes?.Controller1?.SetButton(button, pressed);
        }

        private void InitAudio()
        {
            try
            {
                _audioObject = new GameObject("7nes_Audio");
                UnityEngine.Object.DontDestroyOnLoad(_audioObject);
                _audioPlayer = _audioObject.AddComponent<NesAudioPlayer>();
                _audioPlayer.Init(_nes.Apu);
                _audioPlayer.SetActive(false);
            }
            catch (Exception ex)
            {
                Log.Error($"[7nes] Failed to initialize audio: {ex.Message}");
            }
        }

        private void UpdateTexture()
        {
            byte[] fb = _nes.GetFrameBuffer();
            if (fb == null) return;

            for (int y = 0; y < 240; y++)
            {
                for (int x = 0; x < 256; x++)
                {
                    // NES framebuffer is top-to-bottom, Unity textures are bottom-to-top
                    int srcIdx = (y * 256 + x) * 4;
                    int dstIdx = ((239 - y) * 256) + x;
                    _colorBuffer[dstIdx] = new Color32(fb[srcIdx], fb[srcIdx + 1], fb[srcIdx + 2], 255);
                }
            }

            _screenTexture.SetPixels32(_colorBuffer);
            _screenTexture.Apply();
        }
    }
}
