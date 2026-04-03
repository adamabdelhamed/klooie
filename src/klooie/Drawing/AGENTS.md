## Drawing

This folder contains low-level bitmap and ANSI presentation code that turns Klooie frames into terminal output.

- Keep rendering changes allocation-conscious and deterministic because this code runs every frame.
- Prefer ANSI-native screen management over host-native console APIs when the app is in fancy/ANSI mode.
- When changing frame clearing or cursor/color state, preserve the painter's internal state model so later frames remain correct.
