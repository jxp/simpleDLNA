namespace NMaier.SimpleDlna.GUI
{
  partial class FormSettings
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
      if (disposing && (components != null)) {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.numericPort = new System.Windows.Forms.NumericUpDown();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.textCacheFile = new System.Windows.Forms.TextBox();
            this.buttonDeleteClient = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.buttonBrowseCacheFile = new System.Windows.Forms.Button();
            this.folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
            this.checkStartMinimized = new System.Windows.Forms.CheckBox();
            this.checkFileLogging = new System.Windows.Forms.CheckBox();
            this.buttonOK = new System.Windows.Forms.Button();
            this.checkAutoStart = new System.Windows.Forms.CheckBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.buttonBrowseMKV = new System.Windows.Forms.Button();
            this.textMKVToolsDirectory = new System.Windows.Forms.TextBox();
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabGeneral = new System.Windows.Forms.TabPage();
            this.tabClients = new System.Windows.Forms.TabPage();
            this.labelClientsHint = new System.Windows.Forms.Label();
            this.gridClients = new System.Windows.Forms.DataGridView();
            this.colDescription = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTranscodeAudio = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colLastSeen = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colUserAgent = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericPort)).BeginInit();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.tabControl.SuspendLayout();
            this.tabGeneral.SuspendLayout();
            this.tabClients.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridClients)).BeginInit();
            this.SuspendLayout();
            //
            // groupBox1
            //
            this.groupBox1.Controls.Add(this.numericPort);
            this.groupBox1.Location = new System.Drawing.Point(8, 8);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(303, 55);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Port";
            //
            // numericPort
            //
            this.numericPort.DataBindings.Add(new System.Windows.Forms.Binding("Value", global::NMaier.SimpleDlna.GUI.Properties.Settings.Default, "port", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.numericPort.Location = new System.Drawing.Point(7, 22);
            this.numericPort.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this.numericPort.Name = "numericPort";
            this.numericPort.Size = new System.Drawing.Size(80, 23);
            this.numericPort.TabIndex = 0;
            this.toolTip.SetToolTip(this.numericPort, "Port of the http server.\r\nLeave at 0 to automatically have a port selected on sta" +
        "rtup.\r\n\r\n(Requires restart)");
            this.numericPort.Value = global::NMaier.SimpleDlna.GUI.Properties.Settings.Default.port;
            //
            // textCacheFile
            //
            this.textCacheFile.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::NMaier.SimpleDlna.GUI.Properties.Settings.Default, "cache", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.textCacheFile.Location = new System.Drawing.Point(7, 22);
            this.textCacheFile.Name = "textCacheFile";
            this.textCacheFile.Size = new System.Drawing.Size(194, 23);
            this.textCacheFile.TabIndex = 1;
            this.textCacheFile.Text = global::NMaier.SimpleDlna.GUI.Properties.Settings.Default.cache;
            this.toolTip.SetToolTip(this.textCacheFile, "Location of the cache directory.\r\nLeave blank to use the default location (TEMP)." +
        "\r\n\r\n(Requires restart)");
            //
            // buttonDeleteClient
            //
            this.buttonDeleteClient.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonDeleteClient.Enabled = false;
            this.buttonDeleteClient.Location = new System.Drawing.Point(6, 264);
            this.buttonDeleteClient.Name = "buttonDeleteClient";
            this.buttonDeleteClient.Size = new System.Drawing.Size(87, 27);
            this.buttonDeleteClient.TabIndex = 1;
            this.buttonDeleteClient.Text = "Delete";
            this.toolTip.SetToolTip(this.buttonDeleteClient, "Forget the selected clients.\r\nA deleted client is added again the next time it co" +
        "nnects.");
            this.buttonDeleteClient.UseVisualStyleBackColor = true;
            this.buttonDeleteClient.Click += new System.EventHandler(this.buttonDeleteClient_Click);
            //
            // groupBox2
            //
            this.groupBox2.Controls.Add(this.buttonBrowseCacheFile);
            this.groupBox2.Controls.Add(this.textCacheFile);
            this.groupBox2.Location = new System.Drawing.Point(8, 70);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(303, 55);
            this.groupBox2.TabIndex = 2;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Cache directory";
            //
            // buttonBrowseCacheFile
            //
            this.buttonBrowseCacheFile.Location = new System.Drawing.Point(209, 20);
            this.buttonBrowseCacheFile.Name = "buttonBrowseCacheFile";
            this.buttonBrowseCacheFile.Size = new System.Drawing.Size(87, 27);
            this.buttonBrowseCacheFile.TabIndex = 0;
            this.buttonBrowseCacheFile.Text = "Browse";
            this.buttonBrowseCacheFile.UseVisualStyleBackColor = true;
            this.buttonBrowseCacheFile.Click += new System.EventHandler(this.buttonBrowseCacheFile_Click);
            //
            // checkStartMinimized
            //
            this.checkStartMinimized.AutoSize = true;
            this.checkStartMinimized.Checked = global::NMaier.SimpleDlna.GUI.Properties.Settings.Default.startminimized;
            this.checkStartMinimized.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::NMaier.SimpleDlna.GUI.Properties.Settings.Default, "startminimized", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.checkStartMinimized.Location = new System.Drawing.Point(15, 227);
            this.checkStartMinimized.Name = "checkStartMinimized";
            this.checkStartMinimized.Size = new System.Drawing.Size(109, 19);
            this.checkStartMinimized.TabIndex = 4;
            this.checkStartMinimized.Text = "Start minimized";
            this.checkStartMinimized.UseVisualStyleBackColor = true;
            //
            // checkFileLogging
            //
            this.checkFileLogging.AutoSize = true;
            this.checkFileLogging.Checked = global::NMaier.SimpleDlna.GUI.Properties.Settings.Default.filelogging;
            this.checkFileLogging.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::NMaier.SimpleDlna.GUI.Properties.Settings.Default, "filelogging", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.checkFileLogging.Location = new System.Drawing.Point(15, 200);
            this.checkFileLogging.Name = "checkFileLogging";
            this.checkFileLogging.Size = new System.Drawing.Size(200, 19);
            this.checkFileLogging.TabIndex = 3;
            this.checkFileLogging.Text = "Log diagnostic messages to a file";
            this.checkFileLogging.UseVisualStyleBackColor = true;
            //
            // buttonOK
            //
            this.buttonOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.buttonOK.Location = new System.Drawing.Point(526, 345);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new System.Drawing.Size(87, 27);
            this.buttonOK.TabIndex = 0;
            this.buttonOK.Text = "OK";
            this.buttonOK.UseVisualStyleBackColor = true;
            this.buttonOK.Click += new System.EventHandler(this.buttonOK_Click);
            //
            // checkAutoStart
            //
            this.checkAutoStart.AutoSize = true;
            this.checkAutoStart.Location = new System.Drawing.Point(15, 254);
            this.checkAutoStart.Name = "checkAutoStart";
            this.checkAutoStart.Size = new System.Drawing.Size(203, 19);
            this.checkAutoStart.TabIndex = 5;
            this.checkAutoStart.Text = "Start automatically with Windows";
            this.checkAutoStart.UseVisualStyleBackColor = true;
            this.checkAutoStart.CheckedChanged += new System.EventHandler(this.checkAutoStart_CheckedChanged);
            //
            // groupBox3
            //
            this.groupBox3.Controls.Add(this.buttonBrowseMKV);
            this.groupBox3.Controls.Add(this.textMKVToolsDirectory);
            this.groupBox3.Location = new System.Drawing.Point(8, 132);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(303, 58);
            this.groupBox3.TabIndex = 6;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "MKVToolnix Path (optional)";
            //
            // buttonBrowseMKV
            //
            this.buttonBrowseMKV.Location = new System.Drawing.Point(209, 20);
            this.buttonBrowseMKV.Name = "buttonBrowseMKV";
            this.buttonBrowseMKV.Size = new System.Drawing.Size(87, 27);
            this.buttonBrowseMKV.TabIndex = 3;
            this.buttonBrowseMKV.Text = "Browse";
            this.buttonBrowseMKV.UseVisualStyleBackColor = true;
            //
            // textMKVToolsDirectory
            //
            this.textMKVToolsDirectory.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::NMaier.SimpleDlna.GUI.Properties.Settings.Default, "mkvTools", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.textMKVToolsDirectory.Location = new System.Drawing.Point(7, 22);
            this.textMKVToolsDirectory.Name = "textMKVToolsDirectory";
            this.textMKVToolsDirectory.Size = new System.Drawing.Size(194, 23);
            this.textMKVToolsDirectory.TabIndex = 2;
            //
            // tabControl
            //
            this.tabControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl.Controls.Add(this.tabGeneral);
            this.tabControl.Controls.Add(this.tabClients);
            this.tabControl.Location = new System.Drawing.Point(8, 8);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(605, 329);
            this.tabControl.TabIndex = 1;
            //
            // tabGeneral
            //
            this.tabGeneral.Controls.Add(this.groupBox1);
            this.tabGeneral.Controls.Add(this.groupBox2);
            this.tabGeneral.Controls.Add(this.groupBox3);
            this.tabGeneral.Controls.Add(this.checkFileLogging);
            this.tabGeneral.Controls.Add(this.checkStartMinimized);
            this.tabGeneral.Controls.Add(this.checkAutoStart);
            this.tabGeneral.Location = new System.Drawing.Point(4, 24);
            this.tabGeneral.Name = "tabGeneral";
            this.tabGeneral.Padding = new System.Windows.Forms.Padding(3);
            this.tabGeneral.Size = new System.Drawing.Size(597, 301);
            this.tabGeneral.TabIndex = 0;
            this.tabGeneral.Text = "General";
            this.tabGeneral.UseVisualStyleBackColor = true;
            //
            // tabClients
            //
            this.tabClients.Controls.Add(this.labelClientsHint);
            this.tabClients.Controls.Add(this.buttonDeleteClient);
            this.tabClients.Controls.Add(this.gridClients);
            this.tabClients.Location = new System.Drawing.Point(4, 24);
            this.tabClients.Name = "tabClients";
            this.tabClients.Padding = new System.Windows.Forms.Padding(3);
            this.tabClients.Size = new System.Drawing.Size(597, 301);
            this.tabClients.TabIndex = 1;
            this.tabClients.Text = "Clients";
            this.tabClients.UseVisualStyleBackColor = true;
            //
            // labelClientsHint
            //
            this.labelClientsHint.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelClientsHint.Location = new System.Drawing.Point(99, 264);
            this.labelClientsHint.Name = "labelClientsHint";
            this.labelClientsHint.Size = new System.Drawing.Size(492, 27);
            this.labelClientsHint.TabIndex = 2;
            this.labelClientsHint.Text = "Clients are identified by their User-Agent. Changes take effect immediately.";
            this.labelClientsHint.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // gridClients
            //
            this.gridClients.AllowUserToAddRows = false;
            this.gridClients.AllowUserToResizeRows = false;
            this.gridClients.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gridClients.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridClients.BackgroundColor = System.Drawing.SystemColors.Window;
            this.gridClients.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridClients.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colDescription,
            this.colTranscodeAudio,
            this.colLastSeen,
            this.colUserAgent});
            this.gridClients.Location = new System.Drawing.Point(6, 6);
            this.gridClients.Name = "gridClients";
            this.gridClients.RowHeadersVisible = false;
            this.gridClients.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridClients.Size = new System.Drawing.Size(585, 252);
            this.gridClients.TabIndex = 0;
            this.gridClients.CurrentCellDirtyStateChanged += new System.EventHandler(this.gridClients_CurrentCellDirtyStateChanged);
            this.gridClients.SelectionChanged += new System.EventHandler(this.gridClients_SelectionChanged);
            this.gridClients.UserDeletingRow += new System.Windows.Forms.DataGridViewRowCancelEventHandler(this.gridClients_UserDeletingRow);
            //
            // colDescription
            //
            this.colDescription.DataPropertyName = "Description";
            this.colDescription.FillWeight = 35F;
            this.colDescription.HeaderText = "Description";
            this.colDescription.Name = "colDescription";
            this.colDescription.ToolTipText = "A friendly name for the client. Clear it to use the User-Agent.";
            //
            // colTranscodeAudio
            //
            this.colTranscodeAudio.DataPropertyName = "TranscodeAudio";
            this.colTranscodeAudio.FillWeight = 15F;
            this.colTranscodeAudio.HeaderText = "Transcode audio";
            this.colTranscodeAudio.Name = "colTranscodeAudio";
            //
            // colLastSeen
            //
            this.colLastSeen.DataPropertyName = "LastSeen";
            dataGridViewCellStyle1.Format = "g";
            this.colLastSeen.DefaultCellStyle = dataGridViewCellStyle1;
            this.colLastSeen.FillWeight = 20F;
            this.colLastSeen.HeaderText = "Last seen";
            this.colLastSeen.Name = "colLastSeen";
            this.colLastSeen.ReadOnly = true;
            //
            // colUserAgent
            //
            this.colUserAgent.DataPropertyName = "UserAgent";
            this.colUserAgent.FillWeight = 30F;
            this.colUserAgent.HeaderText = "User-Agent";
            this.colUserAgent.Name = "colUserAgent";
            this.colUserAgent.ReadOnly = true;
            //
            // FormSettings
            //
            this.AcceptButton = this.buttonOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(621, 380);
            this.Controls.Add(this.tabControl);
            this.Controls.Add(this.buttonOK);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Name = "FormSettings";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Settings";
            this.groupBox1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.numericPort)).EndInit();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.tabControl.ResumeLayout(false);
            this.tabGeneral.ResumeLayout(false);
            this.tabGeneral.PerformLayout();
            this.tabClients.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridClients)).EndInit();
            this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.GroupBox groupBox1;
    private System.Windows.Forms.NumericUpDown numericPort;
    private System.Windows.Forms.ToolTip toolTip;
    private System.Windows.Forms.GroupBox groupBox2;
    private System.Windows.Forms.Button buttonBrowseCacheFile;
    private System.Windows.Forms.TextBox textCacheFile;
    private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog;
    private System.Windows.Forms.CheckBox checkStartMinimized;
    private System.Windows.Forms.CheckBox checkFileLogging;
    private System.Windows.Forms.Button buttonOK;
    private System.Windows.Forms.CheckBox checkAutoStart;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Button buttonBrowseMKV;
        private System.Windows.Forms.TextBox textMKVToolsDirectory;
    private System.Windows.Forms.TabControl tabControl;
    private System.Windows.Forms.TabPage tabGeneral;
    private System.Windows.Forms.TabPage tabClients;
    private System.Windows.Forms.DataGridView gridClients;
    private System.Windows.Forms.DataGridViewTextBoxColumn colDescription;
    private System.Windows.Forms.DataGridViewCheckBoxColumn colTranscodeAudio;
    private System.Windows.Forms.DataGridViewTextBoxColumn colLastSeen;
    private System.Windows.Forms.DataGridViewTextBoxColumn colUserAgent;
    private System.Windows.Forms.Button buttonDeleteClient;
    private System.Windows.Forms.Label labelClientsHint;
  }
}
