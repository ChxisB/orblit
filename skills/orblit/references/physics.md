# Physics

Rigid bodies live in their own repository, `https://github.com/ChxisB/orblit-physics.git`,
as two packages under `packages/<name>`. Names below were checked against
`orblit_physics` 0.3.0 and `orblit_physics_scene` 0.1.1. A git dependency
resolves to `${PUB_CACHE:-$HOME/.pub-cache}/git/orblit-physics-<commit>/`, and
each package's `lib/<name>.dart` lists its exports.

```yaml
dependencies:
  orblit_physics:
    git:
      url: https://github.com/ChxisB/orblit-physics.git
      path: packages/orblit_physics
  orblit_physics_scene:
    git:
      url: https://github.com/ChxisB/orblit-physics.git
      path: packages/orblit_physics_scene
```

- **`orblit_physics`** is the solver: C++ behind a build hook, driven from
  Dart, depending on nothing else in Orblit. Native platforms only; **not the
  web**.
- **`orblit_physics_scene`** exports one class, `ScenePhysics`. It simulates
  every entity in a scene document that has a `body` component and answers
  each step with a `SceneDiff`. It does not re-export `orblit_physics`, so
  import both when you need `Shape` or `PhysicsEventKind`.

Guide: https://orblitengine.com/docs/guides/physics/

## The world

```dart
final physics = Physics(settings: const PhysicsSettings(gravity: [0, -9.81, 0]));

physics.add(1, shape: const Shape.plane(0, 1, 0), motion: PhysicsMotion.fixed);
physics.add(2, shape: const Shape.sphere(0.5), at: [0, 3, 0], restitution: 0.6);

physics.step(1 / 60);                 // one call per tick, same delta each time
final pose = physics.transformOf(2);  // Float32List: x, y, z, then qx, qy, qz, qw
physics.dispose();
```

- `add(id, {required shape, motion = free, at, rotation (xyzw), mass,
  friction, restitution, linearDamping, angularDamping, layers, asleep})`.
  `id` is any non-zero int; adding one already there is ignored.
- `remove(id)`, `place(id, at:, rotation:)` (teleport, forgets velocity),
  `drive(id, velocity:, spin:)` (how a driven body is moved), `push(id,
  impulse:, at:)` (N·s at a world point; off-centre spins it), `wake(id)`.
- Reading: `transformOf`, `velocityOf` (six floats), `alive`, `asleep`,
  `count`, and `readInto(ids, Float32List out, stride:)` for many at once.
- `events` is `List<PhysicsEvent>` for **the last step only**: `kind`
  (`touchBegan`, `touchEnded`, `slept`, `woke`), `a`, `b` (smaller id first),
  `at`, `normal`, `force` (N·s).
- `cast(from:, direction:, distance:, shape:, rotation:, layers:, ignore:)`
  returns `PhysicsHit?` with `body`, `at`, `normal`, `distance` and `started`
  (the cast began inside the body). No shape casts a point, which is a ray.
- `Layers(is_: bits, cares: bits)`. Two bodies meet when *either* cares about
  the other.
- `PhysicsMotion.fixed` never moves, `driven` goes where it is sent and is
  never pushed back, `free` falls and is pushed.
- `PhysicsSettings` fields left null take the engine's defaults.

## Characters

Anything that walks, whether a player or anyone else, is a character, not a
free body. It is swept through the world rather than solved. It slides along
walls, climbs steps, stands on slopes and slides down steeper ones, and rides
whatever it stands on.

```dart
const player = 7;
physics.addCharacter(player, at: [0, 0.91, 0]); // centre: 0.9 above the feet, plus the skin

// Every step, before physics.step:
final footing = physics.footingOf(player)!;
final up = footing.grounded && jumpPressed ? 5.0 : footing.velocity[1] - 9.81 / 60;
physics.drive(player, velocity: [walkX, up, walkZ]);
physics.step(1 / 60);
```

- `addCharacter(id, {shape = Shape.capsule(0.3, 0.6), at, rotation,
  stepHeight = 0.3, steepest = pi / 4, skin = 0.01, strength = 500,
  friction = 0.5, layers})`. `steepest` is in radians. `strength` is the
  most force, in newtons, it pushes a free body with. It keeps `skin` metres
  from everything.
- `drive(id, velocity:)` is a **request**, in metres a second, relative to
  what the character stands on, **with gravity in it**. The world decides how
  much of it the character gets. `spin` is ignored: which way it faces is the
  game's to keep.
- `footingOf(id)` and `footingsOf(ids)` give `PhysicsFooting?`, which is null
  for anything that is not a character. Its fields:
  - `ground`: the body underneath, or 0 for none.
  - `normal`: which way that surface faces.
  - `velocity`: what survived of the request, relative to the ground.
  - `carried`: how fast the ground moved it.
  - `turning`: how fast the ground turned it about up, in radians a second.
  - `grounded`.

  Play the walk animation from `velocity`, not from `velocity` plus
  `carried`, so a character on a lift walks nowhere.
- It pushes free bodies and is pushed only by driven ones. A rolling crate
  stops against it; a closing door shoves it aside.
- `place` teleports it and forgets what it stood on.

**Root motion drives a character.** A clip's `RootStep` is where the
animation wants to go, and the character decides where it actually gets. Advance
the clip by the same tick as the world. Turn the step into world space,
divide it by the tick, and ask for that:

```dart
final step = clipPlayer.advance(tick);
final walk = heading.asRotationMatrix().transformed(step.moved.position) / tick;
heading = (heading * step.moved.rotation)..normalize();
physics.drive(player, velocity: [walk.x, up, walk.z]);
physics.step(tick);
```

Do not add the step to a position as well; the character's transform is the
position now. A clip whose `RootMotion` `rises` carries its own up and down,
so ask for `walk.y` in place of the fall while it plays.

## The body component

`BodyComponent` in `orblit_scene` (`SceneComponents.body`, JSON key `body`).
Its fields are `shape` (`BodyShape.box`, `sphere`, `capsule`, `plane`),
`size`, `radius`, `height`, `centre`, `motion` (`BodyMotion.fixed`, `driven`,
`free`), `mass`, `friction`, `restitution`, `linearDamping`, `angularDamping`,
`layers`, `cares` and `startsAsleep` (JSON `asleep`). Change one field with
`copyWith`.

- Sizes are in the entity's own units, so the body scales with the entity. A
  sphere takes the largest of the three scales. A capsule takes the larger
  sideways scale for its radius and the upright one for its height.
- `plane` is endless ground facing the entity's up, through `centre`. It
  never moves, whatever `motion` says.

## A document, simulated

```dart
final view = OrblitDocumentView(document);
final scene = ScenePhysics(document);   // step: 1 / 60, maxSteps: 8

// Every frame, from the Ticker's delta in seconds:
final moved = scene.advance(seconds);
if (!moved.isEmpty) setState(() => view.apply(moved));

// Every edit goes to both:
final diff = SceneDiff.between(scene.document, next);
scene.apply(diff);
view.apply(diff);

// By entity id:
scene.physics.push(scene.bodyOf('crate')!, impulse: [40, 0, 0]);
for (final event in scene.events) {
  print('${scene.entityOf(event.a)} ${event.kind.name} ${scene.entityOf(event.b)}');
}

scene.dispose();
```

## Traps

- **`Shape.box` takes half sizes; `BodyComponent.size` is edge to edge.**
  `Shape.capsule(radius, halfHeight)` is the straight part either side of the
  middle; `BodyComponent.height` is tip to tip, ends included.
- **Rotations differ.** The world takes and returns quaternions as x, y, z, w.
  A scene transform's rotation is degrees, applied Z, then Y, then X.
- **Read `scene.events`, not `scene.physics.events`.** One `advance` can take
  several steps or none. The world keeps only its last step's events, so its
  list loses collisions on a slow frame and repeats them on a fast one.
- **Never pass `advance`'s diff to `scene.apply`.** It is already in
  `scene.document`. Applying it again rebuilds every body that moved and
  stops it dead.
- **Edits are teleports.** `apply` rebuilds each body the diff touched, and
  every body under it, at rest. That includes moving, rescaling and changing
  the body.
- **Ids.** Numbers from 1 up are `ScenePhysics`'s. A body added straight to
  `scene.physics` needs a negative id; it is simulated but never written
  back. `bodyOf` and `entityOf` convert between entity and body; an entity
  keeps its number while it has a body.
- **A body falling asleep reports `touchEnded`** for what it rests on. Do not
  read `touchEnded` alone as "left the floor"; check `physics.asleep(id)`.
- **A plane cannot be cast.** Asking gives null.
- **A character does not fall on its own.** Leave gravity out of `drive` and
  it hangs in the air.
- **Build each request from the footing, not from the last request.** Read
  `footingOf` before `drive`. Its `velocity` has already lost whatever the
  floor or a wall stopped, so a character standing still does not pile up a
  fall it is not taking and then drop through the next hole at full speed.
- **A character on the way up is never grounded,** so `grounded &&
  jumpPressed` launches it once. It does not keep adding the launch every step
  while the key is held.
- **Drive a character before every step.** Left alone, it keeps what survived
  of its last request and gains no more gravity, so it floats.
- **`ScenePhysics` makes no characters.** `BodyComponent` has no character
  motion. Add one straight to `scene.physics` with a negative id, and move its
  entity yourself from `transformOf`. `advance` takes several steps or none,
  which a character driven once a frame cannot follow. Keep your own clock
  instead, and on each tick drive, then call `scene.advance(scene.step)`,
  which takes exactly one step.
- **A plane cannot be a character.** `addCharacter` takes it, but it never
  has a footing.
- **Name clashes.** `Shape` and `Layers` are exported by both
  `orblit_physics` and `orblit_collide`. Prefix one import.
- **Not deterministic across machines.** The same inputs at the same step on
  one machine replay exactly; two machines drift. Fine for state-synced
  multiplayer, wrong for lockstep.
- **No continuous collision.** Small fast things tunnel. Use a smaller `step`,
  or `cast` along the path first.
- **No joints.**
- **The editor** draws bodies (an inspector section and a wireframe) but does
  not simulate them.
