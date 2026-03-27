# Gaming Movement

This folder contains low-level movement primitives shared across games.

- Keep hooks and diagnostics generic and opt-in.
- Do not introduce game-specific dependencies or policy into these files.
- Preserve the existing movement decision flow; add observability at stable boundaries rather than redesigning scoring or steering behavior.
