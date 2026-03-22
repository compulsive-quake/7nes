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
        private string[] _romFiles;
        private int _currentRomIndex;
        private Color32[] _colorBuffer;

        public bool IsRunning => _isRunning;
        public Texture2D ScreenTexture => _screenTexture;
        public string CurrentRomName => _nes?.CurrentRomName ?? "No ROM";

        public NesEmulatorManager()
        {
            _nes = new Nes();
            _screenTexture = new Texture2D(256, 240, TextureFormat.RGBA32, false);
            _screenTexture.filterMode = FilterMode.Point;
            _colorBuffer = new Color32[256 * 240];
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

            try
            {
                _nes.RunFrame();
                UpdateTexture();
            }
            catch (Exception ex)
            {
                Log.Error($"[7nes] Emulator error: {ex.Message}");
                _isRunning = false;
            }
        }

        public void Stop()
        {
            _isRunning = false;
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
