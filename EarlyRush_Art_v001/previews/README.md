# v001 review previews

- `contact-sheet.png`: all 16 candidates, complete exported canvases. A means an existing runtime key; B requires a new program interface. Neither means visual acceptance.
- `scale-calibration-1280x720.png`: 1 world unit = 128 px; exported PNGs use their complete canvas, configured world width and pivot. Crosses mark pivots, not collision boundaries. Approximate ground anchors still require in-engine review.
- The desk is `Assets/Resources/RushArt/props.png`, zero-based index 12 of a 4x4 sheet. The door is `extras.png`, index 11. Both use the original fractional source rectangles. Their 2.25 / 2.40 world-unit widths and `(0.5, 0.1)` reference pivots are provisional layout aids, not final map dimensions. These old images also use an older viewing angle and are explicitly labelled LEGACY REFERENCE ONLY.
- `alpha-backgrounds.png`: normal source-over compositing on cream, dark teal and bright purple. Artwork and alpha remain unchanged.
- `asphalt-2x2.png`: four exact copies of the 512px export, no padding or drawn seam lines. Inspect the centre axes for visible repetition; numeric border differences alone cannot establish seamlessness.
- `review.html`: local viewer with a background selector and a three-frame windup review. Its optional 6 fps loop is a review aid only, not game timing or an approved looping animation.

Regenerate with `pwsh -File Tools/Preview-ArtV001.ps1` after exporting the package. This script writes previews only.