## Drawing

This folder contains low-level bitmap and ANSI presentation code that turns Klooie frames into terminal output.

- Keep rendering changes allocation-conscious and deterministic because this code runs every frame.
- Prefer ANSI-native screen management over host-native console APIs when the app is in fancy/ANSI mode.
- When changing frame clearing or cursor/color state, preserve the painter's internal state model so later frames remain correct.
- Frame-shedding budgets should only cut work at stable visual boundaries like whole rows; avoid stopping mid-row because that can render backgrounds without their foreground glyphs.
- `PaintDetailSettings.DetailPercent` is the public consumer-facing paint-quality knob. A value of `50` maps to the historical compressor constants. Lower values increase color tolerance, allow longer softened runs, and react to backpressure earlier; higher values tighten tolerance and shorten softened runs. Tune the `LowestDetailProfile`, `DefaultProfile`, and `HighestDetailProfile` endpoints instead of scattering detail math through the painter.
