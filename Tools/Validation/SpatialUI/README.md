# Spatial UI source checks

These checks use small Unity API substitutes. They do **not** compile against Unity assemblies, start Unity, render the game, run Unity physics, or certify a Web build / VR headset.

With an already installed .NET SDK, run from the repository root:

```bash
python3 Tools/Validation/SpatialUI/verify.py --dotnet /path/to/dotnet
```

Nothing is installed or downloaded. Temporary test assemblies are removed. The checks cover:

- Changes confined to the five Mac-synced scripts; Inspector fields and embedded audio/art unchanged from the pre-conversion baseline.
- No IMGUI, screen-space canvas modes or projected screen labels in Assets scripts.
- Static 16:9 perspective projection of production workbench UI coordinates, checking viewport bounds and ingredient-label overlap.
- C# source compilation with legacy input and WebGL/new-input symbols, using API substitutes.
- World-plane button hits, first-frame click protection, one action per frame, scene/nearer-panel occlusion, hidden/disabled/distant panels, head rotation leaving the sign in place, and graphic reuse.
- Three recipes in all three trials, 90/60/45-second limits, unordered ingredients, 15 stir/shake round trips, completion checks, wrong ingredients and the 45-second customer timeout.
- Third-round selection rings and receipts without recipe/quantity hints; rack labels staying in place while bottles animate.
- Name/status text cells staying inside their boards, bounded font fitting and an enlarged outlined world pointer with click response.
- Recipe reading stands, customer service choices, easy guides, order tickets and shift completion UI states.

The substitute physics exposes an explicit blocking flag; those checks verify the caller's handling, not Unity's collision implementation. Verify real layouts, fonts, input, occlusion and all transitions on the Mac. Test files are outside Assets and are not included in Unity or the five-script Mac sync.
