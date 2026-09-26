# Physics

Rigid bodies live in their own repository, `https://github.com/ChxisB/orblit-physics.git`,
as three packages under `packages/<name>`. Names below were checked against
`orblit_physics` 0.5.0, `orblit_physics_scene` 0.2.0 and
`orblit_physics_terrain` 0.1.0, and `orblit_scene` 0.9.0 for the components. A git dependency
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
  orblit_physics_terrain:
    git:
      url: https://github.com/ChxisB/orblit-physics.git
      path: packages/orblit_physics_terrain
```

- **`orblit_physics`** is the solver: C++ behind a build hook, driven from
  Dart, depending on nothing else in Orblit. Native platforms only; **not the
  web**.
- **`orblit_physics_scene`** exports one class, `ScenePhysics`. It simulates
  every entity in a scene document that has a `body` component and answers
  each step with a `SceneDiff`. It does not re-export `orblit_physics`, so
  import both when you need `Shape` or `PhysicsEventKind`.
- **`orblit_physics_terrain`** exports one class, `TerrainPhysics`. It lays
  an `orblit_terrain` `Terrain` as ground, the regions near a point, and lays
  a region again when it is edited.

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
  (`touchBegan`, `touchEnded`, `slept`, `woke`, `broke`), `a`, `b` (smaller
  id first), `at`, `normal`, `force` (N·s). For `broke`, `a` is the joint's
  id, `b` is 0, and `force` is what broke it.
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

## Ground

```dart
physics.layGround(
  hill,                       // any non-zero id, like any body
  heights: heights,           // columns * rows, row after row; nan is a hole
  columns: 65,
  rows: 65,
  spacing: 0.5,
  at: [-16, 0, -16],          // where sample (0, 0) is
);
```

- Sample (c, r) is `heights[r * columns + c]` at `at + (c * spacing, h, r *
  spacing)`. Each square is two triangles split from (c, r) to (c + 1, r + 1),
  the same split `Terrain.heightAt` and the terrain mesh use.
- Everything under the surface is solid: a sunk body is pushed up and out,
  never down through, even when the ground is raised under a sleeper.
- `layGround` again with the same id **replaces** it and wakes whatever is
  over the old ground or the new. Returns false, changing nothing, for a zero
  id, a spacing that is not positive, too few heights, or fewer than 2×2
  samples (4×4 with a margin).
- `margin: true`: the outer ring of samples is the neighbouring pieces',
  never stood on, so pieces laid side by side are one ground. `at` is then
  the margin's corner.
- Casts stop on ground, and characters walk on it, climb it and slide down
  what is too steep.

A terrain:

```dart
final ground = TerrainPhysics(physics, terrain); // friction, restitution, layers
ground.sync(x: camera.x, z: camera.z, radius: 64); // every frame
ground.sync(x: camera.x, z: camera.z, radius: 64, refresh: false); // mid-stroke
ground.regionOf(hit.body); // RegionKey?, for an event or cast that hit it
ground.clear();            // takes it all up
```

- `sync` lays regions within `radius`, takes up regions a region's width
  beyond it or gone from the terrain, and relays a region whose `revision`,
  or a neighbour's, has moved. It returns how many it laid.
- Holes and missing regions are holes, exactly as `heightAt` says null.
- Bodies are `TerrainPhysics.idOf(key)`, far below zero (from −2^62), clear
  of `ScenePhysics`'s positive ids and of small negative ids for your own
  bodies. Pass `numbering:` to choose others.

## Joints

```dart
physics.join(
  1,                                   // a joint id: counted apart from bodies
  const Joint.hinge(limit: JointLimit(0, 1.6), speed: 1, strength: 50),
  a: 0,                                // the world
  b: door,
  at: [0.5, 1, 0],                     // the joint's point in the world
  rotation: [0, 0, 0.7071, 0.7071],    // its frame, xyzw: x is the hinge axis
  breakingTorque: 400,
);
final state = physics.jointStateOf(1); // JointState?: offset, angles, force, torque
physics.unjoin(1);
```

- `join(id, joint, {required a, b = 0, required at, rotation, to,
  breakingForce, breakingTorque, collide = false})` returns whether it was
  made. Either `a` or `b` may be 0, the world, **not both**. False for a zero
  or taken id, an end that is not a body, or an end joined to itself.
- Kinds: `Joint.fixed()`, `Joint.point()`, `Joint.hinge(limit:, speed:,
  strength:)` (turns about the frame's x), `Joint.slider(limit:, speed:,
  strength:)` (along x), `Joint.distance(limit:)` (keeps `at` on `a` and `to`
  on `b` apart; no limit is a rod, `JointLimit(0, 2)` a rope),
  `Joint.cone(swing:, twist:)` (x swings within `swing`), and
  `Joint.sixAxis(alongX:, ..., aboutZ:)`, each free unless limited.
- **The physics package takes radians**, metres and newtons. `JointLimit(low,
  high)`; `JointLimit.locked()` holds it at nought. A motor with `strength`
  0 is no motor; `speed` 0 with some strength is friction.
- It holds how the bodies stood when it was made, so every limit is measured
  from there. `PhysicsJointKind` is the enum; `Joint` is what you pass.
- Joined free bodies sleep and wake together.

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

## The joint component

`JointComponent` in `orblit_scene` (`SceneComponents.joint`, JSON key
`joint`): `kind` (`JointKind.fixed`, `point`, `hinge`, `slider`, `distance`,
`cone`, `sixAxis`), `limits` (`Map<JointAxis, JointRange>`, keyed `alongX` to
`aboutZ`), `swing`, `speed`, `strength`, `breakingForce`, `breakingTorque`,
`collide`. Change one limit with `limit(axis, JointRange(low, high))` or
`free(axis)`; `JointRange.at(v)` is locked. `axes` says which limits the kind
reads.

- **It names no ids.** The joint holds the nearest body at or above its
  entity to the nearest body above that, or to the world:
  `JointComponent.endsOf(id, parentOf:, hasBody:)` says which. A forearm hangs
  from the arm because it is under it. So put the joint on its own child
  entity where the hinge or elbow is, and turn that entity so its x is the
  axis.
- **Degrees in the document**, as a transform's rotation is; the bridge
  converts. `speed` is degrees or metres a second.
- A chain tied at both ends, or two siblings joined to each other, cannot be
  said this way. Make those with `scene.physics.join` and a negative id.

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
final hinge = scene.physics.jointStateOf(scene.jointOf('hinge')!);
for (final event in scene.events) {
  if (event.kind == PhysicsEventKind.broke) {
    print('${scene.entityOfJoint(event.a)} broke'); // also in scene.broken
    continue;
  }
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
- **A plane cannot be cast.** Asking gives null. Nor can ground; both can be
  cast against.
- **Removing ground wakes nothing.** A body asleep on it stays in the air
  until something wakes it (`wake(id)`), which is what keeps a crate in place
  while its region streams out and back.
- **`sync` with `refresh: false` during a stroke, then once without.** Left
  true, every frame of a brush stroke relays the regions under it. A colour
  or cover paint moves the revision too, so it relays as well.
- **`orblit_terrain` never depends on physics.** Collision is the bridge's
  job; `heightAt` is still how to place a thing on the ground without it.
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
- **A joint's axes are its first body's.** Every measure is `b` as `a` sees
  it, so with the world as `a` a door's angle is the door's, and with the
  world as `b` it reads the other way round. `ScenePhysics` puts the world, or
  the body above, first.
- **Swing and twist mean nothing near a half turn.** Within a quarter turn
  they are the angles they look like. Limit a joint before it bends that far.
- **`remove` drops a body's joints silently.** No `broke` event, because
  nothing broke; what they held is woken and falls. Laying ground again keeps
  joints on it.
- **`broke` names the joint, not a body.** `event.a` is the joint id, which
  may equal a body id. Map it with `scene.entityOfJoint`, not `entityOf`.
- **Radians in `orblit_physics`, degrees in a document.** `JointLimit(0, 90)`
  on a hinge is ninety radians, not a quarter turn. `jointStateOf` answers in
  radians even for a joint the document made.
- **An edit remakes a joint as things stand then.** A door edited half open
  reads nought half open, and its limits move with it.
- **A broken scene joint stays broken** until its own entity is edited.
  Moving the body, or a parent, does not mend it.
- **Joined bodies do not collide with each other** unless `collide` is true.
- **The editor** draws bodies and joints (inspector sections and wireframes)
  but does not simulate them.
