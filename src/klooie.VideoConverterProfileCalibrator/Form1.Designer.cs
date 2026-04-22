namespace VideoConverterProfileCalibrator;

partial class Form1
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            refreshTimer.Dispose();
            testBitmap?.Dispose(testBitmap.Lease, "Video converter profile calibrator closed");
            preview.Image?.Dispose();
            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        splitContainer = new SplitContainer();
        controlsPanel = new TableLayoutPanel();
        presetComboBox = new ComboBox();
        profileNameTextBox = new TextBox();
        cellPixelWidthNumeric = new NumericUpDown();
        fontPixelSizeNumeric = new NumericUpDown();
        fontFamilyComboBox = new ComboBox();
        textOffsetXNumeric = new NumericUpDown();
        textOffsetYNumeric = new NumericUpDown();
        textScaleXNumeric = new NumericUpDown();
        textScaleYNumeric = new NumericUpDown();
        refreshButton = new Button();
        statusLabel = new Label();
        profileSummaryTextBox = new TextBox();
        preview = new PreviewPictureBox();
        ((System.ComponentModel.ISupportInitialize)splitContainer).BeginInit();
        splitContainer.Panel1.SuspendLayout();
        splitContainer.Panel2.SuspendLayout();
        splitContainer.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)cellPixelWidthNumeric).BeginInit();
        ((System.ComponentModel.ISupportInitialize)fontPixelSizeNumeric).BeginInit();
        ((System.ComponentModel.ISupportInitialize)textOffsetXNumeric).BeginInit();
        ((System.ComponentModel.ISupportInitialize)textOffsetYNumeric).BeginInit();
        ((System.ComponentModel.ISupportInitialize)textScaleXNumeric).BeginInit();
        ((System.ComponentModel.ISupportInitialize)textScaleYNumeric).BeginInit();
        SuspendLayout();
        // 
        // splitContainer
        // 
        splitContainer.Dock = DockStyle.Fill;
        splitContainer.FixedPanel = FixedPanel.Panel1;
        splitContainer.Location = new Point(0, 0);
        splitContainer.Name = "splitContainer";
        splitContainer.Panel1.Controls.Add(controlsPanel);
        splitContainer.Panel2.Controls.Add(preview);
        splitContainer.Size = new Size(1184, 761);
        splitContainer.SplitterDistance = 350;
        splitContainer.TabIndex = 0;
        // 
        // controlsPanel
        // 
        controlsPanel.ColumnCount = 2;
        controlsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
        controlsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
        controlsPanel.Dock = DockStyle.Fill;
        controlsPanel.Location = new Point(0, 0);
        controlsPanel.Name = "controlsPanel";
        controlsPanel.Padding = new Padding(14);
        controlsPanel.RowCount = 19;
        controlsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        controlsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        controlsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        controlsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        controlsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        controlsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        controlsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        controlsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        controlsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        controlsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        controlsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        controlsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        controlsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        controlsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        controlsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        controlsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        controlsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        controlsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        controlsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        controlsPanel.Size = new Size(350, 761);
        controlsPanel.TabIndex = 0;
        // 
        // presetComboBox
        // 
        presetComboBox.Dock = DockStyle.Fill;
        presetComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        presetComboBox.FormattingEnabled = true;
        presetComboBox.Location = new Point(152, 17);
        presetComboBox.Name = "presetComboBox";
        presetComboBox.Size = new Size(181, 23);
        presetComboBox.TabIndex = 0;
        // 
        // profileNameTextBox
        // 
        profileNameTextBox.Dock = DockStyle.Fill;
        profileNameTextBox.Location = new Point(152, 51);
        profileNameTextBox.Name = "profileNameTextBox";
        profileNameTextBox.Size = new Size(181, 23);
        profileNameTextBox.TabIndex = 1;
        // 
        // cellPixelWidthNumeric
        // 
        cellPixelWidthNumeric.Dock = DockStyle.Fill;
        cellPixelWidthNumeric.Location = new Point(152, 85);
        cellPixelWidthNumeric.Maximum = new decimal(new int[] { 128, 0, 0, 0 });
        cellPixelWidthNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        cellPixelWidthNumeric.Name = "cellPixelWidthNumeric";
        cellPixelWidthNumeric.Size = new Size(181, 23);
        cellPixelWidthNumeric.TabIndex = 2;
        cellPixelWidthNumeric.Value = new decimal(new int[] { 8, 0, 0, 0 });
        // 
        // fontPixelSizeNumeric
        // 
        fontPixelSizeNumeric.DecimalPlaces = 2;
        fontPixelSizeNumeric.Dock = DockStyle.Fill;
        fontPixelSizeNumeric.Increment = new decimal(new int[] { 25, 0, 0, 131072 });
        fontPixelSizeNumeric.Location = new Point(152, 119);
        fontPixelSizeNumeric.Maximum = new decimal(new int[] { 200, 0, 0, 0 });
        fontPixelSizeNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        fontPixelSizeNumeric.Name = "fontPixelSizeNumeric";
        fontPixelSizeNumeric.Size = new Size(181, 23);
        fontPixelSizeNumeric.TabIndex = 3;
        fontPixelSizeNumeric.Value = new decimal(new int[] { 14, 0, 0, 0 });
        // 
        // fontFamilyComboBox
        // 
        fontFamilyComboBox.Dock = DockStyle.Fill;
        fontFamilyComboBox.FormattingEnabled = true;
        fontFamilyComboBox.Location = new Point(152, 153);
        fontFamilyComboBox.Name = "fontFamilyComboBox";
        fontFamilyComboBox.Size = new Size(181, 23);
        fontFamilyComboBox.TabIndex = 4;
        // 
        // textOffsetXNumeric
        // 
        textOffsetXNumeric.DecimalPlaces = 2;
        textOffsetXNumeric.Dock = DockStyle.Fill;
        textOffsetXNumeric.Increment = new decimal(new int[] { 25, 0, 0, 131072 });
        textOffsetXNumeric.Location = new Point(152, 187);
        textOffsetXNumeric.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
        textOffsetXNumeric.Minimum = new decimal(new int[] { 100, 0, 0, int.MinValue });
        textOffsetXNumeric.Name = "textOffsetXNumeric";
        textOffsetXNumeric.Size = new Size(181, 23);
        textOffsetXNumeric.TabIndex = 5;
        // 
        // textOffsetYNumeric
        // 
        textOffsetYNumeric.DecimalPlaces = 2;
        textOffsetYNumeric.Dock = DockStyle.Fill;
        textOffsetYNumeric.Increment = new decimal(new int[] { 25, 0, 0, 131072 });
        textOffsetYNumeric.Location = new Point(152, 221);
        textOffsetYNumeric.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
        textOffsetYNumeric.Minimum = new decimal(new int[] { 100, 0, 0, int.MinValue });
        textOffsetYNumeric.Name = "textOffsetYNumeric";
        textOffsetYNumeric.Size = new Size(181, 23);
        textOffsetYNumeric.TabIndex = 6;
        // 
        // textScaleXNumeric
        // 
        textScaleXNumeric.DecimalPlaces = 3;
        textScaleXNumeric.Dock = DockStyle.Fill;
        textScaleXNumeric.Increment = new decimal(new int[] { 5, 0, 0, 131072 });
        textScaleXNumeric.Location = new Point(152, 255);
        textScaleXNumeric.Maximum = new decimal(new int[] { 5, 0, 0, 0 });
        textScaleXNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 196608 });
        textScaleXNumeric.Name = "textScaleXNumeric";
        textScaleXNumeric.Size = new Size(181, 23);
        textScaleXNumeric.TabIndex = 7;
        textScaleXNumeric.Value = new decimal(new int[] { 1, 0, 0, 0 });
        // 
        // textScaleYNumeric
        // 
        textScaleYNumeric.DecimalPlaces = 3;
        textScaleYNumeric.Dock = DockStyle.Fill;
        textScaleYNumeric.Increment = new decimal(new int[] { 5, 0, 0, 131072 });
        textScaleYNumeric.Location = new Point(152, 289);
        textScaleYNumeric.Maximum = new decimal(new int[] { 5, 0, 0, 0 });
        textScaleYNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 196608 });
        textScaleYNumeric.Name = "textScaleYNumeric";
        textScaleYNumeric.Size = new Size(181, 23);
        textScaleYNumeric.TabIndex = 8;
        textScaleYNumeric.Value = new decimal(new int[] { 1, 0, 0, 0 });
        // 
        // refreshButton
        // 
        controlsPanel.SetColumnSpan(refreshButton, 2);
        refreshButton.Dock = DockStyle.Fill;
        refreshButton.Location = new Point(17, 561);
        refreshButton.Name = "refreshButton";
        refreshButton.Size = new Size(316, 36);
        refreshButton.TabIndex = 9;
        refreshButton.Text = "Refresh Preview";
        refreshButton.UseVisualStyleBackColor = true;
        // 
        // statusLabel
        // 
        statusLabel.AutoEllipsis = true;
        controlsPanel.SetColumnSpan(statusLabel, 2);
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.Location = new Point(17, 600);
        statusLabel.Name = "statusLabel";
        statusLabel.Size = new Size(316, 28);
        statusLabel.TabIndex = 10;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // profileSummaryTextBox
        // 
        controlsPanel.SetColumnSpan(profileSummaryTextBox, 2);
        profileSummaryTextBox.Dock = DockStyle.Fill;
        profileSummaryTextBox.Font = new Font("Consolas", 9F);
        profileSummaryTextBox.Location = new Point(17, 631);
        profileSummaryTextBox.Multiline = true;
        profileSummaryTextBox.Name = "profileSummaryTextBox";
        profileSummaryTextBox.ReadOnly = true;
        profileSummaryTextBox.ScrollBars = ScrollBars.Vertical;
        profileSummaryTextBox.Size = new Size(316, 113);
        profileSummaryTextBox.TabIndex = 11;
        // 
        // preview
        // 
        preview.BackColor = Color.FromArgb(32, 32, 32);
        preview.Dock = DockStyle.Fill;
        preview.Location = new Point(0, 0);
        preview.Name = "preview";
        preview.Size = new Size(830, 761);
        preview.TabIndex = 0;
        preview.TabStop = true;
        // 
        // Form1
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1184, 761);
        Controls.Add(splitContainer);
        MinimumSize = new Size(900, 560);
        Name = "Form1";
        Text = "Console Renderer Scale Profile Calibrator";
        splitContainer.Panel1.ResumeLayout(false);
        splitContainer.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)splitContainer).EndInit();
        splitContainer.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)cellPixelWidthNumeric).EndInit();
        ((System.ComponentModel.ISupportInitialize)fontPixelSizeNumeric).EndInit();
        ((System.ComponentModel.ISupportInitialize)textOffsetXNumeric).EndInit();
        ((System.ComponentModel.ISupportInitialize)textOffsetYNumeric).EndInit();
        ((System.ComponentModel.ISupportInitialize)textScaleXNumeric).EndInit();
        ((System.ComponentModel.ISupportInitialize)textScaleYNumeric).EndInit();
        ResumeLayout(false);
    }

    private SplitContainer splitContainer;
    private TableLayoutPanel controlsPanel;
    private ComboBox presetComboBox;
    private TextBox profileNameTextBox;
    private NumericUpDown cellPixelWidthNumeric;
    private NumericUpDown fontPixelSizeNumeric;
    private ComboBox fontFamilyComboBox;
    private NumericUpDown textOffsetXNumeric;
    private NumericUpDown textOffsetYNumeric;
    private NumericUpDown textScaleXNumeric;
    private NumericUpDown textScaleYNumeric;
    private Button refreshButton;
    private Label statusLabel;
    private TextBox profileSummaryTextBox;
    private PreviewPictureBox preview;
}
