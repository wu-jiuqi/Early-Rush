# Art V4 Unity review (not gameplay)

Source: user-provided D:\EarlyRush\output\art-v4. Source files preserved.

Editor: Early Rush > Art V4 > Create Review Scene or Build Windows Review.
CLI Unity: -executeMethod ArtV4ReviewBuild.Build
Review exe: --art-v4-capture exports camera-rendered 3 starts, 9 task areas and 3 overviews to QA/art-v4. These are actual composed Unity geometry/sprites, not enlarged source overview JPEGs. Screenshot labels are names and accompanying README; interactive viewer permanently labels itself REVIEW ONLY.

Controls: 1/2/3 chapter; WASD pan; wheel zoom; F overview; T next task location.
Scale: 40 layout px per world unit. Sprite aspect preserved, bottom-center pivot, footprint-Y sorting. Floors are capsule/room mesh unions with shared global texture phase; unlike old background, walls and travel area are independently constructed. Static renderer bounds culling is enabled; objects are still instantiated. No runtime memory/performance claim is made without profiling.

Limits: collision and tasks are NOT integrated; no character controller or timer; metro portal is only a pair of disconnected art areas. No claim of playable 7–10 minutes. No V13 gameplay route/state machine modified. Original PNGs reused, scene geometry primitives serve floor construction, not placeholder sprite art. Props containing a whole facade cannot yet support split-roof occlusion. Mainline markings and complex zone graphic details are simplified in this first review scene. Outside walkable areas use the scene's contrasting massing color, not traversable floor.
