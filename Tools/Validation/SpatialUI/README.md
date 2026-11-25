# Reactive spatial UI checks

These checks use small Unity API substitutes. They do **not** start Unity, compile against Unity assemblies, run Unity physics, render WebGL or certify headset comfort.

With an already installed .NET SDK, run from the repository root:

```bash
python3 Tools/Validation/SpatialUI/verify.py --dotnet /path/to/dotnet
```

Nothing is installed or downloaded. Temporary test assemblies are removed. Both legacy-input and WebGL/Input System builds execute the same behavioral checks.

Coverage includes:

- Mac-synced script scope, preserved Inspector fields and embedded media, and absence of screen-space UI / projected text.
- World-plane button input, click guards, scene/card occlusion, graphical reuse, text cells, pointer contrast and mouse-only free look.
- Shared spatial slots with a 0.35-second / 32-degree comfort zone, bounded recentering, viewer movement, hover stability, and common distance/scale adjustment near environment surfaces.
- Rendering priorities: one opening/order card, decision plus optional easy note, one evaluation, and the live order note containing its own timer.
- Recipe grading for all three trials; 90/60/45-second limits, 45-second live orders, 15 stir/shake round trips, and independent-trial confirmations without recipe hints.
- Ten-customer completion and evaluation snapshots, correct ID/intoxication refusals, failures, accuracy grades and the unsafe-sale override.

`geometry.py` reads real card dimensions and slot coordinates from the production code. It projects the complete frame quadrilaterals across 30 nearby eye/view poses and tests polygon separation, rather than comparing axis-aligned bounding boxes of tilted cards.

Substitute physics uses controlled test hits. It verifies handling of occlusion/clearance, not Unity's collision implementation. Test files remain outside Assets and are not shipped by the five-script Mac sync. Verify actual layout, motion comfort, fonts, input and WebGL on the Mac.
