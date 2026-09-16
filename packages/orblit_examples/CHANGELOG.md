# Changelog

## 0.27.0

- **An Imported models example.** Khronos's glTF samples, a Mixamo FBX and an
  OBJ with its material library, read as bytes and handed to the renderer, so
  it works in a browser too. Each file's clips and material variants are
  offered by the names the renderer reports back, a change of clip fades, and
  a lamp's own lights shine at night. `tool/fetch_import_samples.sh` fetches
  the files; `ORBLIT_MODEL`, `ORBLIT_CLIP`, `ORBLIT_VARIANT`, `ORBLIT_DAYLIGHT`
  and `ORBLIT_SAMPLES` choose from a shell or a URL.
- `Example.models`, what each model an example's scene named holds, filled by
  the host from `OrblitView.onAssetInfo`.
- In a browser an example's files are read with the browser's own fetch.

## 0.26.0

- **A Scene files example, which reads a scene rather than building one.**
  Every other example states its scene in Dart; this one parses an `.oscene`
  document — the same text the editor saves — and hands what comes out to the
  renderer, in 2D and in 3D. Neither document names a file on disk, so it
  works in a browser and in a sandbox. Its slider edits the document and
  applies the difference as a `SceneDiff`, which rebuilds the one entity that
  moved and leaves the rest as objects the renderer already has.

## 0.25.0

- **The Gaussian splats example scales itself to its device.** It is handed
  what the view's renderer measured — `OrblitView.profileOf`, which the
  gallery now gives to every example as `Example.device` — and holds its cloud
  to it: no more splats than the device's budget, harmonics no higher than its
  degree, and a coarse sort where a full one would lag a turning camera. A
  toggle turns the limits off, so what they cost and what they save can be
  seen rather than argued about.

## 0.24.0

- **Sprites.** A 2D scene drawn by the same renderer as the 3D ones: a sprite
  sheet painted in code and handed over as bytes, cut into frames with
  `orblit_sprite`, a backdrop that scrolls by moving its layer rather than
  resending a tile, spinning coins, and additive sparks — one draw a layer.

## 0.23.0

- **Every example batches now.** None of them changed; `OrblitScene.batching`
  defaults to true, so the fifty scenes here that never mentioned it get
  merged draws wherever four or more of anything repeat. Batching's own
  example keeps its switch, and its blurb no longer promises that flipping it
  moves nothing: one crate in five casts a shadow, and merging moves 2.27% of
  the frame's pixels along the edges of those shadows. Turn the shadows off
  and the two are bit-identical.

## 0.22.0

- **Panel shadows now shows a shadow.** The harness that proved the panel cast
  nothing is the one that proves it does: a soft shadow under each occluder,
  softer at the tall one's far end than at its foot.

- **Ten new examples.** Shadows puts every shadow setting on a slider.
  Environment volumes walks from a sunny courtyard into a dim, foggy hall.
  Decals paints a poster, a scorch, a puddle, a stripe across the corner of a
  room and a splash around a crate that the crate's own layer is spared.
  Gaussian splats is a generated striped ring with a solid pillar standing in
  it, or any capture named by `ORBLIT_SPLAT`. Outline puts a selected box half
  behind a wall. Batching draws thousands of crates in one, six or every
  colour, and Overdraw crosses heavy surfaces through each other to measure
  what a depth prepass could save. God rays stands a low sun behind a row of
  pillars, and Distortion sends a shockwave across a striped floor, with a
  heat haze rising over a vent and a lens warp on a slider. Motion blur spins
  a fan and slides a plate past a still box and a striped wall, with the
  shutter on a slider from 1/1000 s to 1/30 s.

## 0.21.0

- **A new example, Panel shadows.** A rectangular light over a pale floor with
  three occluders at three heights, and a toggle for whether it casts. There was
  nowhere to look at a rectangular light before this: the Lights example has a
  Panel setting, but its camera sits nearly horizontal, which shows the lit
  sides of things and almost none of the floor they stand on — and a shadow
  lives on the ground.

  It is also the harness that shows the shadow does not work yet. With the
  toggle on and off the two frames differ only by dithering, with no shadow
  shape anywhere in the difference.

## 0.20.0

- **A new example, Reflection probes.** A chrome box turned a half-right angle
  in a room with one red wall and one blue one, so that two of its faces are
  visible at once with a different wall in each. A box square to the camera
  shows only the face reflecting what is behind the camera, which is the one
  face that says nothing about the room it is in.
## 0.19.0

- The Lights example has a fourth kind, **Panel**, with its two edges as
  settings — so the thing area lights are for is visible by dragging: the
  same lumens spread wider and softer as the panel grows, rather than getting
  brighter.
## 0.18.0

- **A new example, Irradiance field.** The same room as the bounced-light one
  on purpose. What is worth watching is not that the walls tint the boxes —
  the bounce does that too — but that the tint is right for where a surface
  *is* rather than for what happens to be in shot.

## 0.17.0

- **A new example, Bounced light.** Two coloured walls facing each other across
  a pale floor, with white boxes between them — the arrangement every renderer
  has been photographed in since Cornell in 1984, because it is the one that
  shows bounced light plainly. The sky's ambient is turned right down on
  purpose: ambient fills shadows evenly and for free, and a room where it does
  that is a room where a second bounce has nothing left to add.

## 0.16.0

- Blocks stops taking itself apart at the draw distance. It fades nothing —
  a solid world has no intermediate shape a cube can hold without tearing it —
  and pairs the range with fog worked out from that range, so the boundary is
  reached inside opaque air rather than in plain sight.

## 0.15.0

- Blocks fades its distance by shrinking rather than sinking, which is what
  stops the far side of the world turning into coloured plates hanging in the
  sky. A block sits on other blocks, not on the ground, so sinking one to its
  own bottom left it exactly where it was.

- The Runner supplies its own camera and cannot be orbited. A runner is
  followed, not looked at: its world only exists in a wedge in front of the
  character — the track is laid one way, there is nothing behind it, and the
  scenery is drawn to a range measured from the camera — so orbiting round to
  the side, or under the ground plane, showed the empty half, with hazards and
  coins over a horizon and the road they belong to out of frame. It leans with
  the runner as it weaves, because a camera welded to the centre line makes
  that read as the world sliding sideways rather than the character moving.

## 0.14.0

- An example can say what it needs that the repository does not carry, and
  under what terms. The Bistro scenes are somebody else's art and hundreds of
  megabytes of it, so they are fetched rather than committed — and until they
  are, the example drew a placeholder and explained itself to somebody who
  then had to go and find a shell script. A description rather than a
  download: the example says what it wants and which command brings it, and
  whatever is showing it decides whether that becomes a button.

## 0.13.0

- Blocks generates a world rather than a heightfield. Four climate fields
  instead of one height: continentalness decides how far above the sea a
  region sits and is what makes coasts, erosion decides how much the land is
  allowed to vary there, and temperature and humidity decide what grows.
  Height is continentalness through a curve, scaled by erosion — the curve
  matters, because a straight line gives as much land at every altitude and a
  real world has a lot of coast, a lot of gentle ground and a little that is
  high. Surface rules by biome give beaches, deserts, a snow line that moves
  with temperature, and trees where it is warm and damp enough. The shape of
  that is Pebble's approach read as a reference and written again; none of its
  code is here.

- The runner's buildings stood in the road. The track is 4.6 either side of
  the middle and the scenery started at 4.2, so a tower could be in the third
  lane — which is what "it keeps generating really long objects" was: a
  building with the road running through it. They also varied only in height,
  all of them the same 1.8 across and up to eleven high, which is a six-to-one
  slab; a row of those beside the camera is a wall with slots in it rather
  than a city. And there is ground under them now, because a tower with
  nothing beneath it reads as a bug rather than as distance.

- Blocks is a world you are in rather than one you look at. WASD and space to
  walk and jump, drag to look, click to dig and right-click to put a block
  back. The world is a grid of bytes now instead of a list of what to draw —
  the moment somebody can dig, "what is at this point" is asked constantly, by
  the body falling and by every ray under the crosshair, and a grid answers
  that in one lookup rather than sixty thousand comparisons. Only blocks with
  air beside them are drawn, which in a world of solid hills is about an
  eighth of them.

## 0.12.1

- The runner's black shapes were shadows. Three things at once: the track was
  a twentieth of full brightness, so a shadow on it was simply black; the sky
  was dim enough that a shadowed surface got almost nothing; and the coins,
  which are thin discs, cast hard-edged rectangles onto the road with nothing
  above them to explain the shape. A lighter road, more sky, and coins that
  float without casting.

## 0.12.0

- Two whole small worlds rather than one technique each.

  **Blocks**: a generated landscape of sixty thousand cubes with grass, stone,
  snow and lakes, in one buffer uploaded when the world changes and not again.
  Only the block somebody can see is built — a column of height twelve is one
  cube, not twelve — and a column fills down to its lowest neighbour so a
  cliff is a wall rather than floating tops.

  **Runner**: a track that never ends and never allocates a piece of one. A
  fixed set of hazards, coins and roadside blocks laid out once; what changes
  is how far the world has come, and a piece that goes behind the camera comes
  round the front by a modulo. The runner does not run — it stays where it is
  and the world moves past.
## 0.11.0

- `Example` carries a `note`, so any example can say what the renderer told it
  about the scene. That report used to reach one example and be dropped for
  all the others, which is why a Bistro whose files had never been downloaded
  showed a grey placeholder cube and said nothing — and a scene that draws the
  wrong thing in silence reads as the engine being broken rather than as a
  file being absent.

## 0.10.0

- The Bistro exterior no longer judders. It was asking for four shadow
  cascades of two thousand square and contact shadows, which came to fifty-five
  milliseconds a frame with the camera looking down the street — twelve frames
  a second, arriving unevenly, which is what a judder is. Three cascades of a
  thousand and no contact shadows is within a millisecond and a half of having
  no shadows at all, and the walk now runs at a hundred and twenty frames a
  second with one percent unevenness. Resolution is adaptive as well, so what
  is in view changing between a wall and a hundred and seventy metres of street
  changes the pixel count rather than the frame rate.

## 0.9.0

- `Toggle` takes a `note` and an `enabled`, and the Bistro examples use it
  instead of `SwitchListTile`. A ListTile paints onto the nearest Material
  ancestor and reports itself broken when something opaque sits in between,
  which the settings panel is — so four switches were reporting an error on
  every frame they were on screen, and an editor showing the Bistro filled its
  console with them.

## 0.8.0

- A blending example: three ground panels that are the same two surfaces and
  the same mask, differing only in what the mask is taken to mean. The point
  is visible with the grass slider at half — the first two panels are half
  grass everywhere, and the third has grass in the mortar and stone on the
  stones. Its textures are generated rather than shipped, and the height it
  blends by is the cobble relief itself, which is what stops grass appearing
  where the stones are.

## 0.7.0

- The walk no longer goes through walls. The route is searched rather than
  chosen — a breadth-first flood of the open ground at half a metre, with the
  curve then checked at six hundred points for half a metre of clearance.
  Both earlier paths clipped: the polyline at 18 samples in 100, the curve
  through it at 17.

## 0.6.0

- The walk follows a Catmull-Rom curve rather than a polyline, so both where
  the camera is and where it points change smoothly the whole way. Measured
  at a peak of 84 degrees a second with no discontinuity across the cycle;
  the polyline turned instantly at every one of its waypoints.
- Walked at constant speed rather than constant parameter, because a spline's
  parameter is not its arc length.

## 0.5.0

- The walk turns round instead of reversing. Four phases on a loop — down the
  street, turn, back, turn — with the turn eased in and out so it starts and
  stops at zero speed.
- Temporal anti-aliasing, screen-space reflections, ACES tone mapping, contact
  shadows and stronger ambient occlusion.

## 0.4.0

- A prefiltered environment, built by `cmgen` when the scene is fetched. The
  flat one-band ambient is a placeholder by its own admission; this is a
  photograph of a real sky, with the reflection in its mip chain and the
  diffuse in its harmonics. It is most of the difference between a render and
  a photograph.
- The exterior walks the street instead of orbiting it, at a walking pace,
  with a bob and a slow look around. The route comes from an occupancy map of
  the scene rather than a guess — the first attempt followed the street lamps
  and walked through the restaurant, because the lamps stand on the pavement
  with the building between them.

## 0.3.0

- The Bistro's materials are repaired on fetch. All 132 omitted
  `metallicFactor`, and glTF's default is 1.0 — so every cobblestone, wall
  and awning was rendering as solid metal, which has no diffuse response and
  goes black whatever the lighting does.
- Night is lit like night: the moon at a few lux rather than 900, which was
  three thousand times a real one and flattened the whole street.
- Sliders for moonlight and film speed, four-sample anti-aliasing, ambient
  occlusion, and a shadow distance — `OrblitShadows.distance` defaults to 0,
  which leaves the shadow map covering nothing.

## 0.2.0

- Two Bistro examples, exterior and interior, lighting the Amazon Lumberyard
  Bistro (ORCA, CC BY 4.0). The first example built on somebody else's art,
  with reference images to judge the lighting against, and by a wide margin
  the heaviest thing here to measure a frame on.
- Fetched by `tool/fetch_bistro.sh`, which also derives the hundred-odd light
  positions from the scene's own emissive geometry — the conversion carries no
  `KHR_lights_punctual`, so the fixtures are ours to place.

## 0.1.0

- First cut of `orblit_examples`. Pre-alpha: everything is subject to change.
