# Physics

Rigid bodies live in their own repository, `https://github.com/ChxisB/orblit-physics.git`,
as three packages under `packages/<name>`. Names below were checked against
`orblit_physics` 0.10.0, `orblit_physics_scene` 0.8.0 and
`orblit_physics_terrain` 0.1.0, and `orblit_scene` 0.13.0 for the components. A git dependency
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
  friction, restitution, linearDamping, angularDamping, layers, asleep,
  trigger, stay})`. `id` is any non-zero int; adding one already there is
  ignored.
- `remove(id)`, `place(id, at:, rotation:)` (teleport, forgets velocity),
  `drive(id, velocity:, spin:)` (how a driven body is moved), `push(id,
  impulse:, at:)` (N·s at a world point; off-centre spins it), `wake(id)`.
- Reading: `transformOf`, `velocityOf` (six floats), `alive`, `asleep`,
  `count`, and `readInto(ids, Float32List out, stride:)` for many at once.
- `events` is `List<PhysicsEvent>` for **the last step only**: `kind`
  (`touchBegan`, `touchEnded`, `slept`, `woke`, `broke`, `entered`, `exited`,
  `touchStay`, `inside`), `a`, `b` (smaller id first), `at`, `normal`, `force`
  (N·s). For `broke`, `a` is the joint's id, `b` is 0, and `force` is what
  broke it. For `entered`, `exited` and `inside`, `a` is the trigger and `b`
  the body, **whatever their ids**.
- `cast(from:, direction:, distance:, shape:, rotation:, layers:, ignore:,
  triggers:)` returns `PhysicsHit?` with `body`, `at`, `normal`, `distance`
  and `started` (the cast began inside the body). No shape casts a point,
  which is a ray.
- `castAll(...same, limit = 32)` is every hit, nearest first, and a full list
  keeps the nearest. `castAny(...same)` is a `bool` and stops at the first
  body it finds. `overlap(at:, shape:, rotation:, layers:, ignore:,
  triggers:, limit = 32)` is body ids in no order, and with no `shape` it is
  the bodies containing the point. Nothing is moved or woken.
- Questions see solid bodies and never a trigger. `triggers: true` sees
  triggers and nothing solid.
- `Layers(is_: bits, cares: bits)`. Two bodies meet when *either* cares about
  the other.
- `PhysicsMotion.fixed` never moves, `driven` goes where it is sent and is
  never pushed back, `free` falls and is pushed.
- `PhysicsSettings` fields left null take the engine's defaults.
- `snapshot()` returns a `PhysicsSnapshot` of what a step reads from the one
  before, and `restore(snapshot)` goes back to it, dropping commands queued
  since. It is native memory, so `dispose()` it. It outlives its world,
  restores into another world, and restores any number of times. The same
  commands and step sizes after a restore give the same bits, on the same
  build only.
- `contacts` is a `List<PhysicsContact>` for the last step: `a`, `b` (smaller
  id first), `at`, `normal` (out of `b` towards `a`), `depth` (m) and `impulse`
  (N·s). A sleeping pair is not listed. `stats` is a `PhysicsStats`: counts of
  bodies by kind, `asleep`, `triggers`, `characters`, `joints`, `zones`,
  `rules`, `pairs`, `touching` and `points`, and `stepMicroseconds`. There is
  no island count.

## Cylinders and hulls

```dart
physics.add(3, shape: const Shape.cylinder(0.4, 0.6), at: [2, 3, 0]);

// A hull is cooked once from points, flat as x, y, z, x, y, z, ...
physics.layHull(-1, points: corners);        // false when it cannot be cooked
physics.add(4, shape: const Shape.hull(-1), at: [4, 3, 0]);
physics.dropHull(-1);                        // false while a body still uses it
```

- `Shape.cylinder(radius, halfHeight)` is flat at both ends and stands along
  the body's own y. It is `2 * halfHeight` tall. A capsule of the same two
  numbers is taller by two radii and has no flat end to stand on.
- `layHull(id, {required points})` keeps the convex solid round the points
  under `id`, which is any non-zero int. It answers false, and lays nothing,
  for an id already used, fewer than four points, more than 100,000, a number
  that is not finite, a length that is not a multiple of three, or points
  that enclose no volume.
- A hull is never changed once laid. To change one, lay another under a new
  id and make the bodies again. A hundred bodies share one hull.
- Past 255 corners the solid is cooked down to the 255 that stand out
  furthest, so it is a little smaller than the points and never larger.
- A body is placed by the origin of the frame its points were given in, and
  weighs and turns about the middle of the solid.
- A body or a cast that names a hull nobody laid makes nothing and meets
  nothing. Only a pair with a cylinder or a hull in it uses the general
  routine. Spheres, boxes and capsules keep theirs.
- On ground, a cylinder or a hull that has sunk into a cliff comes out along
  the nearest face, which is not always the way it came in.

## Triggers, zones, belts and rules

```dart
physics.add(pond, shape: const Shape.box(4, 1, 4), motion: PhysicsMotion.fixed,
    trigger: true, stay: true);
physics.setZone(pond, const PhysicsZone(gravity: [0, -1.5, 0], linearDamping: 4,
    priority: 1));
physics.setSurface(belt, velocity: const [2, 0, 0]);
physics.setRule(lift, crate, const PhysicsRule(moveScaleA: 0));
```

- **A trigger is a place.** Fixed or driven only: the flag is ignored on a free
  body. Nothing collides with it, and characters and solid questions do not
  see it. It reports `entered` and `exited`, and `inside` every step when the
  trigger or the body has `stay`. `exited` also fires when either is removed.
  Triggers do not see triggers or fixed bodies.
- **`stay` on a body also reports `touchStay`** for each solid contact that
  goes on. It stops when the body sleeps, because a sleeping pair has its
  touch ended. `inside` does not stop: a trigger reads positions.
- **A zone changes gravity, `linearDamping` and `angularDamping`** for the
  free bodies inside a trigger. A null field leaves the body's own. Each field
  is decided on its own: highest `priority` wins, then the lower body id.
  Characters are not moved by a zone. `setZone` is false for a body that is
  not a trigger. `removeZone` leaves the trigger. Both wake what is inside.
- **`setSurface(id, velocity:)` is a belt.** World metres a second, held until
  set again, stored whole and projected on each surface it touches, so a
  velocity into the body does nothing. It wakes what rests on it. A body
  being carried never sleeps. It is per body, not per pair.
- **`setRule(a, b, PhysicsRule(...))` changes one pair's contact:**
  `friction` and `restitution` replace the pair's, and `moveScaleA` and
  `moveScaleB` scale how much of the push each body takes (1 as is, 0
  immovable to the other, 2 as if half the mass). It is **data, not a
  callback**. False for a missing body, a body against itself or a negative
  scale. It holds until `removeRule` or until either body goes, and it wakes
  both. A rule that changes nothing asserts.
- **`PhysicsRule(ignore: true)` makes a pair pass through each other.** No
  contact and no touch event. A character does not read rules, so it still
  stops at a body it is told to ignore. It replaces any other rule on the pair.

## Body controls

```dart
physics.setControls(
  crate,
  const PhysicsControls(
    locks: {PhysicsLock.moveZ, PhysicsLock.turnX, PhysicsLock.turnY},
    gravityScale: 0.5,
    maxSpeed: 12,
  ),
);
physics.setMotion(crate, PhysicsMotion.driven); // lifted by a cutscene
physics.setGravity([0, -3.7, 0]);
```

- **`setControls(id, PhysicsControls, {quiet = false})` sets all of it at
  once.** To change one field, send the others again. False, and nothing
  changed, for a missing body, ground, a number that is not finite, or a
  negative cap or inertia. It wakes the body and what rests on it. Pass
  `quiet: true` for a body just added, so one added asleep stays asleep.
- **`locks`** holds any of `PhysicsLock.moveX` to `turnZ`, in the world's axes.
  The solver holds them, so a body locked to a plane still rests on a floor
  tilted across it, and a locked turn holds exactly.
- **`gravityScale`** scales the gravity the body feels, a zone's included. Zero
  floats and a negative number rises.
- **`maxSpeed` and `maxSpin`** cap metres and radians a second. Zero is no cap.
  A cap is applied once a step, after the contacts.
- **`centre`** is where the weight is, in the body's own frame. A free body
  turns about it, and its `velocityOf` is the velocity of that point. The
  transform still reports the origin the body was placed by. `push` with no `at`
  goes through it.
- **`inertia`** is the inertia about each axis through the centre of mass. It is
  used as given, and only when all three parts are above zero. Null, or a zero
  part, uses the shape's, moved to the centre of mass.
- A fixed or driven body keeps its controls for when it is made free.
- **`setMotion(id, PhysicsMotion)`** makes a body fixed, driven or free. Fixed
  stops it, driven keeps its velocity, and free gives it the mass it was made
  with and its controls. Ignored for a trigger, a character, ground, a missing
  body and a body already in that motion.
- **`setGravity([x, y, z])`** wakes every body that can move. False for a number
  that is not finite. Zones and `gravityScale` still apply on top.

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
Its fields are `shape` (`BodyShape.box`, `sphere`, `capsule`, `cylinder`,
`hull`, `compound`, `plane`), `size`, `radius`, `height`, `hull`,
`parts`, `shapeScale`, `centre`, `motion`
(`BodyMotion.fixed`, `driven`,
`free`), `mass`, `friction`, `restitution`, `linearDamping`, `angularDamping`,
`layers`, `cares`, `startsAsleep` (JSON `asleep`), `trigger`, `stay` and
`surface` (a `Vector3`, world metres a second). Its controls are `locks` (a
`Set<BodyLock>`, `moveX` to `turnZ`), `gravityScale` (1), `maxSpeed` and
`maxSpin` (0 is no cap), `centreOfMass` and `inertia` (`Vector3`s, zero for the
shape's own). Change one field with `copyWith`.

- Sizes are in the entity's own units, so the body scales with the entity. A
  sphere takes the largest of the three scales. A capsule or a cylinder takes
  the larger sideways scale for its radius and the upright one for its height.
- `hull` is flat x, y, z numbers in the entity's own units, measured from
  `centre`, and each axis is stretched by that axis of the entity's scale.
  Bodies with the same corners at the same scale share one hull, and the last
  body to go takes it with it.
- **A hull that cannot be cooked is a body that does nothing.** Fewer than
  four points, points in one plane, a length that is not a multiple of three
  or a list with something other than numbers in it all give the entity its
  number but no body in the world, so it never moves. The file keeps it, and
  fixing the points makes it a body at once. A cylinder with a radius or a
  height of nought does the same.
- `centreOfMass` scales with the entity. `inertia` does not.
- A negative cap or inertia in a file is read as none by the bridge, so the
  locks still apply. The world itself refuses one.
- **Materials are presets, not a field.** `BodyMaterial.presets` holds Ice,
  Metal, Wood, Stone, Sandbag and Rubber, each a friction and a restitution.
  `body.madeOf(material)` copies both onto a body. `BodyMaterial.of(body)` gives
  the preset whose two numbers match exactly, or null. Nothing is stored in the
  file, so a body with other numbers is custom.
- **Layer names are a scene setting.** `SceneSettings.layerNames` holds up to 32
  names, and `nameOf(layer)` answers `String?` for one that has none. Changing
  it is one `SetSetting` with the field `SetSetting.layerNames`. The editor
  shows `layers` and `cares` as a grid of toggles under those names.
- `plane` is endless ground facing the entity's up, through `centre`. It
  never moves, whatever `motion` says.

## The zone component

`ZoneComponent` in `orblit_scene` (`SceneComponents.zone`, JSON key `zone`):
`gravity` (`Vector3?`), `linearDamping`, `angularDamping` (`double?`) and
`priority` (`int`, 0). A null field is left out of the file and left to the
body. It goes on an entity that has a body and **makes that body a trigger**,
whatever `trigger` says. `ScenePhysics` calls `setZone` for it. Editing a zone
rebuilds its body, so it is a teleport. `copyWith` cannot clear a field: build
a new `ZoneComponent` to do that.

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
scene.ignore('crate', 'ghost');         // pass through, either order
scene.unignore('ghost', 'crate');

// Smooth display: `ScenePhysics(document, smooth: true)` shows each body
// blended between its last two steps, so every frame moves it.
scene.physics.place(scene.bodyOf('crate')!, at: [0, 5, 0]);
scene.resetSmoothing('crate');          // no streak to the new place
scene.stopSmoothing('player');          // shown as the world has it...
scene.startSmoothing('player');         // ...until this

final hinge = scene.physics.jointStateOf(scene.jointOf('hinge')!);
for (final event in scene.events) {
  if (event.kind == PhysicsEventKind.broke) {
    print('${scene.entityOfJoint(event.a)} broke'); // also in scene.broken
    continue;
  }
  print('${scene.entityOf(event.a)} ${event.kind.name} ${scene.entityOf(event.b)}');
}

// Rollback. The diff is already in scene.document, and is for the view:
final frame = scene.snapshot();
scene.advance(1);
view.apply(scene.restore(frame));
frame.dispose();

scene.dispose();
```

## Traps

- **`Shape.box` takes half sizes; `BodyComponent.size` is edge to edge.**
  `Shape.capsule(radius, halfHeight)` is the straight part either side of the
  middle; `BodyComponent.height` is tip to tip, ends included. A cylinder's
  height is end to end, so `Shape.cylinder(r, h)` is `BodyComponent.height`
  `2 * h`.
- **A hull laid straight into `physics` wants a negative id,** as a body does.
  `ScenePhysics` numbers its own hulls from 1 and never reuses one.
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
- **`smooth: true` shows a body a step behind.** The document has the blended
  pose and `physics` has the truth, so casts, events and contacts are up to a
  sixtieth of a second ahead of what is drawn. It is off by default. A call
  that takes no step still returns a diff, and a body at rest adds nothing to
  it.
- **A move through `physics.place` needs `resetSmoothing(entity)`,** or the
  body is drawn crossing the gap. An edit through `apply` needs nothing.
  `resetSmoothing`, `stopSmoothing` and `startSmoothing` answer false for an
  entity the document does not have. `stopSmoothing` covers the entity and
  everything under it.
- **A snapshot does not cross smoothing modes.** `restore` throws an
  `ArgumentError` when the scene and the snapshot differ in `smooth`, and
  changes nothing.
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
- **Trigger events name the trigger first,** not the smaller id. Keying every
  event on the smaller id misses them.
- **A solid question never sees a trigger.** A ray through a pond hits what is
  under it. Ask with `triggers: true` to find the zone. A trigger cannot be
  found by a solid `overlap` either.
- **`touchStay` stops when a body sleeps, and `inside` does not.** Do not read
  a missing `touchStay` as "left".
- **A zone needs a body on its entity** and is a trigger, so it is fixed or
  driven. On a free body it does nothing. It never moves a character.
- **A zone's null field is the body's own,** not zero. `copyWith` cannot set a
  field back to null.
- **A belt's velocity is in the world,** not the entity's frame, and only the
  part along the touched surface counts.
- **A rule is per pair and data.** There is no per-contact callback, and no
  per-pair surface speed. A belt is per body.
- **One-way platforms are not there.**
- **`ignore` does not stop a character.** A character is swept, not solved, and
  does not read rules. Keep it off a body with a layer instead.
- **`scene.ignore` needs both bodies and takes entity ids.** It answers false
  for a missing body or an entity against itself. The pair is kept when an edit
  rebuilds a body, and forgotten when an entity loses its body or leaves the
  document. It is not in the document, because an id inside a prefab instance is
  a path that would not survive the file being read again.
- **`setRule` wakes both bodies,** so `ignore` wakes a sleeper too.
- **A cap is per step.** A body can pass `maxSpeed` inside a step. It never
  leaves one above it.
- **A lock is in the world's axes,** not the entity's. A body turned a quarter
  about y and locked along x is locked along the world's x.
- **`setControls` wakes the body** unless `quiet` is true. The bridge sends
  controls quietly when it adds a body, so `startsAsleep` still holds.
- **A given `inertia` is used as it is.** It is not moved to the centre of mass
  and not scaled by the entity. One zero part hands all three back to the
  shape's.
- **A trigger cannot change motion.** `setMotion` ignores a trigger, a
  character and ground, with no error.
- **Dispose snapshots.** They are native memory. `restore` with a disposed one
  throws a `StateError` and changes nothing. A snapshot is not a file and does
  not leave the process.
- **A restore gives the view a diff.** `scene.restore` changes
  `scene.document`, so pass its `SceneDiff` to `view.apply`, or the view shows
  the frame before.
- **A replay needs the same step.** `ScenePhysics` rolls its body and joint
  numbers back, so an entity added after the snapshot gets the same number
  again. A snapshot restored into a scene with another `step` does not replay.
- **`contacts` and `stats` are the last step's.** A pair asleep is not in
  `contacts`, and `stepMicroseconds` is not in a snapshot.
- **The editor** draws bodies, triggers, zones and joints (inspector sections
  and wireframes) but does not simulate them. Nothing draws contacts yet.

## Compound and scaled shapes

- `Physics.layCompound(id, parts: List<ShapePart>, scale: [1, 1, 1])`
  returns false for an invalid or already used id. `Shape.compound(id)` names
  it for bodies, casts and overlaps. A single part gives a scaled or offset
  shape. Compounds retain the empty gaps between parts.
- `ShapePart(shape:, at: [0, 0, 0], rotation: [0, 0, 0, 1], scale: [1, 1, 1])`
  scales, turns, then translates a convex primitive or hull. Rotation is xyzw.
  Outer compound scale also stretches part placements and rotated geometry.
- Keep 1 to 64 convex parts. No planes, height fields or nested compounds.
  Scales are finite and positive. Geometry scaling is exact, including curved
  shapes and transformed normals. Each part has uniform density; overlaps
  count their mass twice. The combined inertia includes rotations and offsets.
- `dropCompound(id)` refuses removal while a body uses it. A compound keeps
  its hulls alive. Drop the compound before dropping its hulls. Snapshots
  retain assets independently of the live world.
- `BodyComponent(shape: BodyShape.compound, parts: [...])` uses `BodyPart`.
  Part sizes and heights are full dimensions. A part has `shape`, `size`,
  `radius`, `height`, `hull`, `centre`, `rotation` and `scale`. Centre is local
  to the compound. Its file rotation is a quaternion, its editor turn degrees.
- `BodyComponent.shapeScale` defaults to `Vector3.all(1)`. Setting it applies
  exact geometry scaling before the entity transform. Compounds always scale
  exactly. Ordinary rounded bodies with the default shapeScale retain their
  existing entity sizing rules. Planes ignore shapeScale.
- A body origin is placed by `centre`; its automatic centre of mass comes
  from its parts. `centreOfMass` offsets that combined balance point.
- Scene compounds own their assets by body number and release them on edits
  and removal. Use negative ids for hulls and compounds added straight into
  `scene.physics`, since positive ids belong to the document.
- The editor offers Compound, Add part, Remove part, each part's geometry,
  centre, scale and turn, and Shape scale for the whole collider. Fit to mesh
  replaces the compound with one fitted box. The gizmo draws the same maps.
