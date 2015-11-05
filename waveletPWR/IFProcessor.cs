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
    public unsafe class IFProcessor : IIQProcessor
    {
        private const int FFTBins = 1024 * 8;
        private const int FFTTimerInterval = 40;

        private const int DefaultFilterOrder = 510;

        private int _maxIQSamples;
        private int _fftSamplesPerFrame;
        private double _fftOverlapRatio;        
        private int _fftBins;
        private bool _fftSpectrumAvailable;
        private bool _fftBufferIsWaiting;
        private UnsafeBuffer _iqBuffer;
        private Complex* _iqPtr;
        private UnsafeBuffer _fftBuffer;
        private Complex* _fftPtr;
        private UnsafeBuffer _fftWindow;
        private float* _fftWindowPtr;
        private UnsafeBuffer _fftSpectrum;
        private float* _fftSpectrumPtr;
        private UnsafeBuffer _scaledFFTSpectrum;
        private byte* _scaledFFTSpectrumPtr;
        private double _sampleRate;
        private bool _updateFilter;
        private int _filterbandwidth;
        private int _filterOffset;
        private bool _enableFilter;

        private Thread _fftThread;
        private bool _fftThreadRunning;
        private System.Windows.Forms.Timer _fftTimer;
        private SpectrumAnalyzer _spectrumAnalyzer;

        private readonly float _fftOffset = (float) Utils.GetDoubleSetting("fftOffset", -40.0);
        private readonly SharpEvent _fftEvent = new SharpEvent(false);
        private readonly ComplexFifoStream _iqStream = new ComplexFifoStream(BlockMode.BlockingRead);
        private readonly ISharpControl _control;

        private ComplexFilter _complexFilter;

        private static readonly bool _displayBeforeFilter = Utils.GetBooleanSetting("displayBeforeFilter");

        public IFProcessor(ISharpControl control)
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
            _spectrumAnalyzer.DisplayRange = 130;
            _spectrumAnalyzer.EnableFilter = false;
            _spectrumAnalyzer.EnableHotTracking = false;
            _spectrumAnalyzer.EnableSideFilterResize = true;
            _spectrumAnalyzer.EnableFilterMove = false;
            _spectrumAnalyzer.BandType = BandType.Center;
            _spectrumAnalyzer.StepSize = 1000;
            _spectrumAnalyzer.UseSmoothing = true;
            _spectrumAnalyzer.Attack = 0.9f;
            _spectrumAnalyzer.Decay = 0.6f;
            _spectrumAnalyzer.StatusText = "Haar Basis Function";  //EDIT: changed spectrum alanyzer window title to match wavelet fn
            _spectrumAnalyzer.FrequencyChanged += spectrumAnalyzer_FrequencyChanged;
            _spectrumAnalyzer.CenterFrequencyChanged += spectrumAnalyzer_CenterFrequencyChanged;
            _spectrumAnalyzer.BandwidthChanged += spectrumAnalyzer_BandwidthChanged;
            _spectrumAnalyzer.VisibleChanged += spectrumAnalyzer_VisibleChanged;

            #endregion

            _control.RegisterStreamHook(this, ProcessorType.DecimatedAndFilteredIQ);
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

        private void spectrumAnalyzer_BandwidthChanged(object sender, BandwidthEventArgs e)
        {
            var sign = e.Side == BandType.Upper ? 1 : -1;
            if (e.Bandwidth > _control.FilterBandwidth || Math.Abs(_spectrumAnalyzer.FilterOffset + sign * e.Bandwidth / 2) > _control.FilterBandwidth / 2)
            {
                e.Cancel = true;
                return;
            }
            _filterbandwidth = e.Bandwidth;
            _filterOffset = e.Offset;
            _updateFilter = true;
        }

        private void spectrumAnalyzer_FrequencyChanged(object sender, FrequencyEventArgs e)
        {
            e.Cancel = true;
            _control.Frequency += e.Frequency - _spectrumAnalyzer.Frequency;
        }

        private void spectrumAnalyzer_CenterFrequencyChanged(object sender, FrequencyEventArgs e)
        {
            e.Cancel = true;
            _control.Frequency = e.Frequency - GetFrequencyOffset();
        }

        public UserControl Control
        {
            get { return _spectrumAnalyzer; }
        }

        public double SampleRate
        {
            get { return _sampleRate; }
            set
            {
                if (_sampleRate != value)
                {
                    _sampleRate = value;
                    _updateFilter = true;
                    _spectrumAnalyzer.BeginInvoke(new Action(() => ConfigureSpectrumAnalyzer(true)));
                }
            }
        }

        public bool Enabled
        {
            get;
            set;
        }

        public bool EnableFilter
        {
            get { return _enableFilter; }
            set
            {
                _enableFilter = value;
                _spectrumAnalyzer.EnableFilter = value;
            }
        }

        public void Process(Complex* buffer, int length)
        {
            if (!_spectrumAnalyzer.Visible)
            {
                return;
            }

            if (_displayBeforeFilter)
            {
                if (_iqStream.Length < length * 4)
                {
                    _iqStream.Write(buffer, length);
                }
            }

            if (_enableFilter)
            {
                if (_complexFilter == null)
                {
                    var kernel = FilterBuilder.MakeComplexKernel(_sampleRate, DefaultFilterOrder, _filterbandwidth, _filterOffset, WindowType.BlackmanHarris7);
                    _complexFilter = new ComplexFilter(kernel);
                }
                else if (_updateFilter)
                {
                    var kernel = FilterBuilder.MakeComplexKernel(_sampleRate, DefaultFilterOrder, _filterbandwidth, _filterOffset, WindowType.BlackmanHarris7);
                    _complexFilter.SetKernel(kernel);
                    _updateFilter = false;
                }

                _complexFilter.Process(buffer, length);
            }

            if (!_displayBeforeFilter)
            {
                if (_iqStream.Length < length * 4)
                {
                    _iqStream.Write(buffer, length);
                }
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

                case "StepSize":
                case "FFTRange":
                case "FFTOffset":
                case "SAttack":
                case "SDecay":
                case "Frequency":
                    ConfigureSpectrumAnalyzer(false);
                    break;

                case "FilterBandwidth":
                case "DetectorType":
                    ConfigureSpectrumAnalyzer(true);
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
                _iqStream.Open();
            }
            
            _fftTimer.Enabled = true;
        }

        public void Stop()
        {
            _fftTimer.Enabled = false;
            _fftThreadRunning = false;

            if (_fftThread != null)
            {
                _iqStream.Close();
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

                if (_sampleRate == 0.0)
                {
                    // WTF???
                    Thread.Sleep(1);
                    continue;
                }

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
                    Utils.Memcpy(_iqPtr, _iqPtr + _fftSamplesPerFrame, (_fftBins - _fftSamplesPerFrame) * sizeof(Complex));
                }

                #endregion

                #region Read IQ data

                var targetLength = _fftSamplesPerFrame;

                var total = 0;
                while (_control.IsPlaying && total < targetLength && _fftThreadRunning)
                {
                    var len = targetLength - total;
                    total += _iqStream.Read(_iqPtr, _fftBins - targetLength + total, len);
                }

                _iqStream.Advance(excessSamples);

                #endregion

                if (!_fftSpectrumAvailable)
                {
                    #region Process FFT gain

                    // http://www.designnews.com/author.asp?section_id=1419&doc_id=236273&piddl_msgid=522392
                    var fftGain = (float)(10.0 * Math.Log10((double)_fftBins / 2));
                    var compensation = 24.0f - fftGain + _fftOffset;

                    #endregion

                    #region Calculate FFT

                    Utils.Memcpy(_fftPtr, _iqPtr, _fftBins * sizeof(Complex));
                    Fourier.ApplyFFTWindow(_fftPtr, _fftWindowPtr, _fftBins);
                    Fourier.ForwardTransform(_fftPtr, _fftBins);
                    Fourier.SpectrumPower((Complex*)_fftPtr, (float*)_fftSpectrumPtr, (int)_fftBins,(float)compensation);

                    #endregion
                    
                    _fftSpectrumAvailable = true;
                }

                if (_iqStream.Length <= _maxIQSamples)
                {
                    _fftBufferIsWaiting = true;
                    _fftEvent.WaitOne();
                }
            }
            _iqStream.Flush();
        }

        private void BuildFFTWindow()
        {
            var window = FilterBuilder.MakeWindow(WindowType.BlackmanHarris7, _fftBins);
            fixed (float* windowPtr = window)
            {
                Utils.Memcpy(_fftWindow, windowPtr, _fftBins * sizeof(float));
            }
        }

        private void fftTimer_Tick(object sender, EventArgs e)
        {
            if (_control.IsPlaying)
            {
                var ratio = _spectrumAnalyzer.SpectrumWidth / _sampleRate;
                var bins = Math.Min(_fftBins, (int) (_fftBins * ratio));
                var ptr = _fftSpectrumPtr + (_fftBins - bins) / 2;
                _spectrumAnalyzer.Render(ptr, bins);
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
            _iqBuffer = UnsafeBuffer.Create(_fftBins, sizeof(Complex));
            _fftBuffer = UnsafeBuffer.Create(_fftBins, sizeof(Complex));
            _fftWindow = UnsafeBuffer.Create(_fftBins, sizeof(float));
            _fftSpectrum = UnsafeBuffer.Create(_fftBins, sizeof(float));
            _scaledFFTSpectrum = UnsafeBuffer.Create(_fftBins, sizeof(byte));

            _iqPtr = (Complex*)_iqBuffer;
            _fftPtr = (Complex*)_fftBuffer;
            _fftWindowPtr = (float*)_fftWindow;
            _fftSpectrumPtr = (float*)_fftSpectrum;
            _scaledFFTSpectrumPtr = (byte*)_scaledFFTSpectrum;
        }
        
        #endregion             

        #region Display

        private long GetFrequencyOffset()
        {
            switch (_control.DetectorType)
            {
                case DetectorType.USB:
                    return _control.FilterBandwidth / 2;

                case DetectorType.LSB:
                    return -_control.FilterBandwidth / 2;

                default:
                    return 0;
            }
        }

        private void ConfigureSpectrumAnalyzer(bool overrideFilter)
        {
            _spectrumAnalyzer.SpectrumWidth = (int) Math.Min(_sampleRate, _control.FilterBandwidth * 1.5);
            if (overrideFilter)
            {
                _spectrumAnalyzer.FilterOffset = 0;
                _filterOffset = 0;
                _spectrumAnalyzer.FilterBandwidth = _control.FilterBandwidth;
                _filterbandwidth = _control.FilterBandwidth;
                _updateFilter = true;
            }
            
            var freq = _control.Frequency + GetFrequencyOffset();
            _spectrumAnalyzer.Frequency = freq;
            _spectrumAnalyzer.CenterFrequency = freq;
            _spectrumAnalyzer.DisplayOffset = (int) _control.FFTOffset;
            if (_sampleRate > 0)
            {
                _spectrumAnalyzer.DisplayRange = (int) Math.Ceiling((_control.FFTRange + 6.02 * Math.Log(_control.RFBandwidth / _sampleRate) / Math.Log(4)) * 0.1) * 10;
            }
            _spectrumAnalyzer.StepSize = _control.StepSize;
            _spectrumAnalyzer.Attack = _control.SAttack;
            _spectrumAnalyzer.Decay = _control.SDecay;
            _spectrumAnalyzer.EnableFrequencyMarker = _control.DetectorType != DetectorType.LSB && _control.DetectorType != DetectorType.USB;
        }

        #endregion
    }
}
