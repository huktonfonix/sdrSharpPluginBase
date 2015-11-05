namespace SDRSharp.waveletPWR
{
    partial class waveletPWRPanel
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
        	this.enableFilterCheckBox = new System.Windows.Forms.CheckBox();
        	this.mainTableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
        	this.enableAudioCheckBox = new System.Windows.Forms.CheckBox();
        	this.enableMPXCheckBox = new System.Windows.Forms.CheckBox();
        	this.enableIFCheckBox = new System.Windows.Forms.CheckBox();
        	this.mainTableLayoutPanel.SuspendLayout();
        	this.SuspendLayout();
        	// 
        	// enableFilterCheckBox
        	// 
        	this.enableFilterCheckBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
        	this.enableFilterCheckBox.AutoSize = true;
        	this.enableFilterCheckBox.Location = new System.Drawing.Point(105, 8);
        	this.enableFilterCheckBox.Name = "enableFilterCheckBox";
        	this.enableFilterCheckBox.Size = new System.Drawing.Size(81, 17);
        	this.enableFilterCheckBox.TabIndex = 0;
        	this.enableFilterCheckBox.Text = "Enable filter";
        	this.enableFilterCheckBox.UseVisualStyleBackColor = true;
        	this.enableFilterCheckBox.CheckedChanged += new System.EventHandler(this.enableFilterCheckBox_CheckedChanged);
        	// 
        	// mainTableLayoutPanel
        	// 
        	this.mainTableLayoutPanel.ColumnCount = 2;
        	this.mainTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
        	this.mainTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
        	this.mainTableLayoutPanel.Controls.Add(this.enableAudioCheckBox, 0, 2);
        	this.mainTableLayoutPanel.Controls.Add(this.enableMPXCheckBox, 0, 1);
        	this.mainTableLayoutPanel.Controls.Add(this.enableIFCheckBox, 0, 0);
        	this.mainTableLayoutPanel.Controls.Add(this.enableFilterCheckBox, 1, 0);
        	this.mainTableLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        	this.mainTableLayoutPanel.Location = new System.Drawing.Point(0, 0);
        	this.mainTableLayoutPanel.Name = "mainTableLayoutPanel";
        	this.mainTableLayoutPanel.RowCount = 3;
        	this.mainTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
        	this.mainTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
        	this.mainTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
        	this.mainTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
        	this.mainTableLayoutPanel.Size = new System.Drawing.Size(204, 104);
        	this.mainTableLayoutPanel.TabIndex = 1;
        	// 
        	// enableAudioCheckBox
        	// 
        	this.enableAudioCheckBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
        	this.enableAudioCheckBox.AutoSize = true;
        	this.enableAudioCheckBox.Checked = true;
        	this.enableAudioCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
        	this.enableAudioCheckBox.Location = new System.Drawing.Point(3, 77);
        	this.enableAudioCheckBox.Name = "enableAudioCheckBox";
        	this.enableAudioCheckBox.Size = new System.Drawing.Size(85, 17);
        	this.enableAudioCheckBox.TabIndex = 2;
        	this.enableAudioCheckBox.Text = "Linear Interp";
        	this.enableAudioCheckBox.UseVisualStyleBackColor = true;
        	this.enableAudioCheckBox.CheckedChanged += new System.EventHandler(this.enableAudioCheckBox_CheckedChanged);
        	// 
        	// enableMPXCheckBox
        	// 
        	this.enableMPXCheckBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
        	this.enableMPXCheckBox.AutoSize = true;
        	this.enableMPXCheckBox.Checked = true;
        	this.enableMPXCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
        	this.enableMPXCheckBox.Location = new System.Drawing.Point(3, 42);
        	this.enableMPXCheckBox.Name = "enableMPXCheckBox";
        	this.enableMPXCheckBox.Size = new System.Drawing.Size(49, 17);
        	this.enableMPXCheckBox.TabIndex = 1;
        	this.enableMPXCheckBox.Text = "Haar";
        	this.enableMPXCheckBox.UseVisualStyleBackColor = true;
        	this.enableMPXCheckBox.CheckedChanged += new System.EventHandler(this.enableMPXCheckBox_CheckedChanged);
        	// 
        	// enableIFCheckBox
        	// 
        	this.enableIFCheckBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
        	this.enableIFCheckBox.AutoSize = true;
        	this.enableIFCheckBox.Checked = true;
        	this.enableIFCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
        	this.enableIFCheckBox.Location = new System.Drawing.Point(3, 8);
        	this.enableIFCheckBox.Name = "enableIFCheckBox";
        	this.enableIFCheckBox.Size = new System.Drawing.Size(93, 17);
        	this.enableIFCheckBox.TabIndex = 3;
        	this.enableIFCheckBox.Text = "Debauche D4";
        	this.enableIFCheckBox.UseVisualStyleBackColor = true;
        	this.enableIFCheckBox.CheckedChanged += new System.EventHandler(this.enableIFCheckBox_CheckedChanged);
        	// 
        	// waveletPWRPanel
        	// 
        	this.Controls.Add(this.mainTableLayoutPanel);
        	this.Name = "waveletPWRPanel";
        	this.Size = new System.Drawing.Size(204, 104);
        	this.mainTableLayoutPanel.ResumeLayout(false);
        	this.mainTableLayoutPanel.PerformLayout();
        	this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.CheckBox enableFilterCheckBox;
        private System.Windows.Forms.TableLayoutPanel mainTableLayoutPanel;
        private System.Windows.Forms.CheckBox enableIFCheckBox;
        private System.Windows.Forms.CheckBox enableAudioCheckBox;
        private System.Windows.Forms.CheckBox enableMPXCheckBox;


    }
}
