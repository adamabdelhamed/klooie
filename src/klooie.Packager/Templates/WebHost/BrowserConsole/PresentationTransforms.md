# Console Bitmap Presentation Transforms

Browser-hosted klooie apps can request renderer presentation transforms through `ConsoleApp.Presentation`. The API uses normal console bitmap coordinates and `ILifetime`; the Blazor/WebGL renderer owns viewport fitting, mobile safe areas, animation, and composition.

```csharp
// Keep a small top-left HUD region enlarged while this screen is alive.
ConsoleApp.Current.Presentation.ScaleRegion(
    new Rect(0, 0, 18, 3),
    1.6f,
    new RegionScaleOptions { Anchor = ConsoleBitmapPresentationAnchor.TopLeft, OffsetX = 1, OffsetY = 1 },
    screenLifetime);

// Use the Func overload when the source rectangle moves or resizes after layout.
ConsoleApp.Current.Presentation.ScaleRegion(
    () => hud.AbsoluteBounds.ToRect(),
    2f,
    new RegionScaleOptions { Anchor = ConsoleBitmapPresentationAnchor.TopRight },
    hud);

// Temporarily focus a bottom command bar while preserving bottom docking.
var focusLifetime = DefaultRecyclablePool.Instance.Rent();
ConsoleApp.Current.Presentation.FocusRegion(
    new Rect(0, ConsoleApp.Current.Height - 3, ConsoleApp.Current.Width, 3),
    new FocusRegionOptions { Anchor = ConsoleBitmapPresentationAnchor.Bottom, Padding = .05f },
    focusLifetime);
```

For CLAWS, keep the HUD, command bar, dialogs, galleries, and help panes authored normally in klooie controls. When a mobile/web screen needs emphasis, create presentation requests for the relevant console rectangles and bind them to the same screen, dialog, or interaction lifetime. Dispose or replace the lifetime when the region no longer applies, especially for regions whose bottom/right coordinates change after resize.
