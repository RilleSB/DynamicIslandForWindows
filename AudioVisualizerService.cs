using System;
using System.Runtime.InteropServices;
using System.Threading;
using NAudio.Wave;

namespace DynamicIslandPC
{
    public class AudioVisualizerService : IDisposable
    {
        private WasapiLoopbackCapture capture;
        private readonly object syncRoot = new();
        private bool isRunning;
        private bool isDisposed;

        private const int FftSize = 512;
        private static readonly double[] HannWindow = PrecomputeHannWindow();

        private readonly float[] sampleBuffer = new float[FftSize];
        private int sampleBufferPos;

        private readonly double[] fftReal = new double[FftSize];
        private readonly double[] fftImag = new double[FftSize];
        private readonly float[] currentBands = new float[4];
        private readonly float[] smoothedBands = new float[4];

        private static readonly Guid SubTypeIeeeFloat = new("00000003-0000-0010-8000-00AA00389B71");
        private static readonly Guid SubTypePcm = new("00000001-0000-0010-8000-00AA00389B71");

        private Timer silenceTimer;
        private long _lastDataTick;

        public bool IsEnabled { get; set; } = true;

        public event Action<float, float, float, float> BandsUpdated;

        private static double[] PrecomputeHannWindow()
        {
            var window = new double[FftSize];
            double factor = 2.0 * Math.PI / (FftSize - 1);
            for (int i = 0; i < FftSize; i++)
            {
                window[i] = 0.5 * (1.0 - Math.Cos(factor * i));
            }
            return window;
        }

        public void Start()
        {
            if (!IsEnabled || isRunning || isDisposed)
                return;

            lock (syncRoot)
            {
                if (isRunning || isDisposed)
                    return;

                try
                {
                    capture = new WasapiLoopbackCapture();
                    capture.DataAvailable += OnDataAvailable;
                    capture.RecordingStopped += (s, e) =>
                    {
                        lock (syncRoot)
                        {
                            isRunning = false;
                        }
                    };
                    capture.StartRecording();
                    isRunning = true;
                    _lastDataTick = Environment.TickCount64;
                    StartSilenceWatchdog();
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to initialize WASAPI loopback audio visualizer", ex);
                    Stop();
                }
            }
        }

        public void Stop()
        {
            lock (syncRoot)
            {
                StopSilenceWatchdog();

                if (!isRunning && capture == null)
                    return;

                try
                {
                    if (capture != null)
                    {
                        capture.DataAvailable -= OnDataAvailable;
                        capture.StopRecording();
                        capture.Dispose();
                        capture = null;
                    }
                }
                catch { }
                finally
                {
                    isRunning = false;
                    ResetBands();
                }
            }
        }

        private void StartSilenceWatchdog()
        {
            silenceTimer?.Dispose();
            silenceTimer = new Timer(_ =>
            {
                if (!isRunning) return;
                long elapsed = Environment.TickCount64 - _lastDataTick;
                if (elapsed > 70)
                {
                    float sb0, sb1, sb2, sb3;
                    bool hasActiveValue = false;
                    lock (syncRoot)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            smoothedBands[i] *= 0.72f;
                            if (smoothedBands[i] < 0.005f)
                                smoothedBands[i] = 0f;
                            else
                                hasActiveValue = true;
                        }
                        sb0 = smoothedBands[0];
                        sb1 = smoothedBands[1];
                        sb2 = smoothedBands[2];
                        sb3 = smoothedBands[3];
                    }

                    if (hasActiveValue || elapsed < 280)
                    {
                        BandsUpdated?.Invoke(sb0, sb1, sb2, sb3);
                    }
                }
            }, null, 50, 50);
        }

        private void StopSilenceWatchdog()
        {
            try
            {
                silenceTimer?.Dispose();
                silenceTimer = null;
            }
            catch { }
        }

        public float[] GetSmoothedBands()
        {
            lock (syncRoot)
            {
                var copy = new float[4];
                Array.Copy(smoothedBands, copy, 4);
                return copy;
            }
        }

        private void ResetBands()
        {
            lock (syncRoot)
            {
                for (int i = 0; i < 4; i++)
                {
                    currentBands[i] = 0;
                    smoothedBands[i] = 0;
                }
            }
            BandsUpdated?.Invoke(0f, 0f, 0f, 0f);
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded <= 0)
                return;

            try
            {
                var waveFormat = capture?.WaveFormat;
                if (waveFormat == null)
                    return;

                int channels = waveFormat.Channels;
                if (channels <= 0)
                    return;

                int bits = waveFormat.BitsPerSample;
                bool isFloat = waveFormat.Encoding == WaveFormatEncoding.IeeeFloat ||
                               (waveFormat is WaveFormatExtensible ext && ext.SubFormat == SubTypeIeeeFloat);

                if (isFloat && bits == 32)
                {
                    int sampleCount = e.BytesRecorded / (channels * 4);
                    var floatSpan = MemoryMarshal.Cast<byte, float>(e.Buffer.AsSpan(0, e.BytesRecorded));

                    for (int i = 0; i < sampleCount; i++)
                    {
                        int baseIdx = i * channels;
                        float left = floatSpan[baseIdx];
                        float right = channels > 1 ? floatSpan[baseIdx + 1] : left;
                        float mono = (left + right) * 0.5f;

                        sampleBuffer[sampleBufferPos++] = mono;
                        if (sampleBufferPos >= FftSize)
                        {
                            sampleBufferPos = 0;
                            ProcessFft();
                        }
                    }
                }
                else if (bits == 16)
                {
                    int sampleCount = e.BytesRecorded / (channels * 2);
                    var shortSpan = MemoryMarshal.Cast<byte, short>(e.Buffer.AsSpan(0, e.BytesRecorded));

                    for (int i = 0; i < sampleCount; i++)
                    {
                        int baseIdx = i * channels;
                        float left = shortSpan[baseIdx] / 32768.0f;
                        float right = channels > 1 ? (shortSpan[baseIdx + 1] / 32768.0f) : left;
                        float mono = (left + right) * 0.5f;

                        sampleBuffer[sampleBufferPos++] = mono;
                        if (sampleBufferPos >= FftSize)
                        {
                            sampleBufferPos = 0;
                            ProcessFft();
                        }
                    }
                }
                else if (bits == 24)
                {
                    int bytesPerFrame = channels * 3;
                    int frameCount = e.BytesRecorded / bytesPerFrame;
                    for (int i = 0; i < frameCount; i++)
                    {
                        int offset = i * bytesPerFrame;
                        int leftSample = e.Buffer[offset] | (e.Buffer[offset + 1] << 8) | ((sbyte)e.Buffer[offset + 2] << 16);
                        float left = leftSample / 8388608.0f;
                        float right = left;
                        if (channels > 1)
                        {
                            int rightSample = e.Buffer[offset + 3] | (e.Buffer[offset + 4] << 8) | ((sbyte)e.Buffer[offset + 5] << 16);
                            right = rightSample / 8388608.0f;
                        }
                        float mono = (left + right) * 0.5f;

                        sampleBuffer[sampleBufferPos++] = mono;
                        if (sampleBufferPos >= FftSize)
                        {
                            sampleBufferPos = 0;
                            ProcessFft();
                        }
                    }
                }
                else if (bits == 32)
                {
                    int sampleCount = e.BytesRecorded / (channels * 4);
                    var intSpan = MemoryMarshal.Cast<byte, int>(e.Buffer.AsSpan(0, e.BytesRecorded));

                    for (int i = 0; i < sampleCount; i++)
                    {
                        int baseIdx = i * channels;
                        float left = intSpan[baseIdx] / 2147483648.0f;
                        float right = channels > 1 ? (intSpan[baseIdx + 1] / 2147483648.0f) : left;
                        float mono = (left + right) * 0.5f;

                        sampleBuffer[sampleBufferPos++] = mono;
                        if (sampleBufferPos >= FftSize)
                        {
                            sampleBufferPos = 0;
                            ProcessFft();
                        }
                    }
                }
            }
            catch { }
        }

        private void ProcessFft()
        {
            for (int i = 0; i < FftSize; i++)
            {
                fftReal[i] = sampleBuffer[i] * HannWindow[i];
                fftImag[i] = 0;
            }

            ComputeFft(fftReal, fftImag);

            // Bins for 48kHz and 512 points (~93.75 Hz per bin):
            // Band 0: Bass (1..3) -> ~90..280 Hz
            // Band 1: Low-Mid (4..8) -> ~370..750 Hz
            // Band 2: Mid (9..28) -> ~840..2600 Hz
            // Band 3: High (29..90) -> ~2700..8400 Hz
            float b0 = AverageMagnitude(1, 3);
            float b1 = AverageMagnitude(4, 8);
            float b2 = AverageMagnitude(9, 28);
            float b3 = AverageMagnitude(29, 90);

            // Calibrated sensitivity with increased accent on highs
            float scale0 = Math.Clamp(b0 * 1.35f, 0f, 1f);
            float scale1 = Math.Clamp(b1 * 1.85f, 0f, 1f);
            float scale2 = Math.Clamp(b2 * 2.65f, 0f, 1f);
            float scale3 = Math.Clamp(b3 * 3.85f, 0f, 1f);

            float sb0, sb1, sb2, sb3;
            lock (syncRoot)
            {
                currentBands[0] = scale0;
                currentBands[1] = scale1;
                currentBands[2] = scale2;
                currentBands[3] = scale3;

                // Smooth organic attack and musical decay (snappier attack on crisp highs)
                for (int i = 0; i < 4; i++)
                {
                    float attack = i >= 2 ? 0.62f : 0.48f;
                    if (currentBands[i] > smoothedBands[i])
                        smoothedBands[i] = smoothedBands[i] * (1f - attack) + currentBands[i] * attack;
                    else
                        smoothedBands[i] = smoothedBands[i] * 0.85f + currentBands[i] * 0.15f;
                }

                sb0 = smoothedBands[0];
                sb1 = smoothedBands[1];
                sb2 = smoothedBands[2];
                sb3 = smoothedBands[3];
            }

            _lastDataTick = Environment.TickCount64;
            BandsUpdated?.Invoke(sb0, sb1, sb2, sb3);
        }

        private float AverageMagnitude(int startBin, int endBin)
        {
            double sum = 0;
            int count = Math.Max(1, endBin - startBin + 1);
            for (int i = startBin; i <= endBin && i < FftSize / 2; i++)
            {
                double mag = Math.Sqrt(fftReal[i] * fftReal[i] + fftImag[i] * fftImag[i]);
                sum += mag;
            }
            return (float)(sum / count);
        }

        private static void ComputeFft(double[] real, double[] imag)
        {
            int n = real.Length;
            int j = 0;
            for (int i = 0; i < n - 1; i++)
            {
                if (i < j)
                {
                    (real[i], real[j]) = (real[j], real[i]);
                    (imag[i], imag[j]) = (imag[j], imag[i]);
                }
                int k = n / 2;
                while (k <= j)
                {
                    j -= k;
                    k /= 2;
                }
                j += k;
            }

            for (int l = 1; l < n; l <<= 1)
            {
                double angle = -Math.PI / l;
                double wReal = Math.Cos(angle);
                double wImag = Math.Sin(angle);

                for (int i = 0; i < n; i += (l << 1))
                {
                    double uReal = 1.0;
                    double uImag = 0.0;

                    for (int m = 0; m < l; m++)
                    {
                        int pos = i + m;
                        int partner = pos + l;

                        double tReal = uReal * real[partner] - uImag * imag[partner];
                        double tImag = uReal * imag[partner] + uImag * real[partner];

                        real[partner] = real[pos] - tReal;
                        imag[partner] = imag[pos] - tImag;
                        real[pos] += tReal;
                        imag[pos] += tImag;

                        double nextUReal = uReal * wReal - uImag * wImag;
                        uImag = uReal * wImag + uImag * wReal;
                        uReal = nextUReal;
                    }
                }
            }
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            Stop();
        }
    }
}
