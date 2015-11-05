using System;
using System.Windows.Forms;
using System.ComponentModel;

using SDRSharp.Common;
using SDRSharp.Radio;

namespace SDRSharp.waveletPWR
{
    public class waveletPWRPlugin: ISharpPlugin
    {
        private const string _displayName = "waveletPWR";

        private IFProcessor _ifProcessor;
        private MPXProcessor _mpxProcessor;
        private AFProcessor _afProcessor;
        private waveletPWRPanel _controlPanel;

        public string DisplayName
        {
            get { return _displayName; }
        }

        public bool HasGui
        {
            get { return true; }
        }

        public UserControl GuiControl
        {
            get { return _controlPanel; }
        }

        public void Initialize(ISharpControl control)
        {
            _ifProcessor = new IFProcessor(control);
            _ifProcessor.EnableFilter = Utils.GetBooleanSetting("enableZoomFFTFilter");  //TODO: change the following key names (inside of the "") to something that doesn't conflict with zoomFFT in file called SDRsharp.exe (XML file)
            _ifProcessor.Control.Visible = Utils.GetBooleanSetting("enableZoomIF");

            _mpxProcessor = new MPXProcessor(control);
            _mpxProcessor.Control.Visible = Utils.GetBooleanSetting("enableZoomMPX");

            _afProcessor = new AFProcessor(control);
            _afProcessor.Control.Visible = Utils.GetBooleanSetting("enableZoomAF");

            _controlPanel = new waveletPWRPanel(_ifProcessor, _mpxProcessor, _afProcessor);
        }
        
        public void Close()
        {
            _ifProcessor.Stop();
            _mpxProcessor.Stop();
            _afProcessor.Stop();
            Utils.SaveSetting("enableZoomFFTFilter", _ifProcessor.EnableFilter);  //TODO: change the following key names (inside of the "") to something that doesn't conflict with zoomFFT in file called SDRsharp.exe (XML file)
            Utils.SaveSetting("enableZoomIF", _ifProcessor.Control.Visible);	
            Utils.SaveSetting("enableZoomMPX", _mpxProcessor.Control.Visible);
            Utils.SaveSetting("enableZoomAF", _afProcessor.Control.Visible);
        }        
    }
}
