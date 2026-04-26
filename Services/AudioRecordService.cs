using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;

namespace KokonoeAssistant.Services
{
    /// <summary>
    /// AudioRecordService - Захоплення аудіо з мікрофону в WAV формат
    /// Підготовка для Whisper транскрипції
    /// </summary>
    public class AudioRecordService : IDisposable
    {
        private IWaveIn? _waveIn;
        private WaveFileWriter? _writer;
        private string? _currentRecordFile;
        private bool _isRecording;

        public event EventHandler<EventArgs>? RecordingStarted;
        public event EventHandler<EventArgs>? RecordingStopped;
        public event EventHandler<Exception>? RecordingError;

        public bool IsRecording => _isRecording;
        public string? CurrentRecordFile => _currentRecordFile;

        public AudioRecordService()
        {
            System.Diagnostics.Debug.WriteLine("[AudioRecordService] Initialized");
        }

        public async Task<bool> StartRecordingAsync()
        {
            try
            {
                if (_isRecording) return false;

                var recordDir = Path.Combine(
                    Path.GetTempPath(), 
                    "KokonoeAssistant", 
                    "AudioRecords"
                );
                Directory.CreateDirectory(recordDir);

                _currentRecordFile = Path.Combine(
                    recordDir,
                    $"record_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.wav"
                );

                // Initialize WaveIn with default microphone
                _waveIn = new WaveInEvent();
                _waveIn.WaveFormat = new WaveFormat(16000, 16, 1); // 16kHz, 16-bit, mono (Whisper optimized)
                _waveIn.DataAvailable += WaveIn_DataAvailable;
                _waveIn.RecordingStopped += WaveIn_RecordingStopped;

                // Initialize WaveFileWriter
                _writer = new WaveFileWriter(_currentRecordFile, _waveIn.WaveFormat);

                _waveIn.StartRecording();
                _isRecording = true;

                RecordingStarted?.Invoke(this, EventArgs.Empty);
                System.Diagnostics.Debug.WriteLine($"[AudioRecordService] Recording started: {_currentRecordFile}");

                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioRecordService] Start error: {ex.Message}");
                RecordingError?.Invoke(this, ex);
                return false;
            }
        }

        public async Task<bool> StopRecordingAsync()
        {
            try
            {
                if (!_isRecording) return false;

                _waveIn?.StopRecording();
                _isRecording = false;

                // Wait for cleanup
                await Task.Delay(200);

                RecordingStopped?.Invoke(this, EventArgs.Empty);
                System.Diagnostics.Debug.WriteLine($"[AudioRecordService] Recording stopped");

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioRecordService] Stop error: {ex.Message}");
                RecordingError?.Invoke(this, ex);
                return false;
            }
        }

        public async Task<byte[]?> GetRecordingBytesAsync()
        {
            try
            {
                if (_currentRecordFile == null || !File.Exists(_currentRecordFile))
                    return null;

                return await Task.Run(() => File.ReadAllBytes(_currentRecordFile));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioRecordService] Get bytes error: {ex.Message}");
                return null;
            }
        }

        private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_writer != null)
            {
                _writer.Write(e.Buffer, 0, e.BytesRecorded);
                _writer.Flush();
            }
        }

        private void WaveIn_RecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioRecordService] Recording error: {e.Exception.Message}");
            }
        }

        public void Dispose()
        {
            try
            {
                if (_isRecording)
                {
                    // Stop recording synchronously to avoid fire-and-forget async in Dispose
                    _waveIn?.StopRecording();
                    _isRecording = false;
                    System.Threading.Thread.Sleep(200); // Wait for cleanup
                }

                _writer?.Dispose();
                _waveIn?.Dispose();
                
                System.Diagnostics.Debug.WriteLine("[AudioRecordService] Disposed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioRecordService] Dispose error: {ex.Message}");
            }
        }
    }
}
