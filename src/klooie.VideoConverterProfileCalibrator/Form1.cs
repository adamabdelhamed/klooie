// Note - Don't add using klooie since many UX types conflict with Windows Forms.

using PowerArgs;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;

namespace VideoConverterProfileCalibrator;

public partial class Form1 : Form
{
    private const int PreviewCellWidth = 192;
    private const int PreviewCellHeight = 54;
    private const double MinPreviewZoom = 0.02;
    private const double MaxPreviewZoom = 8.0;
    private const int MaxPreviewVirtualPixels = 12000;

    private readonly System.Windows.Forms.Timer refreshTimer = new();
    private readonly ComboBox textRenderingHintComboBox = new();
    private readonly ComboBox pixelOffsetModeComboBox = new();
    private readonly ComboBox smoothingModeComboBox = new();
    private readonly ComboBox interpolationModeComboBox = new();
    private readonly ComboBox compositingModeComboBox = new();
    private readonly ComboBox compositingQualityComboBox = new();
    private readonly NumericUpDown textContrastNumeric = new();
    private bool loadingProfile;
    private bool previewZoomInitialized;
    private double previewZoom = 1.0;
    private double previewCenterX = 0.5;
    private double previewCenterY = 0.5;
    private klooie.ConsoleBitmap? testBitmap;

    public Form1()
    {
        InitializeComponent();
        AddControls();
        PopulateFontFamilies();
        WireEvents();
        InitPreviewBitmap();
        LoadProfile(klooie.ConsoleRendererScaleProfile.High);
    }

    private void AddControls()
    {
        AddRow("Preset", presetComboBox, 0);
        AddRow("Name", profileNameTextBox, 1);
        AddRow("Cell width", cellPixelWidthNumeric, 2);
        AddRow("Font pixels", fontPixelSizeNumeric, 3);
        AddRow("Font family", fontFamilyComboBox, 4);
        AddRow("Text offset X", textOffsetXNumeric, 5);
        AddRow("Text offset Y", textOffsetYNumeric, 6);
        AddRow("Text scale X", textScaleXNumeric, 7);
        AddRow("Text scale Y", textScaleYNumeric, 8);
        AddRow("Text rendering", textRenderingHintComboBox, 9);
        AddRow("Pixel offset", pixelOffsetModeComboBox, 10);
        AddRow("Smoothing", smoothingModeComboBox, 11);
        AddRow("Interpolation", interpolationModeComboBox, 12);
        AddRow("Compositing mode", compositingModeComboBox, 13);
        AddRow("Compositing quality", compositingQualityComboBox, 14);
        AddRow("Text contrast", textContrastNumeric, 15);

        controlsPanel.Controls.Add(refreshButton, 0, 16);
        controlsPanel.Controls.Add(statusLabel, 0, 17);
        controlsPanel.Controls.Add(profileSummaryTextBox, 0, 18);

        presetComboBox.Items.AddRange(new object[] { "High", "Medium", "Low" });
        presetComboBox.SelectedIndex = 0;
        PopulateEnumCombo(textRenderingHintComboBox, TextRenderingHint.SingleBitPerPixel);
        PopulateEnumCombo(pixelOffsetModeComboBox, PixelOffsetMode.None);
        PopulateEnumCombo(smoothingModeComboBox, SmoothingMode.None);
        PopulateEnumCombo(interpolationModeComboBox, InterpolationMode.NearestNeighbor);
        PopulateEnumCombo(compositingModeComboBox, CompositingMode.SourceOver);
        PopulateEnumCombo(compositingQualityComboBox, CompositingQuality.Default);
        textContrastNumeric.Dock = DockStyle.Fill;
        textContrastNumeric.Minimum = 0;
        textContrastNumeric.Maximum = 12;
        textContrastNumeric.Value = 4;
    }

    private void AddRow(string labelText, Control input, int row)
    {
        var label = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Text = labelText,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        controlsPanel.Controls.Add(label, 0, row);
        controlsPanel.Controls.Add(input, 1, row);
    }

    private static void PopulateEnumCombo<T>(ComboBox comboBox, T defaultValue) where T : struct, Enum
    {
        comboBox.Dock = DockStyle.Fill;
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;

        foreach (var value in Enum.GetValues<T>())
        {
            comboBox.Items.Add(value);
        }

        comboBox.SelectedItem = defaultValue;
    }

    private void PopulateFontFamilies()
    {
        using var fonts = new InstalledFontCollection();

        foreach (var family in fonts.Families.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            fontFamilyComboBox.Items.Add(family.Name);
        }
    }

    private void WireEvents()
    {
        refreshTimer.Interval = 120;
        refreshTimer.Tick += (_, _) =>
        {
            refreshTimer.Stop();
            RefreshPreview();
        };

        presetComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (loadingProfile) return;
            LoadProfile(presetComboBox.SelectedItem?.ToString() == "Low"
                ? klooie.ConsoleRendererScaleProfile.Low : presetComboBox.SelectedItem?.ToString() == "Medium" ? 
                klooie.ConsoleRendererScaleProfile.Medium 
                : klooie.ConsoleRendererScaleProfile.High);
        };

        profileNameTextBox.TextChanged += (_, _) => QueueRefresh();
        fontFamilyComboBox.TextChanged += (_, _) => QueueRefresh();
        refreshButton.Click += (_, _) => RefreshPreview();
        textRenderingHintComboBox.SelectedIndexChanged += (_, _) => QueueRefresh();
        pixelOffsetModeComboBox.SelectedIndexChanged += (_, _) => QueueRefresh();
        smoothingModeComboBox.SelectedIndexChanged += (_, _) => QueueRefresh();
        interpolationModeComboBox.SelectedIndexChanged += (_, _) => QueueRefresh();
        compositingModeComboBox.SelectedIndexChanged += (_, _) => QueueRefresh();
        compositingQualityComboBox.SelectedIndexChanged += (_, _) => QueueRefresh();
        textContrastNumeric.ValueChanged += (_, _) => QueueRefresh();
        preview.MouseEnter += (_, _) => preview.Focus();
        preview.MouseWheel += Preview_MouseWheel;
        preview.Resize += (_, _) => ApplyPreviewViewport();

        foreach (var numeric in GetProfileNumerics())
        {
            numeric.ValueChanged += (_, _) => QueueRefresh();
        }
    }

    private IEnumerable<NumericUpDown> GetProfileNumerics()
    {
        yield return cellPixelWidthNumeric;
        yield return fontPixelSizeNumeric;
        yield return textOffsetXNumeric;
        yield return textOffsetYNumeric;
        yield return textScaleXNumeric;
        yield return textScaleYNumeric;
    }

    private void InitPreviewBitmap()
    {
        testBitmap = klooie.ConsoleBitmap.Create(PreviewCellWidth, PreviewCellHeight);

        for (var y = 0; y < testBitmap.Height; y++)
        {
            for (var x = 0; x < testBitmap.Width; x++)
            {
                testBitmap.SetPixel(x, y, new ConsoleCharacter('#', RGB.Orange.Darker, GetAlternatingBackground(x, y)));
            }
        }
    }

    private static RGB GetAlternatingBackground(int x, int y) => (x + y) % 2 == 0 ? RGB.Black : RGB.Gray;

    private void LoadProfile(klooie.ConsoleRendererScaleProfile profile)
    {
        loadingProfile = true;
        try
        {
            profileNameTextBox.Text = profile.Name;
            cellPixelWidthNumeric.Value = profile.CellPixelWidth;
            fontPixelSizeNumeric.Value = (decimal)profile.FontPixelSize;
            fontFamilyComboBox.Text = profile.FontFamilyName;
            textOffsetXNumeric.Value = (decimal)profile.TextOffsetX;
            textOffsetYNumeric.Value = (decimal)profile.TextOffsetY;
            textScaleXNumeric.Value = (decimal)profile.TextScaleX;
            textScaleYNumeric.Value = (decimal)profile.TextScaleY;
            textRenderingHintComboBox.SelectedItem = profile.TextRenderingHint;
            pixelOffsetModeComboBox.SelectedItem = profile.PixelOffsetMode;
            smoothingModeComboBox.SelectedItem = profile.SmoothingMode;
            interpolationModeComboBox.SelectedItem = profile.InterpolationMode;
            compositingModeComboBox.SelectedItem = profile.CompositingMode;
            compositingQualityComboBox.SelectedItem = profile.CompositingQuality;
            textContrastNumeric.Value = Math.Clamp(profile.TextContrast, (int)textContrastNumeric.Minimum, (int)textContrastNumeric.Maximum);
        }
        finally
        {
            loadingProfile = false;
        }

        RefreshPreview();
    }

    private void QueueRefresh()
    {
        if (loadingProfile) return;

        refreshTimer.Stop();
        refreshTimer.Start();
    }

    private klooie.ConsoleRendererScaleProfile ReadProfile() => new()
    {
        Name = string.IsNullOrWhiteSpace(profileNameTextBox.Text) ? "Custom" : profileNameTextBox.Text.Trim(),
        CellPixelWidth = (int)cellPixelWidthNumeric.Value,
        FontPixelSize = (float)fontPixelSizeNumeric.Value,
        FontFamilyName = string.IsNullOrWhiteSpace(fontFamilyComboBox.Text) ? "Consolas" : fontFamilyComboBox.Text.Trim(),
        TextOffsetX = (float)textOffsetXNumeric.Value,
        TextOffsetY = (float)textOffsetYNumeric.Value,
        TextScaleX = (float)textScaleXNumeric.Value,
        TextScaleY = (float)textScaleYNumeric.Value,
        TextRenderingHint = GetSelectedEnum(textRenderingHintComboBox, TextRenderingHint.SingleBitPerPixel),
        PixelOffsetMode = GetSelectedEnum(pixelOffsetModeComboBox, PixelOffsetMode.None),
        SmoothingMode = GetSelectedEnum(smoothingModeComboBox, SmoothingMode.None),
        InterpolationMode = GetSelectedEnum(interpolationModeComboBox, InterpolationMode.NearestNeighbor),
        CompositingMode = GetSelectedEnum(compositingModeComboBox, CompositingMode.SourceOver),
        CompositingQuality = GetSelectedEnum(compositingQualityComboBox, CompositingQuality.Default),
        TextContrast = (int)textContrastNumeric.Value,
    };

    private static T GetSelectedEnum<T>(ComboBox comboBox, T fallback) where T : struct, Enum =>
        comboBox.SelectedItem is T value ? value : fallback;

    private void RefreshPreview()
    {
        if (testBitmap == null) return;

        try
        {
            CapturePreviewCenter();

            var profile = ReadProfile();
            var previewBitmap = new Bitmap(testBitmap.Width * profile.CellPixelWidth, testBitmap.Height * profile.CellPixelHeight);

            using (var rasterizer = new klooie.ConsoleBitmapRasterizer(profile))
            {
                rasterizer.Rasterize(testBitmap, previewBitmap);
            }

            var oldImage = preview.Image;
            preview.Image = previewBitmap;
            oldImage?.Dispose();

            if (previewZoomInitialized == false)
            {
                previewZoom = GetFitZoom(previewBitmap);
                previewZoomInitialized = true;
            }

            ApplyPreviewViewport();
            statusLabel.Text = $"{previewBitmap.Width}x{previewBitmap.Height} pixels, {previewZoom:P0} zoom";
            profileSummaryTextBox.Text = FormatProfile(profile);
        }
        catch (Exception ex)
        {
            statusLabel.Text = ex.Message;
        }
    }

    private static string FormatProfile(klooie.ConsoleRendererScaleProfile profile)
    {
        var culture = CultureInfo.InvariantCulture;
        return $$"""
            Name = "{{profile.Name}}"
            CellPixelWidth = {{profile.CellPixelWidth.ToString(culture)}}
            CellPixelHeight = {{profile.CellPixelHeight.ToString(culture)}}
            FontPixelSize = {{profile.FontPixelSize.ToString("0.###", culture)}}
            FontFamilyName = "{{profile.FontFamilyName}}"
            TextOffsetX = {{profile.TextOffsetX.ToString("0.###", culture)}}
            TextOffsetY = {{profile.TextOffsetY.ToString("0.###", culture)}}
            TextScaleX = {{profile.TextScaleX.ToString("0.###", culture)}}
            TextScaleY = {{profile.TextScaleY.ToString("0.###", culture)}}
            TextRenderingHint = {{profile.TextRenderingHint}}
            PixelOffsetMode = {{profile.PixelOffsetMode}}
            SmoothingMode = {{profile.SmoothingMode}}
            InterpolationMode = {{profile.InterpolationMode}}
            CompositingMode = {{profile.CompositingMode}}
            CompositingQuality = {{profile.CompositingQuality}}
            TextContrast = {{profile.TextContrast.ToString(culture)}}
            """;
    }

    private void Preview_MouseWheel(object? sender, MouseEventArgs e)
    {
        if (preview.Image == null) return;

        var anchor = preview.PointToClient(Cursor.Position);
        if (preview.ClientRectangle.Contains(anchor) == false)
        {
            anchor = new Point(preview.ClientSize.Width / 2, preview.ClientSize.Height / 2);
        }

        CapturePreviewCenter(anchor);
        var oldZoom = previewZoom;
        var wheelClicks = Math.Clamp(e.Delta / 120.0, -6.0, 6.0);
        previewZoom = ClampZoom(previewZoom * Math.Pow(1.15, wheelClicks));

        if (Math.Abs(previewZoom - oldZoom) < 0.0001) return;

        ApplyPreviewViewport(anchor);
        if (preview.Image is Bitmap bitmap)
        {
            statusLabel.Text = $"{bitmap.Width}x{bitmap.Height} pixels, {previewZoom:P0} zoom";
        }
    }

    private double GetFitZoom(Image image)
    {
        var availableWidth = Math.Max(1, preview.ClientSize.Width);
        var availableHeight = Math.Max(1, preview.ClientSize.Height);
        return Math.Min((double)availableWidth / image.Width, (double)availableHeight / image.Height);
    }

    private double ClampZoom(double zoom)
    {
        if (preview.Image == null) return Math.Clamp(zoom, MinPreviewZoom, MaxPreviewZoom);

        var maxByWidth = MaxPreviewVirtualPixels / (double)Math.Max(1, preview.Image.Width);
        var maxByHeight = MaxPreviewVirtualPixels / (double)Math.Max(1, preview.Image.Height);
        var effectiveMax = Math.Max(MinPreviewZoom, Math.Min(MaxPreviewZoom, Math.Min(maxByWidth, maxByHeight)));
        return Math.Clamp(zoom, MinPreviewZoom, effectiveMax);
    }

    private void CapturePreviewCenter(Point? viewportAnchor = null)
    {
        if (preview.Image == null || preview.ClientSize.Width <= 0 || preview.ClientSize.Height <= 0) return;

        var anchor = viewportAnchor ?? new Point(preview.ClientSize.Width / 2, preview.ClientSize.Height / 2);
        var imagePoint = preview.ViewportToImage(anchor);
        previewCenterX = ClampUnit(imagePoint.X / preview.Image.Width);
        previewCenterY = ClampUnit(imagePoint.Y / preview.Image.Height);
    }

    private void ApplyPreviewViewport(Point? viewportAnchor = null)
    {
        if (preview.Image == null) return;

        previewZoom = ClampZoom(previewZoom);
        previewCenterX = ClampUnit(previewCenterX);
        previewCenterY = ClampUnit(previewCenterY);

        var anchor = viewportAnchor ?? new Point(preview.ClientSize.Width / 2, preview.ClientSize.Height / 2);
        preview.SetView(previewZoom, new PointF(
            (float)(previewCenterX * preview.Image.Width),
            (float)(previewCenterY * preview.Image.Height)),
            anchor);
    }

    private static double ClampUnit(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return 0.5;
        return Math.Clamp(value, 0.0, 1.0);
    }
}

internal sealed class PreviewPictureBox : Control
{
    private Image? image;
    private double zoom = 1.0;
    private PointF imageAnchor;
    private Point viewportAnchor;
    private bool hasView;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Image? Image
    {
        get => image;
        set
        {
            image = value;
            if (image != null && hasView == false)
            {
                imageAnchor = new PointF(image.Width / 2f, image.Height / 2f);
                viewportAnchor = new Point(ClientSize.Width / 2, ClientSize.Height / 2);
                hasView = true;
            }

            Invalidate();
        }
    }

    public PreviewPictureBox()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    public void SetView(double zoom, PointF imageAnchor, Point viewportAnchor)
    {
        this.zoom = zoom;
        this.imageAnchor = imageAnchor;
        this.viewportAnchor = viewportAnchor;
        hasView = true;
        Invalidate();
    }

    public PointF ViewportToImage(Point viewportPoint)
    {
        if (Image == null) return PointF.Empty;

        var origin = GetImageOrigin();
        return new PointF(
            (float)((viewportPoint.X - origin.X) / zoom),
            (float)((viewportPoint.Y - origin.Y) / zoom));
    }

    protected override void OnPaint(PaintEventArgs pe)
    {
        if (Image == null)
        {
            pe.Graphics.Clear(BackColor);
            return;
        }

        pe.Graphics.Clear(BackColor);
        pe.Graphics.SmoothingMode = SmoothingMode.None;
        pe.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        pe.Graphics.PixelOffsetMode = PixelOffsetMode.Half;

        var scaledWidth = Image.Width * zoom;
        var scaledHeight = Image.Height * zoom;
        var origin = GetImageOrigin();

        if (scaledWidth <= ClientSize.Width)
        {
            origin.X = (float)((ClientSize.Width - scaledWidth) / 2.0);
        }

        if (scaledHeight <= ClientSize.Height)
        {
            origin.Y = (float)((ClientSize.Height - scaledHeight) / 2.0);
        }

        var visibleLeft = Math.Max(0, (ClientRectangle.Left - origin.X) / zoom);
        var visibleTop = Math.Max(0, (ClientRectangle.Top - origin.Y) / zoom);
        var visibleRight = Math.Min(Image.Width, (ClientRectangle.Right - origin.X) / zoom);
        var visibleBottom = Math.Min(Image.Height, (ClientRectangle.Bottom - origin.Y) / zoom);

        if (visibleRight <= visibleLeft || visibleBottom <= visibleTop) return;

        var source = RectangleF.FromLTRB((float)visibleLeft, (float)visibleTop, (float)visibleRight, (float)visibleBottom);
        var destination = new RectangleF(
            (float)(origin.X + visibleLeft * zoom),
            (float)(origin.Y + visibleTop * zoom),
            (float)(source.Width * zoom),
            (float)(source.Height * zoom));

        pe.Graphics.DrawImage(Image, destination, source, GraphicsUnit.Pixel);
    }

    private PointF GetImageOrigin()
    {
        if (Image == null) return PointF.Empty;

        return new PointF(
            (float)(viewportAnchor.X - imageAnchor.X * zoom),
            (float)(viewportAnchor.Y - imageAnchor.Y * zoom));
    }
}
