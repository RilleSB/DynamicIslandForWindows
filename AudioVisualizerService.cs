using System;
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
        private readonly float[] sampleBuffer = new float[FftSize];
        private int sampleBufferPos;

        private readonly double[] fftReal = new double[FftSize];
        private readonly double[] fftImag = new double[FftSize];
        private readonly float[] currentBands = new float[4];
        private readonly float[] smoothedBands = new float[4];

        public bool IsEnabled { get; set; } = true;

        public event Action<float[]> BandsUpdated;

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
            BandsUpdated?.Invoke(new float[4]);
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded <= 0)
                return;

            try
            {
                var waveFormat = capture?.WaveFormat;
                if (waveFormat == null || waveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
                    return;

                int channels = waveFormat.Channels;
                int bytesPerSample = 4;
                int stride = channels * bytesPerSample;
                int sampleCount = e.BytesRecorded / stride;

                for (int i = 0; i < sampleCount; i++)
                {
                    float left = BitConverter.ToSingle(e.Buffer, i * stride);
                    float right = channels > 1 ? BitConverter.ToSingle(e.Buffer, i * stride + bytesPerSample) : left;
                    float mono = (left + right) * 0.5f;

                    sampleBuffer[sampleBufferPos++] = mono;
                    if (sampleBufferPos >= FftSize)
                    {
                        sampleBufferPos = 0;
                        ProcessFft();
                    }
                }
            }
            catch { }
        }

        private void ProcessFft()
        {
            for (int i = 0; i < FftSize; i++)
            {
                // Hann window
                double window = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (FftSize - 1)));
                fftReal[i] = sampleBuffer[i] * window;
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

            // Dynamic gain scaling
            float scale0 = Math.Clamp(b0 * 3.8f, 0f, 1f);
            float scale1 = Math.Clamp(b1 * 5.2f, 0f, 1f);
            float scale2 = Math.Clamp(b2 * 6.5f, 0f, 1f);
            float scale3 = Math.Clamp(b3 * 8.0f, 0f, 1f);

            lock (syncRoot)
            {
                currentBands[0] = scale0;
                currentBands[1] = scale1;
                currentBands[2] = scale2;
                currentBands[3] = scale3;

                // Smooth attack and decay
                for (int i = 0; i < 4; i++)
                {
                    if (currentBands[i] > smoothedBands[i])
                        smoothedBands[i] = smoothedBands[i] * 0.4f + currentBands[i] * 0.6f;
                    else
                        smoothedBands[i] = smoothedBands[i] * 0.82f + currentBands[i] * 0.18f;
                }
            }

            BandsUpdated?.Invoke(GetSmoothedBands());
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
