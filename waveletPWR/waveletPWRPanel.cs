using System;
using System.Windows.Forms;

namespace SDRSharp.waveletPWR
{
    public partial class waveletPWRPanel : UserControl
    {
        private IFProcessor _ifProcessor;
        private MPXProcessor _mpxProcessor;
        private AFProcessor _afProcessor;

        public waveletPWRPanel(IFProcessor ifProcessor, MPXProcessor mpxProcessor, AFProcessor afProcessor)
        {
            InitializeComponent();
            _ifProcessor = ifProcessor;
            _mpxProcessor = mpxProcessor;
            _afProcessor = afProcessor;
            enableFilterCheckBox.Checked = _ifProcessor.EnableFilter && ifProcessor.Control.Visible;
            enableFilterCheckBox.Enabled = ifProcessor.Control.Visible;
            enableIFCheckBox.Checked = ifProcessor.Control.Visible;
            enableMPXCheckBox.Checked = mpxProcessor.Control.Visible;
            enableAudioCheckBox.Checked = afProcessor.Control.Visible;
        }

        private void enableFilterCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            _ifProcessor.EnableFilter = enableFilterCheckBox.Enabled && enableFilterCheckBox.Checked;
        }

        private void enableIFCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            _ifProcessor.Control.Visible = enableIFCheckBox.Checked;
            enableFilterCheckBox.Enabled = enableIFCheckBox.Checked;
            if (!enableIFCheckBox.Checked)
            {
                _ifProcessor.EnableFilter = false;
            }
            else
            {
                enableFilterCheckBox_CheckedChanged(null, null);
            }
        }

        private void enableMPXCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            _mpxProcessor.Control.Visible = enableMPXCheckBox.Checked;
        }

        private void enableAudioCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            _afProcessor.Control.Visible = enableAudioCheckBox.Checked;
        }
    }
}
