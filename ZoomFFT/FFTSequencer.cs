using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.ComponentModel;
using SDRSharp.Common;
using SDRSharp.PanView;
using SDRSharp.Radio;

namespace SDRSharp.ZoomFFT
{
    public unsafe abstract class FFTSequencer
    {
        private int _maxIQSamples;
        private int _fftSamplesPerFrame;
        private int _fftSize;
        private bool _fftSpectrumAvailable;
        private bool _fftBufferIsWaiting;
        private float _fftOffset;
        private double _fftOverlapRatio;
        private double _sampleRate;

        private UnsafeBuffer _inputBuffer;
        private float* _inputPtr;
        
        private UnsafeBuffer _fftBuffer;
        private Complex* _fftPtr;
        
        private UnsafeBuffer _fftWindow;
        private float* _fftWindowPtr;
        
        private UnsafeBuffer _fftSpectrum;
        private float* _fftSpectrumPtr;
        
        private UnsafeBuffer _scaledFFTSpectrum;
        private byte* _scaledFFTSpectrumPtr;

        private Thread _fftThread;
        private bool _fftThreadRunning;
        private System.Windows.Forms.Timer _fftTimer;

        private readonly SharpEvent _fftEvent = new SharpEvent(false);
        private readonly FloatFifoStream _floatStream = new FloatFifoStream(BlockMode.BlockingRead);

        public FFTSequencer(double sampleRate, int fftSize = 1024, int interval = 40, float fftOffset = 0.0f)
        {
            _sampleRate = sampleRate;
            _fftSize = fftSize;
            _fftOffset = fftOffset;

            #region FFT Timer

            _fftTimer = new System.Windows.Forms.Timer();
            _fftTimer.Tick += fftTimer_Tick;
            _fftTimer.Interval = interval;

            #endregion

            #region FFT Buffers / Window

            InitFFTBuffers();
            BuildFFTWindow();

            #endregion
        }

        private void BuildFFTWindow()
        {
            var window = FilterBuilder.MakeWindow(WindowType.BlackmanHarris7, _fftSize);
            fixed (float* windowPtr = window)
            {
                Utils.Memcpy(_fftWindow, windowPtr, _fftSize * sizeof(float));
            }
        }

        private void InitFFTBuffers()
        {
            _inputBuffer = UnsafeBuffer.Create(_fftSize, sizeof(float));
            _fftBuffer = UnsafeBuffer.Create(_fftSize, sizeof(Complex));
            _fftWindow = UnsafeBuffer.Create(_fftSize, sizeof(float));
            _fftSpectrum = UnsafeBuffer.Create(_fftSize, sizeof(float));
            _scaledFFTSpectrum = UnsafeBuffer.Create(_fftSize, sizeof(byte));

            _inputPtr = (float*)_inputBuffer;
            _fftPtr = (Complex*)_fftBuffer;
            _fftWindowPtr = (float*)_fftWindow;
            _fftSpectrumPtr = (float*)_fftSpectrum;
            _scaledFFTSpectrumPtr = (byte*)_scaledFFTSpectrum;
        }

        #region FFT Thread

        private void Start()
        {
            if (_fftThread == null)
            {
                _fftThreadRunning = true;
                _fftThread = new Thread(ProcessFFT);
                _fftThread.Name = "FFT Sequencer";
                _fftThread.Start();
                _floatStream.Open();

                _fftTimer.Enabled = true;
            }
        }

        public void Stop()
        {
            _fftTimer.Enabled = false;
            _fftThreadRunning = false;

            if (_fftThread != null)
            {
                _floatStream.Close();
                _fftEvent.Set();
                _fftThread.Join();
                _fftThread = null;
            }
        }

        private void ProcessFFT(object parameter)
        {
            while (_fftThreadRunning)
            {
                #region Configure

                var fftRate = _fftSize / (_fftTimer.Interval * 0.001);
                _fftOverlapRatio = _sampleRate / fftRate;
                var samplesToConsume = (int)(_fftSize * _fftOverlapRatio);
                _fftSamplesPerFrame = Math.Min(samplesToConsume, _fftSize);
                var excessSamples = samplesToConsume - _fftSamplesPerFrame;
                _maxIQSamples = (int) (samplesToConsume / (double)_fftTimer.Interval * 100 * /*_streamControl.BufferSizeInMs **/  1.5);

                #endregion

                #region Shift data for overlapped mode)

                if (_fftSamplesPerFrame < _fftSize)
                {
                    Utils.Memcpy(_inputPtr, _inputPtr + _fftSamplesPerFrame, (_fftSize - _fftSamplesPerFrame) * sizeof(Complex));
                }

                #endregion

                #region Read IQ data

                var targetLength = _fftSamplesPerFrame;

                var total = 0;
                while (total < targetLength && _fftThreadRunning)
                {
                    var len = targetLength - total;
                    total += _floatStream.Read(_inputPtr, _fftSize - targetLength + total, len);
                }

                if (!_fftThreadRunning)
                {
                    break;
                }

                _floatStream.Advance(excessSamples);

                #endregion

                if (!_fftSpectrumAvailable)
                {
                    #region Process FFT gain

                    // http://www.designnews.com/author.asp?section_id=1419&doc_id=236273&piddl_msgid=522392
                    var fftGain = (float)(10.0 * Math.Log10((double)_fftSize / 2));
                    var compensation = 24.0f - fftGain + _fftOffset;

                    #endregion

                    #region Calculate FFT

                    for (var i = 0; i < _fftSize; i++)
                    {
                        _fftPtr[i] = _inputPtr[i];
                    }

                    Fourier.ApplyFFTWindow(_fftPtr, _fftWindowPtr, _fftSize);
                    Fourier.ForwardTransform(_fftPtr, _fftSize);
                    Fourier.SpectrumPower((Complex*)_fftPtr, (float*)_fftSpectrumPtr, (int)_fftSize, (float)compensation);

                    #endregion

                    _fftSpectrumAvailable = true;
                }

                if (_floatStream.Length <= _maxIQSamples)
                {
                    _fftBufferIsWaiting = true;
                    _fftEvent.WaitOne();
                }
            }
            _floatStream.Flush();
        }

        private void fftTimer_Tick(object sender, EventArgs e)
        {
            if (_fftThreadRunning)
            {
                //_spectrumAnalyzer.Render(_fftSpectrumPtr, _fftSize);
                _fftSpectrumAvailable = false;
                if (_fftBufferIsWaiting)
                {
                    _fftBufferIsWaiting = false;
                    _fftEvent.Set();
                }
            }
        }

        #endregion
    }
}
