# Gaming Movement

This folder contains low-level movement primitives shared across games.

- Keep hooks and diagnostics generic and opt-in.
- Do not introduce game-specific dependencies or policy into these files.
- Preserve the existing movement decision flow; add observability at stable boundaries rather than redesigning scoring or steering behavior.
- `Walk` may now be used in an advisory mode via `ExternalMotionSink` plus `EvaluateNow()`. Keep that path generic: it should emit the chosen motion command without applying it to velocity, and callers remain responsible for any scheduling cadence or input simulation on top.
- `Walk.Tick()` is driven by `Vision.VisibleObjectsChanged`, so any per-tick LOS or collision probe inside movement scales directly with vision scan cadence. Cache same-tick point-of-interest LOS/close-enough results instead of recomputing them from multiple movement helpers.
