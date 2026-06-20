using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;
using NAudio.Wave;

namespace KokonoeAssistant.Services
{
    /// <summary>
    /// Records microphone input to WAV and exposes diagnostics for the Whisper pipeline.
    /// </summary>
    public class AudioRecordService : IDisposable
    {
        private readonly object _writeLock = new();
        private IWaveIn? _waveIn;
        private WaveFileWriter? _writer;
        private string? _currentRecordFile;
        private bool _isRecording;
        private DateTime _recordingStartedAt;
        private long _totalBytesRecorded;
        private bool _sawData;
        private float _lastInputLevel;
        private float _sessionPeakLevel;
        private string _lastError = "";
        private string _activeDevice = "";
        private WaveFormat? _activeFormat;

        public sealed record AudioInputDeviceInfo(int DeviceNumber, string ProductName, int Channels);

        public event EventHandler<EventArgs>? RecordingStarted;
        public event EventHandler<EventArgs>? RecordingStopped;
        public event EventHandler<Exception>? RecordingError;
        public event EventHandler<float>? InputLevelChanged;

        public bool IsRecording => _isRecording;
        public string? CurrentRecordFile => _currentRecordFile;
        public string LastError => _lastError;
        public float LastInputLevel => _lastInputLevel;
        public float PeakInputLevel => _sessionPeakLevel;
        public long TotalBytesRecorded => _totalBytesRecorded;
        public string ActiveDevice => _activeDevice;
        public WaveFormat? ActiveFormat => _activeFormat;

        public AudioRecordService()
        {
            Log("initialized");
            LogDeviceInventory();
            LogMicrophonePrivacyHint();
        }

        public IReadOnlyList<AudioInputDeviceInfo> GetInputDevices()
        {
            var list = new List<AudioInputDeviceInfo>();
            try
            {
                for (var i = 0; i < WaveIn.DeviceCount; i++)
                {
                    var caps = WaveIn.GetCapabilities(i);
                    list.Add(new AudioInputDeviceInfo(i, caps.ProductName, caps.Channels));
                }
            }
            catch (Exception ex)
            {
                Log($"device enumeration failed: {DescribeException(ex)}");
            }
            return list;
        }

        public async Task<bool> StartRecordingAsync()
        {
            try
            {
                if (_isRecording)
                {
                    Log("start ignored: already recording");
                    return false;
                }

                ResetSessionState();
                LogDeviceInventory();
                LogMicrophonePrivacyHint();

                var recordDir = Path.Combine(Path.GetTempPath(), "KokonoeAssistant", "AudioRecords");
                EnsureWritableDirectory(recordDir);

                _currentRecordFile = Path.Combine(recordDir, $"record_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.wav");

                var devices = GetInputDevices();
                if (devices.Count == 0)
                    throw new InvalidOperationException("No Windows audio input devices were found. Check microphone driver and Windows privacy settings.");

                var formats = new[]
                {
                    new WaveFormat(16000, 16, 1),
                    new WaveFormat(48000, 16, 1),
                    new WaveFormat(44100, 16, 1),
                    new WaveFormat(48000, 16, Math.Min(2, Math.Max(1, devices[0].Channels))),
                    new WaveFormat(44100, 16, Math.Min(2, Math.Max(1, devices[0].Channels)))
                };

                Exception? last = null;
                foreach (var device in devices)
                {
                    foreach (var format in formats.DistinctBy(f => $"{f.SampleRate}:{f.BitsPerSample}:{f.Channels}"))
                    {
                        try
                        {
                            OpenDevice(device, format);
                            _waveIn!.StartRecording();
                            _isRecording = true;
                            _recordingStartedAt = DateTime.Now;
                            RecordingStarted?.Invoke(this, EventArgs.Empty);
                            Log($"recording started file={_currentRecordFile}; device={_activeDevice}; format={FormatLabel(_activeFormat)}");
                            return await Task.FromResult(true);
                        }
                        catch (Exception ex)
                        {
                            last = ex;
                            Log($"open/start failed device={device.DeviceNumber}:{device.ProductName}; format={FormatLabel(format)}; error={DescribeException(ex)}");
                            CleanupRecordingObjects(deleteFile: true);
                        }
                    }
                }

                throw new InvalidOperationException("All microphone device/format combinations failed. Last error: " + DescribeException(last));
            }
            catch (Exception ex)
            {
                _lastError = DescribeException(ex);
                Log($"start failed: {_lastError}");
                RecordingError?.Invoke(this, ex);
                return false;
            }
        }

        public async Task<bool> StopRecordingAsync()
        {
            try
            {
                if (!_isRecording)
                {
                    Log("stop ignored: not recording");
                    return false;
                }

                _waveIn?.StopRecording();
                _isRecording = false;
                await Task.Delay(250);
                FinalizeWriter();

                var length = _currentRecordFile != null && File.Exists(_currentRecordFile)
                    ? new FileInfo(_currentRecordFile).Length
                    : 0;
                Log($"recording stopped; bytes={_totalBytesRecorded}; wavBytes={length}; peak={_sessionPeakLevel:P0}; last={_lastInputLevel:P0}; dataSeen={_sawData}; file={_currentRecordFile}");
                if (!_sawData || _totalBytesRecorded == 0)
                    Log("warning: recording stopped without audio buffers. This usually means device access was blocked or the selected input is silent.");

                RecordingStopped?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                _lastError = DescribeException(ex);
                Log($"stop failed: {_lastError}");
                RecordingError?.Invoke(this, ex);
                return false;
            }
        }

        public async Task<string?> TestMicAsync(TimeSpan? duration = null)
        {
            var seconds = duration ?? TimeSpan.FromSeconds(3);
            if (IsRecording)
                await StopRecordingAsync();

            var started = await StartRecordingAsync();
            if (!started)
                return null;

            await Task.Delay(seconds);
            await StopRecordingAsync();
            Log($"test_mic saved file={_currentRecordFile}; peak={_sessionPeakLevel:P0}; last={_lastInputLevel:P0}; bytes={_totalBytesRecorded}");
            return _currentRecordFile;
        }

        public async Task<byte[]?> GetRecordingBytesAsync()
        {
            try
            {
                if (_currentRecordFile == null || !File.Exists(_currentRecordFile))
                {
                    Log("get bytes failed: recording file missing");
                    return null;
                }

                var bytes = await Task.Run(() => File.ReadAllBytes(_currentRecordFile));
                Log($"get bytes ok: {bytes.Length} bytes from {_currentRecordFile}");
                return bytes;
            }
            catch (Exception ex)
            {
                _lastError = DescribeException(ex);
                Log($"get bytes failed: {_lastError}");
                return null;
            }
        }

        private void OpenDevice(AudioInputDeviceInfo device, WaveFormat format)
        {
            _waveIn = new WaveInEvent
            {
                DeviceNumber = device.DeviceNumber,
                WaveFormat = format,
                BufferMilliseconds = 50,
                NumberOfBuffers = 4
            };
            _waveIn.DataAvailable += WaveIn_DataAvailable;
            _waveIn.RecordingStopped += WaveIn_RecordingStopped;

            _writer = new WaveFileWriter(_currentRecordFile!, format);
            _activeDevice = $"{device.DeviceNumber}:{device.ProductName}";
            _activeFormat = format;
        }

        private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
        {
            try
            {
                if (e.BytesRecorded <= 0)
                    return;

                lock (_writeLock)
                {
                    _writer?.Write(e.Buffer, 0, e.BytesRecorded);
                }

                _sawData = true;
                _totalBytesRecorded += e.BytesRecorded;
                var level = ComputePcm16Peak(e.Buffer, e.BytesRecorded);
                _lastInputLevel = level;
                if (level > _sessionPeakLevel)
                    _sessionPeakLevel = level;
                InputLevelChanged?.Invoke(this, level);

                if (_totalBytesRecorded <= e.BytesRecorded)
                    Log($"first audio buffer: bytes={e.BytesRecorded}; peak={level:P0}; format={FormatLabel(_activeFormat)}");
            }
            catch (Exception ex)
            {
                _lastError = DescribeException(ex);
                Log($"data buffer write failed: {_lastError}");
                RecordingError?.Invoke(this, ex);
            }
        }

        private void WaveIn_RecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                _lastError = DescribeException(e.Exception);
                Log($"recording stopped with driver error: {_lastError}");
                RecordingError?.Invoke(this, e.Exception);
            }
        }

        private void ResetSessionState()
        {
            _lastError = "";
            _lastInputLevel = 0;
            _sessionPeakLevel = 0;
            _totalBytesRecorded = 0;
            _sawData = false;
            _activeDevice = "";
            _activeFormat = null;
        }

        private void CleanupRecordingObjects(bool deleteFile)
        {
            try { _waveIn?.StopRecording(); } catch (Exception suppressedEx283) { KokoSystemLog.Write("AUDIORECORDSERVICE-CATCH", "CleanupRecordingObjects failed near source line 283: " + suppressedEx283); }
            try { _waveIn?.Dispose(); } catch (Exception suppressedEx284) { KokoSystemLog.Write("AUDIORECORDSERVICE-CATCH", "CleanupRecordingObjects failed near source line 284: " + suppressedEx284); }
            try { _writer?.Dispose(); } catch (Exception suppressedEx285) { KokoSystemLog.Write("AUDIORECORDSERVICE-CATCH", "CleanupRecordingObjects failed near source line 285: " + suppressedEx285); }
            _waveIn = null;
            _writer = null;
            _activeDevice = "";
            _activeFormat = null;

            if (deleteFile && _currentRecordFile != null)
            {
                try { if (File.Exists(_currentRecordFile)) File.Delete(_currentRecordFile); } catch (Exception suppressedEx293) { KokoSystemLog.Write("AUDIORECORDSERVICE-CATCH", "CleanupRecordingObjects failed near source line 293: " + suppressedEx293); }
            }
        }

        private void FinalizeWriter()
        {
            lock (_writeLock)
            {
                try { _writer?.Flush(); } catch (Exception suppressedEx301) { KokoSystemLog.Write("AUDIORECORDSERVICE-CATCH", "FinalizeWriter failed near source line 301: " + suppressedEx301); }
                try { _writer?.Dispose(); } catch (Exception suppressedEx302) { KokoSystemLog.Write("AUDIORECORDSERVICE-CATCH", "FinalizeWriter failed near source line 302: " + suppressedEx302); }
                _writer = null;
            }

            try { _waveIn?.Dispose(); } catch (Exception suppressedEx306) { KokoSystemLog.Write("AUDIORECORDSERVICE-CATCH", "FinalizeWriter failed near source line 306: " + suppressedEx306); }
            _waveIn = null;
        }

        private static void EnsureWritableDirectory(string dir)
        {
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, $".write-test-{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
            }
            catch (Exception ex)
            {
                throw new IOException($"Audio temp directory is not writable: {dir}. {DescribeException(ex)}", ex);
            }
            Log($"record directory writable: {dir}");
        }

        private void LogDeviceInventory()
        {
            var devices = GetInputDevices();
            Log($"input devices: count={devices.Count}");
            foreach (var d in devices)
                Log($"input device {d.DeviceNumber}: {d.ProductName}; channels={d.Channels}");
        }

        private static void LogMicrophonePrivacyHint()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone");
                var value = key?.GetValue("Value")?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    Log($"windows microphone privacy global={value}");

                using var nonPackaged = key?.OpenSubKey("NonPackaged");
                if (nonPackaged != null)
                {
                    foreach (var name in nonPackaged.GetSubKeyNames().Take(20))
                    {
                        using var appKey = nonPackaged.OpenSubKey(name);
                        var appValue = appKey?.GetValue("Value")?.ToString();
                        if (string.Equals(appValue, "Deny", StringComparison.OrdinalIgnoreCase))
                            Log($"windows microphone privacy deny entry: {name}");
                    }
                }

                if (string.Equals(value, "Deny", StringComparison.OrdinalIgnoreCase))
                    Log("warning: Windows privacy appears to deny microphone access. Settings > Privacy & security > Microphone.");
            }
            catch (Exception ex)
            {
                Log($"microphone privacy check unavailable: {DescribeException(ex)}");
            }
        }

        private static float ComputePcm16Peak(byte[] buffer, int bytesRecorded)
        {
            var max = 0;
            for (var i = 0; i + 1 < bytesRecorded; i += 2)
            {
                var sample = BitConverter.ToInt16(buffer, i);
                var abs = Math.Abs((int)sample);
                if (abs > max) max = abs;
            }
            return Math.Clamp(max / 32768f, 0f, 1f);
        }

        private static string FormatLabel(WaveFormat? format)
            => format == null ? "--" : $"{format.SampleRate}Hz/{format.BitsPerSample}bit/{format.Channels}ch";

        private static string DescribeException(Exception? ex)
        {
            if (ex == null) return "none";
            var hresult = ex.HResult == 0 ? "" : $" hresult=0x{ex.HResult:X8}";
            var win32 = ex is Win32Exception win32Ex ? $" win32={win32Ex.NativeErrorCode}" : "";
            var com = ex is COMException comEx ? $" com=0x{comEx.ErrorCode:X8}" : "";
            return $"{ex.GetType().Name}:{win32}{com}{hresult} {ex.Message}";
        }

        private static void Log(string msg)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioRecordService] {msg}");
            KokoSystemLog.Write("AUDIO", msg);
        }

        public void Dispose()
        {
            try
            {
                if (_isRecording)
                {
                    try { _waveIn?.StopRecording(); } catch (Exception suppressedEx400) { KokoSystemLog.Write("AUDIORECORDSERVICE-CATCH", "Dispose failed near source line 400: " + suppressedEx400); }
                    _isRecording = false;
                    System.Threading.Thread.Sleep(200);
                }

                CleanupRecordingObjects(deleteFile: false);
                Log("disposed");
            }
            catch (Exception ex)
            {
                Log($"dispose failed: {DescribeException(ex)}");
            }
        }
    }
}
