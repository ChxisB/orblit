# orblit_gallery

Runs one worked example, full window, so a frame of it can be looked at.

The examples are also shown inside the editor, which opens on a project list —
so nothing draws there until somebody clicks, and neither a frame smoke nor a
person trying to reproduce a rendering fault can get at them. This opens
straight into a scene.

```sh
flutter run -d macos                        # the first example
ORBLIT_EXAMPLE=Decals flutter run -d macos  # a particular one
```

Everything below is driven by the environment, so a sweep of camera angles is a
shell loop rather than a person dragging:

```sh
for yaw in 0 45 90 135; do
  ORBLIT_EXAMPLE=Shadows ORBLIT_YAW=$yaw ORBLIT_SECONDS=2 ./orblit_gallery
done
```

On Android the same names are passed as intent extras:

```sh
adb shell am start -n dev.orblit.orblit_gallery/.MainActivity \
  --es ORBLIT_EXAMPLE Decals --es ORBLIT_DUMP_FRAME 30
```

On the web they are compile-time, not run-time — `flutter build web
--dart-define=ORBLIT_EXAMPLE=Decals`. Only the handful listed in
[orblit_env_web.dart](lib/orblit_env_web.dart) are wired up there.

## Choosing and framing

| | |
| --- | --- |
| `ORBLIT_EXAMPLE=Blocks` | which one, by name (default: the first) |
| `ORBLIT_MENU=0` | no example chooser and no rotate button, whatever the screen size |
| `ORBLIT_YAW` / `ORBLIT_PITCH` / `ORBLIT_DISTANCE` | where to look from |
| `ORBLIT_SECONDS` | hold the clock still, for a scene that animates |
| `ORBLIT_WALK=0` | give the camera back, for an example that drives it |
| `ORBLIT_MESH` | a path to a `.glb` or `.gltf`, shown on its own |
| `ORBLIT_MORPH` | comma-separated shape weights for that model |
| `ORBLIT_DUMP_FRAME=30` | write frame 30 to a file and quit — see `OrblitSurface.h` |
| `ORBLIT_BACKEND` | which Filament backend to open |
| `ORBLIT_PACE` | how fast frames are handed to the renderer |

## Over any example

| | |
| --- | --- |
| `ORBLIT_RANGE` | how far a population is drawn from; 0 draws it all |
| `ORBLIT_TREES=0` | leave the trees out |
| `ORBLIT_SHARPEN` | run the sharpen effect, 0 to 1, over the frame |
| `ORBLIT_EFFECT` | an effect by name, shown on its own over the frame |
| `ORBLIT_SMAA=1` | the whole three-pass SMAA chain |
| `ORBLIT_AA` | `off`, `fxaa` or `temporal` |
| `ORBLIT_BATCHING=0/1` | batching off or on, over the default (on), so the same frame can be drawn both ways and compared |
| `ORBLIT_PREPASS=0/1` | the depth prepass off or on, so the same frame can be timed both ways |
| `ORBLIT_SHADOWS=0` | no shadow pass |
| `ORBLIT_POST=0` | no post-processing |
| `ORBLIT_DETAIL` | which `OrblitDetail` level the pipeline runs at |
| `ORBLIT_GODRAYS` | how strong the god rays are; over any example but God rays itself, turns them on |

`ORBLIT_CULLING=0` draws everything in the scene whether the renderer thinks it
is on screen or not. It is the one switch that tells a bounding box in the
wrong place from a thing that was never drawn: what the frustum test wrongly
throws away is exactly what comes back when it is turned off.

## Per example

### Lights, Probes and the light field

| | |
| --- | --- |
| `ORBLIT_LIGHT` | which light the Lights example shows |
| `ORBLIT_LUMENS` | how bright it is |
| `ORBLIT_PROBE` / `ORBLIT_PROBE_OFF=1` | the Probes example's intensity, or none |
| `ORBLIT_ROUGHNESS` | how rough the surface it lights is |
| `ORBLIT_FIELD` / `ORBLIT_FIELD_OFF=1` | the light field's intensity, or none |
| `ORBLIT_RETENTION` | how much of the field carries between frames |
| `ORBLIT_BOUNCE_OFF=1` | turn the Bounced light example's effect off |
| `ORBLIT_BOUNCE` | with `ORBLIT_EFFECT=bounce`, how much light bounces |
| `ORBLIT_BOUNCE_RADIUS` | how far it looks, in metres |
| `ORBLIT_BOUNCE_THICKNESS` | how solid the depth buffer's surfaces are |
| `ORBLIT_BOUNCE_SLICES` | how many directions each pixel fans along |

### Shadows

| | |
| --- | --- |
| `ORBLIT_SHADOW_LIGHT` | which light casts: `Sun`, `Spot` or `Point` |
| `ORBLIT_SHADOW_KIND` | its edge: `Sharp`, `Soft`, `Area` or `Variance` |
| `ORBLIT_SHADOW_MAP` | the map's size in pixels |
| `ORBLIT_SHADOW_CASCADES` | how many cascades a sun's map is split into |
| `ORBLIT_SHADOW_SPLIT` | place the splits by hand, the first at this fraction of the shadow distance |
| `ORBLIT_SHADOW_CONTACT=1` | screen-space contact shadows |
| `ORBLIT_SHADOW_CONTACT_DISTANCE` | how far each pixel marches, in metres |
| `ORBLIT_SHADOW_SIZE` | the light's size in metres, for the `Area` edge |
| `ORBLIT_VSM_BLUR` | the `Variance` edge's blur, in texels |
| `ORBLIT_PANEL_SHADOW=0` | the Panel shadows example's panel casts nothing |
| `ORBLIT_PANEL` / `ORBLIT_PANEL_HEIGHT` | its panel's edge in metres, and how high it hangs |
| `ORBLIT_PANEL_W` / `ORBLIT_PANEL_H` | the panel's size in metres |

### Environment volumes and decals

| | |
| --- | --- |
| `ORBLIT_VOLUME_AT` | where the walk stands, 0 in the courtyard to 1 at the back of the hall; stops the walk, and prints what the volumes resolved to |
| `ORBLIT_VOLUMES_OFF=1` | the same place with the volumes left out |
| `ORBLIT_VOLUME_BLEND` | how far outside the hall its look reaches, in metres |
| `ORBLIT_DECALS_OFF=1` | paint none of the Decals example's decals |
| `ORBLIT_DECAL_ONLY` | paint only that one of them, counting from 0 |
| `ORBLIT_DECAL_FADE_OFF=1` | turn their angle fade off |
| `ORBLIT_DECAL_MASK_OFF=1` | let the paint splash reach the crate's layer |

### Outline

| | |
| --- | --- |
| `ORBLIT_OUTLINE=0` | the Outline example with nothing outlined |
| `ORBLIT_OUTLINE_OTHERS=0` | outline only the active object |
| `ORBLIT_OUTLINE_WIDTH` | how wide the outline is, in pixels |
| `ORBLIT_OUTLINE_HIDDEN` | `shown`, `faint`, `dashed` or `hidden`: the part a wall hides |

### Gaussian splats

| | |
| --- | --- |
| `ORBLIT_SPLAT` | a `.ply` or `.splat` capture to show instead of the generated ring |
| `ORBLIT_SPLAT_COUNT` | how many splats the generated ring has |
| `ORBLIT_SPLAT_SORT=0` | draw them unsorted, to measure what the sort does |
| `ORBLIT_SPLAT_PILLAR=0` | take the solid pillar out of the ring |
| `ORBLIT_SPLAT_HARMONICS` | how many bands of a capture's view-dependent colour to read: 0, 1, 2 or 3 |
| `ORBLIT_SPLAT_LIMIT` | the most splats to draw, whatever the device says |
| `ORBLIT_SPLAT_COARSE=1` | sort on sixteen bits of depth rather than 32 |
| `ORBLIT_SPLAT_DEVICE=0` | ignore the device's own splat budget, degree and sort, and draw what is asked for instead |

### Imported models and textures

| | |
| --- | --- |
| `ORBLIT_MODEL` | which Imported models sample: `Fox`, `Shoe`, `Lamp`… |
| `ORBLIT_CLIP` | which of its clips plays, from 0 |
| `ORBLIT_VARIANT` | which of its material variants it wears, from 0 |
| `ORBLIT_DAYLIGHT` | 0 to take the sun down, so a file's lights show |
| `ORBLIT_SAMPLES` | where `tool/fetch_import_samples.sh` put them |
| `ORBLIT_BISTRO` | where the Bistro scene's glTF lives |
| `ORBLIT_TEXTURES` / `ORBLIT_TEXTURE_FILES` | a directory of textures, or a comma-separated list |
| `ORBLIT_PICTURE` | one picture, shown on its own |
| `ORBLIT_TEXTURE_SIZE` | the largest texture to keep, in pixels |
| `ORBLIT_VIDEO` | a video file for the Video example |

### Batching and overdraw

| | |
| --- | --- |
| `ORBLIT_CRATES` | how many crates the Batching example draws |
| `ORBLIT_PALETTE` | its colours: `One`, `Six` or `Every` one |
| `ORBLIT_BATCH_MATERIAL=1` | its crates made of one shared material |
| `ORBLIT_BATCH_MESH` | a `.glb` for its crates, instead of the cube (which only batches alongside `ORBLIT_BATCH_MATERIAL=1`) |
| `ORBLIT_MOVING=0` | hold its turning crate still |
| `ORBLIT_SLABS` | how many slabs the Overdraw example crosses |

### Weather and god rays

| | |
| --- | --- |
| `ORBLIT_WEATHER` | `Clear`, `Fair`, `Storm`, `Misty`, `Rain` or `Snow` |
| `ORBLIT_RAIN` | how hard it is coming down |
| `ORBLIT_SUN_BEARING` | the God rays sun's bearing in degrees, 180 behind the camera |
| `ORBLIT_SUN_ALTITUDE` | and its height above the horizon |
| `ORBLIT_COVER` | the God rays sky's cloud cover, 0 to 1 |
| `ORBLIT_GODRAY_SAMPLES` / `_DECAY` / `_DENSITY` | the rest of its settings |

`ORBLIT_MIST` is how much of the air is drawn as banks of cloud, and
`ORBLIT_DENSITY` how much as even haze. They are separate because every
condition raises the two together, and haze thick enough to go with a real bank
of mist is haze thick enough to hide whether the bank was drawn at all.

### Distortion and motion blur

| | |
| --- | --- |
| `ORBLIT_SHOCKWAVE=0` | leave the Distortion example's wave out |
| `ORBLIT_HAZE=0` | and its heat haze |
| `ORBLIT_LENS` | its lens warp, negative for pincushion |
| `ORBLIT_CHROMATIC` | its chromatic split |
| `ORBLIT_WAVE` | its wave's strength |
| `ORBLIT_MOTION=0` | turn the Motion blur example's blur off |
| `ORBLIT_MOTION_OBJECTS=0` | blur by the camera's motion only |
| `ORBLIT_PAN=1` | pan the Motion blur example's camera |
| `ORBLIT_SHUTTER` | its shutter, in seconds |
| `ORBLIT_CIRCLING=0` | stop it going round, so two renders compare |

### Runner

| | |
| --- | --- |
| `ORBLIT_AUTOPILOT=1` | let it play itself, which is the fairness test |
