# Project constraints

- The shipping target is WebGL. Favor browser-compatible rendering and small per-frame CPU/allocation budgets.
- Preserve the saved GroundFloor bake, light probes, encounter sequence and user-edited layout.
- Keep the Office Forward renderer and browser lighting budget when updating or rebaking lighting. Avoid restoring multiple real-time point-light shadows or HDR without profiling.
- Keep assistant physics scans non-allocating and throttled. Cache HUD text instead of constructing arrays during every IMGUI event.
- The user prefers low credit usage; keep investigation and verification focused.
