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

namespace SDRSharp.waveletPWR
{    
    public unsafe class MPXProcessor : IRealProcessor
    {
        private const int FFTBins = 1024 * 4;
        private const int FFTTimerInterval = 40;

        private const int DefaultFilterOrder = 510;

        private int _maxIQSamples;
        private int _fftSamplesPerFrame;
        private double _fftOverlapRatio;        
        private int _fftBins;
        private bool _fftSpectrumAvailable;
        private bool _fftBufferIsWaiting;
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
        private double _sampleRate;

        private Thread _fftThread;
        private bool _fftThreadRunning;
        private System.Windows.Forms.Timer _fftTimer;
        private SpectrumAnalyzer _spectrumAnalyzer;

        private readonly SharpEvent _fftEvent = new SharpEvent(false);
        private readonly FloatFifoStream _floatStream = new FloatFifoStream(BlockMode.BlockingRead);
        private readonly ISharpControl _control;

        public MPXProcessor(ISharpControl control)
        {
            _control = control;
            _control.PropertyChanged += NotifyPropertyChangedHandler;
            Enabled = true;
           
            #region FFT Timer
            
            _fftTimer = new System.Windows.Forms.Timer();            
            _fftTimer.Tick += fftTimer_Tick;
            _fftTimer.Interval = FFTTimerInterval;
            _fftBins = FFTBins;
            
            #endregion
            
            #region FFT Buffers / Window

            InitFFTBuffers();
            BuildFFTWindow();
            
            #endregion

            #region Display component

            _spectrumAnalyzer = new SpectrumAnalyzer();
            _spectrumAnalyzer.Dock = DockStyle.Fill;
            _spectrumAnalyzer.Margin = new Padding(0, 0, 0, 0);
            _spectrumAnalyzer.DisplayRange = 90;
            _spectrumAnalyzer.EnableFilter = false;
            _spectrumAnalyzer.EnableHotTracking = false;
            _spectrumAnalyzer.EnableFrequencyMarker = false;
            _spectrumAnalyzer.StepSize = 19000;
            _spectrumAnalyzer.UseSmoothing = true;
            _spectrumAnalyzer.SpectrumWidth = 100000;//Utils.GetIntSetting("minOutputSampleRate", 32000) / 2;
            _spectrumAnalyzer.Frequency = _spectrumAnalyzer.SpectrumWidth / 2;
            _spectrumAnalyzer.CenterFrequency = _spectrumAnalyzer.SpectrumWidth / 2;
            _spectrumAnalyzer.Attack = 0.9f;
            _spectrumAnalyzer.Decay = 0.6f;
            _spectrumAnalyzer.StatusText = "Debauche D4 Basis Function";		//EDIT: changed spectrum alanyzer window title to match wavelet fn
            _spectrumAnalyzer.FrequencyChanged += spectrumAnalyzer_FrequencyChanged;
            _spectrumAnalyzer.CenterFrequencyChanged += spectrumAnalyzer_CenterFrequencyChanged;
            _spectrumAnalyzer.VisibleChanged += spectrumAnalyzer_VisibleChanged;

            #endregion

            _control.RegisterStreamHook(this, ProcessorType.FMMPX);
            _control.RegisterFrontControl(_spectrumAnalyzer, (PluginPosition) Utils.GetIntSetting("zoomPosition", (int) PluginPosition.Bottom));
        }

        private void spectrumAnalyzer_VisibleChanged(object sender, EventArgs e)
        {
            if (_spectrumAnalyzer.Visible)
            {
                if (_control.IsPlaying)
                {
                    Start();
                }
            }
            else
            {
                Stop();
            }
            _spectrumAnalyzer.ResetSpectrum();
        }

        private void spectrumAnalyzer_FrequencyChanged(object sender, FrequencyEventArgs e)
        {
            e.Cancel = true;
        }

        private void spectrumAnalyzer_CenterFrequencyChanged(object sender, FrequencyEventArgs e)
        {
            e.Cancel = true;
        }

        public double SampleRate
        {
            get { return _sampleRate; }
            set
            {
                if (_sampleRate != value)
                {
                    _sampleRate = value;
                    _spectrumAnalyzer.BeginInvoke(new Action(() => ConfigureSpectrumAnalyzer()));
                }
            }
        }

        public UserControl Control
        {
            get { return _spectrumAnalyzer; }
        }

        public bool Enabled
        {
            get;
            set;
        }

        public void Process(float* buffer, int length)
        {
            if (!_spectrumAnalyzer.Visible)
            {
                return;
            }

            if (_floatStream.Length < length * 4)
            {
                _floatStream.Write(buffer, length);
            }
        }

        private void NotifyPropertyChangedHandler(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "StartRadio":
                    Start();
                    break;

                case "StopRadio":
                    Stop();
                    break;

                case "DetectorType":
                case "SAttack":
                case "SDecay":
                    ConfigureSpectrumAnalyzer();
                    break;
            }
        }
     
        #region FFT Thread

        private void Start()
        {
            _fftThreadRunning = true;
            
            if (_fftThread == null)
            {
                _fftThread = new Thread(ProcessFFT);
                _fftThread.Name = "Zoom FFT";
                _fftThread.Start();
                _floatStream.Open();
            }
            
            _fftTimer.Enabled = true;
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

        #endregion

        #region FFT

        private void ProcessFFT(object parameter)
        {
            while (_control.IsPlaying && _fftThreadRunning)
            {
                #region Configure

                var fftRate = _fftBins / (_fftTimer.Interval * 0.001);
                _fftOverlapRatio = _sampleRate / fftRate;
                var samplesToConsume = (int)(_fftBins * _fftOverlapRatio);
                _fftSamplesPerFrame = Math.Min(samplesToConsume, _fftBins);
                var excessSamples = samplesToConsume - _fftSamplesPerFrame;
                _maxIQSamples = (int)(samplesToConsume / (double)_fftTimer.Interval  * 100 * /*_streamControl.BufferSizeInMs **/  1.5);

                #endregion

                #region Shift data for overlapped mode)

                if (_fftSamplesPerFrame < _fftBins)
                {
                    Utils.Memcpy(_inputPtr, _inputPtr + _fftSamplesPerFrame, (_fftBins - _fftSamplesPerFrame) * sizeof(float));
                }

                #endregion

                #region Read IQ data

                var targetLength = _fftSamplesPerFrame;

                var total = 0;
                while (_control.IsPlaying && total < targetLength && _fftThreadRunning)
                {
                    var len = targetLength - total;
                    total += _floatStream.Read(_inputPtr, _fftBins - targetLength + total, len);
                }

                _floatStream.Advance(excessSamples);

                #endregion

                if (!_fftSpectrumAvailable)
                {
                    #region Process FFT gain

                    // http://www.designnews.com/author.asp?section_id=1419&doc_id=236273&piddl_msgid=522392
                    var fftGain = (float)(10.0 * Math.Log10((double)_fftBins / 2));
                    var compensation = 24.0f - fftGain + 53;

                    #endregion

                    #region Calculate FFT

                    for (var i = 0; i < _fftBins / 2; i++)
                    {
                        _fftPtr[i] = _inputPtr[i];
                    }

                    for (var i = _fftBins / 2; i < _fftBins; i++)
                    {
                        _fftPtr[i] = 0;
                    }
                    Fourier.ApplyFFTWindow(_fftPtr, _fftWindowPtr, _fftBins / 2);
                    Fourier.ForwardTransform(_fftPtr, _fftBins, true);
                    Fourier.SpectrumPower(_fftPtr + _fftBins / 2, _fftSpectrumPtr, _fftBins / 2, compensation);

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

        private void BuildFFTWindow()
        {
            var window = FilterBuilder.MakeWindow(WindowType.Blackman, _fftBins / 2);
            fixed (float* windowPtr = window)
            {
                Utils.Memcpy(_fftWindow, windowPtr, _fftBins / 2 * sizeof(float));
            }
        }

        private void fftTimer_Tick(object sender, EventArgs e)
        {
            if (_control.IsPlaying && _control.DetectorType == DetectorType.WFM)
            {
                var ratio = _spectrumAnalyzer.SpectrumWidth / _sampleRate;
                var bins = Math.Min(_fftBins / 2, (int) (_fftBins * ratio));
                _spectrumAnalyzer.Render(_fftSpectrumPtr, bins);
                _fftSpectrumAvailable = false;
                if (_fftBufferIsWaiting)
                {
                    _fftBufferIsWaiting = false;
                    _fftEvent.Set();
                }
            }
        }

        private void InitFFTBuffers()
        {
            _inputBuffer = UnsafeBuffer.Create(_fftBins, sizeof(float));
            _fftBuffer = UnsafeBuffer.Create(_fftBins, sizeof(Complex));
            _fftWindow = UnsafeBuffer.Create(_fftBins, sizeof(float));
            _fftSpectrum = UnsafeBuffer.Create(_fftBins, sizeof(float));
            _scaledFFTSpectrum = UnsafeBuffer.Create(_fftBins, sizeof(byte));

            _inputPtr = (float*)_inputBuffer;
            _fftPtr = (Complex*)_fftBuffer;
            _fftWindowPtr = (float*)_fftWindow;
            _fftSpectrumPtr = (float*)_fftSpectrum;
            _scaledFFTSpectrumPtr = (byte*)_scaledFFTSpectrum;
        }
        
        #endregion

        #region Display

        private void ConfigureSpectrumAnalyzer()
        {
            _spectrumAnalyzer.Attack = _control.SAttack;
            _spectrumAnalyzer.Decay = _control.SDecay;
            if (_control.DetectorType != DetectorType.WFM)
            {
                _spectrumAnalyzer.ResetSpectrum();
            }
        }

        #endregion
    }
}
