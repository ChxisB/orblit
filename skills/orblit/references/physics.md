# Physics

Rigid bodies live in their own repository, `https://github.com/ChxisB/orblit-physics.git`,
as .NET projects under `dotnet/`. Names below were checked against
`Orblit.Physics` 0.17.0, `Orblit.Physics.Scene` 0.13.0,
`Orblit.Physics.Terrain` 0.1.0, `Orblit.Physics.Vehicle` 0.4.0,
`Orblit.Physics.Soft` 0.2.0 and `Orblit.Scene` 0.24.0 for the components. The
examples compile against those versions. None has been drawn in a running
scene.

```sh
dotnet add package Orblit.Physics
dotnet add package Orblit.Physics.Scene
dotnet add package Orblit.Physics.Terrain
dotnet add package Orblit.Physics.Soft
```

The physics repository's `tool/pack_local.sh` packs them into the same feed as
the engine's ([dotnet.md](dotnet.md#setting-up)). Run its `tool/build_native.sh`
first, or `Orblit.Physics` ships without the solver. Ragdolls are
`Orblit.Physics.Rig`, covered in [ragdoll.md](ragdoll.md).

- **`Orblit.Physics`** is the solver: C++ behind `Orblit.Interop`, depending on
  nothing else in Orblit. It is native code, so there is no browser build.
- **`Orblit.Physics.Scene`** holds `ScenePhysics`, which simulates every entity
  in a scene document that has a `body` component and answers each step with a
  `SceneDiff`. It also holds `ICharacterWalk`, `RootMotionWalk` and
  `ScenePhysicsSnapshot`. It does not hand on `Orblit.Physics`, so use both
  namespaces when you need `Shape` or `PhysicsEventKind`.
- **`Orblit.Physics.Terrain`** holds `TerrainPhysics`. It lays an
  `Orblit.Terrain` terrain as ground, the regions near a point, and lays a
  region again when it is edited.
- **`Orblit.Physics.Vehicle`** holds `Vehicle`, `VehicleSpec`, `WheelSpec`,
  `SuspensionSpec`, `TyreSpec`, `EngineSpec`, `DriveSpec`, `VehicleInput` and
  `WheelState`. `Orblit.Physics.Scene` depends on it, so a scene game needs
  only the namespace.
- **`Orblit.Physics.Soft`** holds `SoftBody`, `SoftSpec`, `ISoftSurface`,
  `SoftContact`, `ISoftAir`, `PhysicsSurface`, `PhysicsWind` and `BodyPoint`.
  It depends on `Orblit.Physics` and nothing else in Orblit.

Guide: https://orblitengine.com/docs/guides/physics/

## The world

```csharp
using Orblit.Physics;

using var physics = new Physics(new PhysicsSettings { Gravity = [0, -9.81, 0] });

physics.Add(1, Shape.Plane(0, 1, 0), PhysicsMotion.Fixed);
physics.Add(2, Shape.Sphere(0.5), at: [0, 3, 0], restitution: 0.6);

physics.Step(1.0 / 60);          // one call per tick, the same delta each time
var pose = physics.TransformOf(2); // float[]: x, y, z, then qx, qy, qz, qw
```

- `Add(id, shape, motion = Free, at, rotation (xyzw), mass, friction,
  restitution, linearDamping, angularDamping, layers, asleep, trigger, stay)`.
  `id` is a non-zero `ulong`, and adding one already there is ignored.
  Vectors are `double` lists: `[x, y, z]`.
- `Remove(id)`, `Place(id, at, rotation)` (teleport, forgets velocity),
  `Drive(id, velocity, spin)` (how a driven body is moved), `Push(id, impulse,
  at)` (N·s at a world point; off-centre spins it) and `Wake(id)`.
- Reading: `TransformOf`, `VelocityOf` (six floats), `Alive`, `Asleep`,
  `Count`, and `ReadInto(ids, output, stride, offset)` for many at once.
  `ReadInto` skips ids the world does not have, and throws when the last row
  does not fit in `output`.
- `Events` lists **the last step only**. Each `PhysicsEvent` has `Kind`
  (`TouchBegan`, `TouchEnded`, `Slept`, `Woke`, `Broke`, `Entered`, `Exited`,
  `TouchStay`, `Inside`), `A`, `B` (smaller id first), `At`, `Normal` and
  `Force` (N·s). For `Broke`, `A` is the joint's id, `B` is 0, `At` is where
  it was and `Force` is what broke it, in newtons, or in newton-metres if the
  torque went past its limit. For `Entered`, `Exited` and `Inside`, `A` is the
  trigger and `B` the body, **whatever their ids**.
- `Cast(@from, direction, distance, shape, rotation, layers, ignore,
  triggers)` returns a `PhysicsHit?` with `Body`, `At`, `Normal`, `Distance`
  and `Started` (the cast began inside the body). No shape casts a point, which
  is a ray. The direction need not be a unit vector. Write `@from`, because
  `from` is a C# keyword.
- `CastAll(..., limit = 32)` is every hit, nearest first, and a full list keeps
  the nearest. `CastAny(...)` is a `bool` and stops at the first body it
  finds. `Overlap(at, shape, rotation, layers, ignore, triggers, limit = 32)`
  is body ids in no order, and with no `shape` it is the bodies containing the
  point. Nothing is moved or woken.
- Questions see solid bodies and never a trigger. `triggers: true` sees
  triggers and nothing solid.
- `new Layers(Is: bits, Cares: bits)`. Two bodies meet when *either* cares
  about the other. `Layers.Everything` is the default.
- `PhysicsMotion.Fixed` never moves, `Driven` goes where it is sent and is
  never pushed back, `Free` falls and is pushed.
- `PhysicsSettings` fields left null take the engine's defaults. They are
  `Gravity`, `SubSteps`, `VelocitySteps`, `PositionSteps`, `JointSteps`,
  `Slop`, `ContactHertz`, `BounceThreshold`, `SleepSpeed`, `SleepAfter` and
  `Sleeping`. `JointSteps` is the passes over the joints in each correcting
  pass, so a long chain has time to carry a load from its tip to its root.
  One is enough for a joint with nothing heavier than itself at the end.
  Eight hold a chain of a few links with a body a hundred times their weight
  on it to a few millimetres, and cost nothing in a world with no joints.
  `SubSteps` cuts each step into that many short ones. More of them hold a
  taller stack still and cost a step's time in proportion. `Slop` is how far into a
  surface a body may come to rest, and it settles at a quarter of that. Leave
  it above nought, or a resting stack separates and lands again every frame.
- `Snapshot()` returns a `PhysicsSnapshot` of what a step reads from the one
  before, and `Restore(snapshot)` goes back to it, dropping commands queued
  since. It is native memory, so `Dispose` it. It outlives its world, restores
  into another world, and restores any number of times. The same commands and
  step sizes after a restore give the same results, on the same build only.
- `Contacts` lists `PhysicsContact`s for the last step: `A`, `B` (smaller id
  first), `At`, `Normal` (out of `B` towards `A`), `Depth` (m) and `Impulse`
  (N·s). A sleeping pair is not listed. `Stats` is a `PhysicsStats`: `Bodies`
  (the `StaticBodies`, `KinematicBodies` and `DynamicBodies` add up to it),
  `Asleep`, `Triggers`, `Characters`, `Joints`, `Zones`, `Rules`, `Pairs`,
  `Touching`, `Points` and `StepMicroseconds`. Kinematic means driven bodies and
  characters. `StepMicroseconds` is about the machine, so a snapshot does not
  carry it. There is no island count.
- Draw contacts with `Orblit.Stage.DebugLines`. It is off until `Enabled` is
  set. Call `lines.Draw(scene, () => ...)` where the scene is made each frame,
  and turn each contact into a `DebugLine(From, To, Colour)`. Each line is a
  stretched cube, and one shorter than `width` shows as a point. Off, it never
  calls the function, so `Contacts` is not copied.

```csharp
using Orblit.Filament;
using Orblit.Physics;
using Orblit.Stage;
using Orblit.VectorMath;

var lines = new DebugLines(enabled: true);
var drawn = lines.Draw(renderScene, () => physics.Contacts.Select(contact =>
{
    var at = new Vector3(contact.At[0], contact.At[1], contact.At[2]);
    var along = new Vector3(contact.Normal[0], contact.Normal[1], contact.Normal[2]);
    return new DebugLine(at, at + (along * 0.3), new Vector3(1, 0.3, 0.2));
}));
```

`renderScene` is the `RenderScene` the frame would draw. `drawn` is that scene
with a segment for each contact.

## Cylinders and hulls

```csharp
using Orblit.Physics;

physics.Add(3, Shape.Cylinder(0.4, 0.6), at: [2, 3, 0]);

// A hull is cooked once from points, flat as x, y, z, x, y, z, ...
physics.LayHull(ulong.MaxValue, corners);   // false when it cannot be cooked
physics.Add(4, Shape.Hull(ulong.MaxValue), at: [4, 3, 0]);
physics.DropHull(ulong.MaxValue);           // false while a body still uses it
```

- `Shape.Cylinder(radius, halfHeight)` is flat at both ends and stands along
  the body's own y. It is `2 * halfHeight` tall. A capsule of the same two
  numbers is taller by two radii and has no flat end to stand on.
- `LayHull(id, points)` keeps the convex solid round the points under `id`,
  which is any non-zero `ulong`. It answers false, and lays nothing, for an id
  already used, fewer than four points, more than 100,000, a number that is not
  finite, a length that is not a multiple of three, or points that enclose no
  volume.
- A hull is never changed once laid. To change one, lay another under a new id
  and make the bodies again. A hundred bodies share one hull.
- Past 255 corners the solid is cooked down to the 255 that stand out
  furthest, so it is a little smaller than the points and never larger.
- A body is placed by the origin of the frame its points were given in, and
  weighs and turns about the middle of the solid.
- A body or a cast that names a hull nobody laid makes nothing and meets
  nothing.

## Triggers, zones, belts and rules

```csharp
using Orblit.Physics;

physics.Add(pond, Shape.Box(4, 1, 4), PhysicsMotion.Fixed, trigger: true, stay: true);
physics.SetZone(pond, new PhysicsZone(Gravity: [0, -1.5, 0], LinearDamping: 4, Priority: 1));
physics.SetSurface(belt, [2, 0, 0]);
physics.SetRule(lift, crate, new PhysicsRule(moveScaleA: 0));
```

- **A trigger is a place.** Fixed or driven only: the flag is ignored on a free
  body. Nothing collides with it, and characters and solid questions do not see
  it. It reports `Entered` and `Exited`, and `Inside` every step when the
  trigger or the body has `stay`. `Exited` also fires when either is removed.
  Triggers do not see triggers or fixed bodies.
- **`stay` on a body also reports `TouchStay`** for each solid contact that
  goes on. Either body of the pair asking is enough. It stops while the pair
  sleeps, and the touch is not ended: it is held with the pair and put back
  when they wake. `Inside` does not stop, because a body asleep in a trigger
  is still in it. Layers and `ignore` rules apply to triggers too.
- **A zone changes gravity, `LinearDamping` and `AngularDamping`** for the free
  bodies inside a trigger. A null field leaves the body's own. Each field is
  decided on its own: the highest `Priority` wins, then the lower body id.
  Characters are not moved by a zone. `SetZone` is false for a body that is not
  a trigger, or for a number that is not a number, or a fluid density or drag
  that is negative or infinite. `RemoveZone` leaves the trigger. Both wake what
  is inside.
- **A zone with a `FluidDensity` is water.** The density is in kilograms per
  cubic metre, a thousand for water, and the surface is the top of the
  trigger's bounds, level. A sphere, box or capsule in it is lifted by the
  weight of the fluid it displaces, so it floats when its own density is lower.
  `FluidDrag` is the fraction of a body's speed the fluid sheds each second
  when the body is wholly under, and less as less of it is. A pool that only
  slows things needs no density, just a weak gravity and a lot of damping.
- **`SetSurface(id, velocity)` is a belt.** World metres a second, held until
  set again, stored whole and projected on each surface it touches, so a
  velocity into the body does nothing. It wakes what rests on it. It is per
  body, not per pair.
- **`SetRule(a, b, new PhysicsRule(...))` changes one pair's contact.**
  `friction` and `restitution` replace the pair's, and `moveScaleA` and
  `moveScaleB` scale how much of the push each body takes (1 as is, 0 immovable
  to the other, 2 as if half the mass). It is **data, not a callback**. False
  for a missing body, a body against itself, a number that is not a number or
  a negative scale. It holds until `RemoveRule` or until either body goes, and
  it wakes both. The order matters only to the move scales. A rule that
  changes nothing throws `ArgumentException`.
- **`new PhysicsRule(ignore: true)` makes a pair pass through each other.** No
  contact and no touch event, and a trigger does not see the other body. A
  character walks through a body it is told to ignore. The rule's other fields
  are not read.
- **`new PhysicsRule(oneWay: [x, y, z])` makes a one-way platform.** The first
  body named is the platform and the second is what passes through it, going
  up along that direction in the world's axes. The platform holds the second
  body only while it is on that side and not rising through it, so a body
  thrown up from below passes, and the same body coming down, or standing on
  it, does not. The rule is for the two bodies named, so a platform that lets
  many through has one rule for each. A character named second jumps up through
  it and lands on it, and reads only this and `Ignore`.

## Body controls

```csharp
using Orblit.Physics;

physics.SetControls(
    crate,
    new PhysicsControls(
        Locks: PhysicsLock.MoveZ | PhysicsLock.TurnX | PhysicsLock.TurnY,
        GravityScale: 0.5,
        MaxSpeed: 12));
physics.SetMotion(crate, PhysicsMotion.Driven); // lifted by a cutscene
physics.SetGravity([0, -3.7, 0]);
```

- **`SetControls(id, controls, quiet = false)` sets all of it at once.** To
  change one field, send the others again. False, and nothing changed, for a
  missing body, ground, a number that is not finite, or a negative cap or
  inertia. It wakes the body and what rests on it. Pass `quiet: true` for a
  body just added, so one added asleep stays asleep.
- **`Locks`** holds any of `PhysicsLock.MoveX` to `TurnZ`, in the world's axes.
  The solver holds them, so a body locked to a plane still rests on a floor
  tilted across it, and a locked turn holds exactly.
- **`GravityScale`** scales the gravity the body feels, a zone's included. Zero
  floats and a negative number rises.
- **`MaxSpeed` and `MaxSpin`** cap metres and radians a second. Zero is no cap.
  A cap is applied once a step, after the contacts.
- **`Centre`** is where the weight is, in the body's own frame. A free body
  turns about it, and its `VelocityOf` is the velocity of that point. The
  transform still reports the origin the body was placed by. `Push` with no
  `at` goes through it.
- **`Inertia`** is the inertia about each axis through the centre of mass, in
  kilogram square metres. It is used as given, and only when all three parts
  are above zero. Null, or a zero part, uses the shape's, moved to the centre
  of mass.
- **`Continuous`** stops a free body at the first fixed body or ground its move
  would take it through, instead of finding it on the far side. Speculative
  contacts already hold anything fast, so this is for a small body at a walking
  pace against a thin wall, and for a body knocked into one in the same step.
  It costs one cast a step while the body moves. Two free bodies are not cast,
  and neither is a driven body or a character.
- A fixed or driven body keeps its controls for when it is made free.
- **`SetMotion(id, motion)`** makes a body fixed, driven or free. Fixed stops
  it, driven keeps its velocity, and free gives it the mass it was made with
  and its controls. Ignored for a trigger, a character, ground, a missing body
  and a body already in that motion.
- **`SetGravity([x, y, z])`** wakes every body that can move. False for a
  number that is not finite. Zones and `GravityScale` still apply on top.

## Characters

Anything that walks, whether a player or anyone else, is a character, not a
free body. It is swept through the world rather than solved. It slides along
walls, climbs steps, stands on slopes and slides down steeper ones, and rides
whatever it stands on.

```csharp
using Orblit.Physics;

const ulong player = 7;
physics.AddCharacter(player, at: [0, 0.91, 0]); // centre: 0.9 above the feet, plus the skin

// Every step, before physics.Step:
var footing = physics.FootingOf(player)!; // null only for what is not a character
var up = footing.Grounded && jumpPressed ? 5.0 : footing.Velocity[1] - (9.81 / 60);
physics.Drive(player, [walkX, up, walkZ]);
physics.Step(1.0 / 60);
```

- `AddCharacter(id, shape = Shape.Capsule(0.3, 0.6), at, rotation, stepHeight
  = 0.3, steepest = π / 4, skin = 0.01, strength = 500, friction = 0.5,
  layers)`. `steepest` is in radians. `strength` is the most force, in newtons,
  it pushes a free body with. It keeps `skin` metres from everything.
- `Drive(id, velocity)` is a **request**, in metres a second, relative to what
  the character stands on, **with gravity in it**. The world decides how much of
  it the character gets. `spin` is ignored: which way it faces is the game's to
  keep.
- `FootingOf(id)` and `FootingsOf(ids)` give `PhysicsFooting?`, which is null
  for anything that is not a character. Its fields:
  - `Ground`: the body underneath, or 0 for none.
  - `Normal`: which way that surface faces.
  - `Velocity`: what survived of the request, relative to the ground.
  - `Carried`: how fast the ground moved it.
  - `Turning`: how fast the ground turned it about up, in radians a second.
  - `Grounded`.
  - `Wall`, `Ceiling`: what stopped the walk last step, beside the ground. A
    ceiling is something over its head. Both are false unless what it asked for
    met something, so it has to be asking to go into the wall.

  Play the walk animation from `Velocity`, not from `Velocity` plus `Carried`,
  so a character on a lift walks nowhere.
- It pushes the free bodies it walks into, along the ground and through their
  centres, so a crate slides rather than tips. A free body never pushes it back,
  because the solver treats a character as a wall. Only fixed and driven bodies
  push it, as a door does that closes on it or a lift that rises into it. A
  character walked into by another gives way at half the closing speed. Two
  characters placed inside each other walk apart.
- `SetStance(id, shape)` is a crouch or a stand. It takes a sphere, box,
  capsule or cylinder, keeps the feet where they were, and answers false,
  changing nothing, when the new shape does not fit. A character under a beam
  stays crouched. False too for a hull and for what is not a character.
- `Place` teleports it. It lets go of what it stood on and stops asking to
  move, so call `Drive` again.

**Root motion drives a character.** A clip's step is where the animation wants
to go, and the character decides where it actually gets. Advance the clip by
the same tick as the world. Turn the step into world space, divide it by the
tick, and ask for that:

```csharp
using Orblit.Physics;

var step = clipPlayer.Advance(tick);
// Through the matrix: turning by the quaternion itself would turn by the inverse.
var walk = heading.AsRotationMatrix().Transformed(step.Moved.Position);
walk.Scale(1 / tick);
heading *= step.Moved.Rotation;
heading.Normalize();
physics.Drive(player, [walk.X, up, walk.Z]);
physics.Step(tick);
```

`clipPlayer` is an `Orblit.Motion.ClipPlayer` and `heading` is an
`Orblit.VectorMath.Quaternion`. `up` is the vertical request worked out in the
first fence. Do not add the step to a position as well. The
character's transform is the position now. In a scene, `RootMotionWalk` does
this for you (see [A document, simulated](#a-document-simulated)).

## Ground

```csharp
using Orblit.Physics;

physics.LayGround(
    hill,                       // any non-zero id, like any body
    heights,                    // columns * rows, row after row; NaN is a hole
    columns: 65,
    rows: 65,
    spacing: 0.5,
    at: [-16, 0, -16]);         // where sample (0, 0) is
```

- Sample (c, r) is `heights[r * columns + c]` at `at + (c * spacing, h, r *
  spacing)`. Each square is two triangles split from (c, r) to (c + 1, r + 1),
  the same split the terrain mesh uses.
- A height that is not a finite number is a hole, and every triangle with that
  sample as a corner is missing. Things fall through a hole and roll off its
  rim.
- Everything under the surface is solid: a sunk body is pushed up and out,
  never down through, even when the ground is raised under a sleeper.
- `LayGround` again with the same id **replaces** it, keeps its joints, and
  wakes whatever is over the old ground or the new. It returns false, changing
  nothing, for a zero id, a spacing that is not positive, too few heights, or
  fewer than 2×2 samples (4×4 with a margin).
- `margin: true`: the outer ring of samples is the neighbouring pieces', never
  stood on, so pieces laid side by side are one ground. `at` is then the
  margin's corner.
- Casts stop on ground, and characters walk on it, climb it and slide down what
  is too steep.

A terrain, where `terrain` is an `Orblit.Terrain.Terrain`:

```csharp
using Orblit.Physics.Terrain;

var ground = new TerrainPhysics(physics, terrain); // friction, restitution, layers
ground.Sync(camera.X, camera.Z, 64);                  // every frame
ground.Sync(camera.X, camera.Z, 64, refresh: false);  // mid-stroke
ground.RegionOf(hit.Body);  // RegionKey?, for an event or cast that hit it
ground.Clear();             // takes it all up
```

- `Sync` lays regions within `radius`, takes up regions a region's width beyond
  it or gone from the terrain, and relays a region whose revision, or a
  neighbour's, has moved. It returns how many it laid.
- Holes and missing regions are holes, exactly as the terrain's `HeightAt`
  says null.
- Bodies are `TerrainPhysics.IdOf(key)`, a quarter of the `ulong` range below
  the top, clear of the small ids `ScenePhysics` hands out. Pass `numbering` to
  choose others. `Laid`, `BodyOf(key)` and `RegionOf(body)` say what is down.

## Joints

```csharp
using Orblit.Physics;

physics.Join(
    1,                                   // a joint id: counted apart from bodies
    Joint.Hinge(limit: new JointLimit(0, 1.6), speed: 1, strength: 50),
    a: 0,                                // the world
    at: [0.5, 1, 0],                     // the joint's point in the world
    b: door,
    rotation: [0, 0, 0.7071, 0.7071],    // its frame, xyzw: x is the hinge axis
    breakingTorque: 400);
var state = physics.JointStateOf(1);     // JointState?: Offset, Angles, Force, Torque
physics.Unjoin(1);
```

- `Join(id, joint, a, at, b = 0, rotation, to, axisB, over, path,
  breakingForce, breakingTorque, collide = false)` returns whether it was
  made, and wakes both bodies. Either `a` or `b` may be 0, the world, **not
  both**. False for a zero or taken id, an end that is not a body, an end
  joined to itself, or a number that is not finite. Joint ids are counted apart
  from body ids, so a joint may share a number with a body.
- Kinds: `Joint.Fixed()`, `Joint.Point()`, `Joint.Hinge(limit, speed,
  strength)` (turns about the frame's x), `Joint.Slider(limit, speed,
  strength)` (along x), `Joint.Distance(limit)` (keeps `at` on `a` and `to` on
  `b` apart; no limit is a rod, `new JointLimit(0, 2)` a rope),
  `Joint.Cone(swing, twist)` (x swings within `swing`), and
  `Joint.SixAxis(alongX, ..., aboutZ)`, each free unless limited.
- Couplings and a path: `Joint.Gear(ratio)` (the second turns `ratio` times as
  far as the first, about its own x or `axisB`; a ratio is negative for meshed
  gears, which turn opposite ways), `Joint.Rack(ratio)` (the first slides
  `ratio` metres along x for each radian the second turns), `Joint.Pulley(ratio)`
  (a rope over two fixed points `over`, with `at` on the first body and `to` on
  the second: the first length plus `ratio` times the second stays as it was
  made) and `Joint.Path(limit, speed, strength, goal, follow)` (the second body
  slides on the bent line `path`, in world points when made).
- **Give.** Every kind takes `springs: new JointSprings(AlongX: new
  JointSpring(hertz, ratio: 1, rest))`. Every kind with a limit also takes
  `softLimit: new JointSoftness(hertz, ratio)`, which makes the ends of the
  limit springs and not walls. A spring replaces a lock on its way, and on a
  free way is the only thing holding it. `goal: new JointGoal(at, hertz,
  ratio)` on a hinge, slider or path makes the motor hold a place, never harder
  than `strength`, and `speed` is then unread.
- **`Aim(id, rest, goal)` moves where a joint's springs and goal hold** without
  making it again. `rest` is six nullable places in the order of
  `JointSprings.All`: metres along x, y, z, then radians about them. Any other
  count throws `ArgumentException`. A null place leaves that way where it was,
  and a null list leaves all six. `goal` moves the goal, and a joint with no
  goal ignores it. Both bodies wake only when a place moved, so aiming at where
  a joint already holds lets what hangs from it sleep. False for no such joint
  or an infinite value. `Unjoin(id)` wakes both bodies, so what the joint held
  up falls.
- **The physics package takes radians**, metres and newtons. `new
  JointLimit(low, high)`; `JointLimit.Locked()` holds it at nought. A motor
  with `strength` 0 is no motor, and `speed` 0 with some strength is friction.
- It holds how the bodies stood when it was made, so every limit is measured
  from there.
- **Hertz is the way's own frequency under the load it moves.** It is the
  undamped cycles a second of the body on the end of the spring. A turn is
  measured against how hard the body is to swing about the joint. A box hung on
  a slider spring settles its rest length plus gravity over omega squared below
  the hook. A `ratio` of 0 rings for ever, 1 settles as fast as it can without
  overshooting, and above 1 is sluggish. A spring stiffer than a step can
  follow is as stiff as one can.
- **A gear counts its turns,** so the ratio holds after the wheels have gone
  round a hundred times. An angle read back from a pose cannot count turns.
- **A pulley is a rod.** It pushes as well as pulls. `Join` throws
  `ArgumentException` for a pulley without exactly two `over` points, and for a
  point that is not x, y and z.
- **A path keeps counting past its ends,** on the line gone on straight. A
  limit at the line's length stops what runs off. Without one it runs off.
- **A path is corners.** The joint is as sharp at them as the points are, so a
  curve wants a point every few degrees. A point that touches the one before it
  is dropped. To close a loop, end where it began. The second body is pulled to
  the nearest place on the path, so it should begin on it. `Join` answers false
  for fewer than two points once touching ones are merged, and for more than
  10,000.
- **A path is carried with the first body.**
- **Joined bodies sleep together and wake together.** A chain does not go to
  sleep one link at a time, and touching its end wakes all of it.

## The body component

`BodyComponent` in `Orblit.Scene` (`SceneComponents.Body`, JSON key `body`).
Its constructor takes every field by name, all optional: `shape`
(`BodyShape.Box`, `Sphere`, `Capsule`, `Cylinder`, `Hull`, `Compound`, `Mesh`,
`Plane`), `size`, `radius`, `height`, `hull`, `parts`, `meshVertices`,
`meshIndices`, `shapeScale`, `centre`, `motion` (`BodyMotion.Fixed`, `Driven`,
`Free`, `Character`), `mass`, `friction`, `restitution`, `linearDamping`,
`angularDamping`, `layers`, `cares`, `startsAsleep` (JSON `asleep`), `trigger`,
`stay` and `surface` (a `Vector3`, world metres a second). Its controls are
`locks` (a set of `BodyLock`, `MoveX` to `TurnZ`), `gravityScale` (1),
`maxSpeed` and `maxSpin` (0 is no cap), `continuous` (false), `centreOfMass`
and `inertia` (`Vector3`s, zero for the shape's own). Its character fields are
`stepHeight`, `steepest`, `skin`, `strength` and `crouchHeight`.

- Sizes are in the entity's own units, so the body scales with the entity. A
  sphere takes the largest of the three scales. A capsule or a cylinder takes
  the larger sideways scale for its radius and the upright one for its height.
- `hull` is flat x, y, z numbers in the entity's own units, measured from
  `centre`, and each axis is stretched by that axis of the entity's scale.
  Bodies with the same corners at the same scale share one hull, and the last
  body to go takes it with it.
- **A hull that cannot be cooked is a body that does nothing.** Fewer than four
  points, points in one plane, or a length that is not a multiple of three all
  give the entity its number but no body in the world, so it never moves. The
  file keeps it, and fixing the points makes it a body at once. A cylinder with
  a radius or a height of nought does the same.
- `centreOfMass` scales with the entity. `inertia` does not.
- A negative cap or inertia in a file is read as none, so the locks still
  apply. The world itself refuses one.
- **Materials are presets, not a field.** `BodyMaterial.Presets` holds Ice,
  Metal, Wood, Stone, Sandbag and Rubber, each a friction and a restitution.
  `body.MadeOf(material)` copies both onto a body. `BodyMaterial.Of(body)`
  gives the preset whose two numbers match exactly, or null. Nothing is stored
  in the file, so a body with other numbers is custom.
- **Layer names are a scene setting.** `SceneSettings.LayerNames` holds the
  names, and `NameOf(layer)` answers `string?` for one that has none. Changing
  it is one `SetSetting` with the field `SetSetting.LayerNames`. The editor
  shows `layers` and `cares` as a grid of toggles under those names.
- `Plane` is endless ground facing the entity's up, through `centre`. It never
  moves, whatever `motion` says.

## The zone component

`ZoneComponent` in `Orblit.Scene` (`SceneComponents.Zone`, JSON key `zone`):
`gravity` (`Vector3?`), `linearDamping`, `angularDamping` (`double?`) and
`priority` (`long`, 0). A null field is left out of the file and left to the
body. It goes on an entity that has a body and **makes that body a trigger**,
whatever `trigger` says. `ScenePhysics` calls `SetZone` for it. Editing a zone
rebuilds its body, so it is a teleport. The component has no fluid fields, so
a lake is made with `scene.Physics.SetZone` on the body's number.

## The joint component

`JointComponent` in `Orblit.Scene` (`SceneComponents.Joint`, JSON key
`joint`): `kind` (`JointKind.Fixed`, `Point`, `Hinge`, `Slider`, `Distance`,
`Cone`, `SixAxis`, `Gear`, `Rack`, `Pulley`, `Path`), `limits` (a map from
`JointAxis`, `AlongX` to `AboutZ`, to `JointRange`), `swing`, `speed`,
`strength`, `breakingForce`, `breakingTorque`, `collide`. Change one limit with
`Limit(axis, new JointRange(low, high))` or `Free(axis)`; `JointRange.At(v)` is
locked. `Axes` says which limits the kind reads.

Give and machines: `springs` (a map from `JointAxis` to `JointGive(Hertz,
Ratio, Rest)`, set with `Spring(axis, give)` and `Unspring(axis)`; JSON
`{"alongX": [hertz, ratio, rest]}` with the last two optional), `softHertz` and
`softRatio`, `goal`, `goalHertz` and `goalRatio` (a motor holding a place when
`goalHertz` is above 0), `ratio`, `axisB`, `over` (a pulley's two points),
`path` (points, flat in JSON, three numbers each) and `follow`. All points and
`axisB` are in the joint entity's own frame, and `rest` and `goal` are in
degrees or metres.

- **It names no ids.** The joint holds the nearest body at or above its entity
  to the nearest body above that, or to the world:
  `JointComponent.EndsOf(...)` says which. A forearm hangs from the arm because
  it is under it. So put the joint on its own child entity where the hinge or
  elbow is, and turn that entity so its x is the axis.
- **Degrees in the document**, as a transform's rotation is; the bridge
  converts. `speed` is degrees or metres a second.
- A chain tied at both ends, or two siblings joined to each other, cannot be
  said this way. Make those with `scene.Physics.Join` and an id from the top of
  the `ulong` range. A gear, rack or pulley ties the body to the body it hangs
  under, so a second wheel is a child of the first.

## The vehicle and wheel components

`VehicleComponent` and `WheelComponent` in `Orblit.Scene`
(`SceneComponents.Vehicle` and `SceneComponents.Wheel`, JSON keys `vehicle` and
`wheel`). The vehicle goes on the chassis entity, which has a free body. Each
wheel is a **direct child** with a wheel component.

`VehicleComponent`: `maxSteer` (degrees, 30), `antiRoll` (0), `torque` (flat
`[rpm, Nm, ...]`), `idle`, `ratios`, `reverse`, `finalDrive`, `lock` (0 is an
open differential) and `ground` (the layers the wheels cast against).
`WheelComponent`: `radius`, `steers`, `drives`, `brake`, `handbrake`, `travel`,
`hertz`, `damping` and `grip`. A car with no `drives` wheel has no engine and
no gearbox. In C# the constructor parameter for `lock` is `@lock`.

- **Place a wheel where it rests.** The entity's position is the wheel's centre
  with the car standing still. The bridge works out where the spring hangs from
  using the sag (`g / (2 pi hertz)^2`), which does not depend on the car's mass.
  It then writes the position, the roll (about the wheel's own x axle) and the
  steer (about the chassis' up) back to the entity every step. The wheel keeps
  its own scale.
- **Front is minus z**, plus x is right, positive steer is left.
- **No car, no error.** A body that is scaled, `Driven`, `Fixed` or has no
  wheel child, or a spec the vehicle refuses (a gear ratio whose downshift
  lands above the upshift), keeps its data and is just a body.
  `scene.VehicleOf(id)` is null then.
- **Drive it with `scene.Pilot(id, new VehicleInput(steer, throttle, brake,
  handbrake, gear))`**, any time, and `scene.Pilot(id, null)` to let go. The
  scene has no input actions of its own: read your keys and call `Pilot`. Gear
  is null for automatic, `-1` for reverse. `VehicleInput` throws
  `ArgumentOutOfRangeException` for a value out of range, and a gear the car
  lacks is refused by `Pilot` and by `Vehicle.SetInput`.
- **The step is at most `Vehicle.MaxStep`, `1/30`.** `Vehicle.Update` throws
  past it, so a scene with a car needs `ScenePhysics(step: ...)` of 1/30 or
  less.
- **A snapshot does not hold the tyres.** After `Restore` the bristles are
  straight and the driver is the one you gave last. A car made again by an
  edit does the same and keeps its driver, unless the edit removed the gear
  the driver held.
- **`antiRoll` wants two wheels to an axle**, at the same z to 0.1 mm.
- **With `smooth` the chassis shows a step behind** and the wheels are
  current.
- `Vehicle` reads `Gear`, `Rpm` and `Wheels`, a list of `WheelState` with
  `Grounded`, `Compression`, `Force`, `Spin`, `Steer` and `Roll`.

## A document, simulated

A scene document is a tree of entities. A body is a component on one.

```csharp
using Orblit.Scene;
using Orblit.VectorMath;

static SceneEntity Thing(string id, BodyComponent body, Vector3? at = null) =>
    new(id, id, components: new Dictionary<string, SceneComponent>
    {
        [SceneComponents.Transform] = new TransformComponent(at),
        [SceneComponents.Body] = body,
    });

var yard = new SceneDocument("Yard", entities:
[
    Thing("floor", new BodyComponent(BodyShape.Plane, motion: BodyMotion.Fixed)),
    Thing("crate", new BodyComponent(), new Vector3(0, 3, 0)),
]);
```

Then simulate it beside the view that draws it:

```csharp
using Orblit.Physics;
using Orblit.Physics.Scene;
using Orblit.Scene;
using Orblit.Stage;

using var scene = new ScenePhysics(document);   // step: 1 / 60, maxSteps: 8
var view = new DocumentView(document);

// Every frame, from the frame's delta in seconds:
var moved = scene.Advance(seconds);
if (!moved.IsEmpty)
{
    view.Apply(moved);
}

// Every edit goes to both:
var diff = SceneDiff.Between(scene.Document, next);
scene.Apply(diff);
view.Apply(diff);

// By entity id:
scene.Physics.Push(scene.BodyOf("crate")!.Value, [40, 0, 0]);
scene.Ignore("crate", "ghost");           // pass through, either order
scene.Unignore("ghost", "crate");

// Smooth display: new ScenePhysics(document, smooth: true) shows each body
// blended between its last two steps, so every frame moves it.
scene.Physics.Place(scene.BodyOf("crate")!.Value, [0, 5, 0]);
scene.ResetSmoothing("crate");            // no streak to the new place
scene.StopSmoothing("player");            // shown as the world has it...
scene.StartSmoothing("player");           // ...until this

var hinge = scene.Physics.JointStateOf(scene.JointOf("hinge")!.Value);
foreach (var physicsEvent in scene.Events)
{
    if (physicsEvent.Kind == PhysicsEventKind.Broke)
    {
        Console.WriteLine($"{scene.EntityOfJoint(physicsEvent.A)} broke"); // also in scene.Broken
        continue;
    }

    Console.WriteLine($"{scene.EntityOf(physicsEvent.A)} {physicsEvent.Kind} {scene.EntityOf(physicsEvent.B)}");
}

// Rollback. The diff is already in scene.Document, and is for the view:
using var frame = scene.Snapshot();
scene.Advance(1);
view.Apply(scene.Restore(frame));
```

`document`, `next`, `seconds` and `view` are yours. `next` is the edited
document.

A character in a scene walks by `scene.Walk(entity, walk)`, where `walk` is an
`ICharacterWalk`:

```csharp
using Orblit.Motion;
using Orblit.Physics.Scene;

scene.Walk("player", new RootMotionWalk(new ClipPlayer(walkClip)));
```

`ICharacterWalk.Ask(seconds, footing)` answers the velocity to request for one
step. `advance` calls it once for every step it takes, so a frame that owes
three steps asks three times and nothing needs its own clock. `RootMotionWalk`
is the clip's root motion with gravity in it. Its `Heading` is which way the
character faces, for the game to write to the entity, since the world keeps no
facing. `walkClip` is a `ClipDocument`.

## Cloth

`SoftBody` is particles joined by distance links, stepped beside the world and
not inside it. There is no scene component for one. `Cloth` is a grid with
links along rows and columns, both diagonals so it resists shear, and two
apart so it resists folding. `Rope` is a chain. `Balloon` is a closed mesh that
also holds the volume it began with, so it sags under a load without going
flat.

```csharp
using Orblit.Physics.Soft;
using Orblit.VectorMath;

var flag = SoftBody.Cloth(
    columns: 20,
    rows: 12,
    origin: new Vector3(0, 3.4, 0),
    across: new Vector3(0.08, 0, 0),  // to the next particle along a row
    down: new Vector3(0, -0.08, 0),   // to the next row
    surface: new PhysicsSurface(physics),                                  // optional
    air: new PhysicsWind(physics, new Dictionary<ulong, Vector3> { [zone] = new(0, 0, 8) }), // optional
    spec: new SoftSpec(windDrag: 3));
for (var row = 0; row < 12; row++)
{
    flag.Pin(row * 20);
}

physics.Step(dt);   // the world first
flag.Step(dt);      // then the cloth
var at = flag.PositionOf((2 * 20) + 5);  // particle row * columns + column
```

- Particle `row * columns + column`. `SoftBody.Rope(points)` is a chain, and
  `SoftBody.Balloon(points, triangles, volumeCompliance: ...)` takes triangles
  wound so the normals point out. It throws for a surface with a hole in it or
  one wound inside out.
- `Pin`, `Release` and `Place` hold a particle. `Place` moves a pinned one.
- `Attach(i, BodyPoint.Of(physics, id, local), limit: newtons)` holds a
  particle to a point on a body. Over `limit`, averaged over a step, it lets
  go. `PullOf(i)` reads the pull and `IsAttached(i)` tells whether it holds. The
  pull is one way: the cloth never pushes the body. The place can be any
  `Func<Vector3>`, such as a bone.
- `PhysicsSurface` casts a sphere through the world. A floor must be a box,
  since a world cannot cast a plane. Triggers are never met. Pass `ignore` for
  a body the cloth passes through, such as the one it hangs from.
- `PhysicsWind(physics, zones)` blows from trigger bodies, and overlapping
  zones add. A trigger not in the map blows nothing. Any `ISoftAir` works. It is
  asked once a step, at the middle of the cloth. `SoftSpec.WindDrag` is how
  fast the air takes a particle up to its own speed, and a flag at 1.0 in a
  10 m/s wind is pushed at about the pull of gravity.
- `Step` is cut to `SoftSpec.MaxStep` (0.1 s) and runs in sub-steps of at most
  `SoftSpec.SubstepLength` (1/480 by default, 1/120 at the longest). A body
  that has been still for a second stops stepping. `Wake()` it when a surface
  moves under it. A pin, `Place` or a new wind wakes it already.
- A body hung from few links stretches more than one hung from many. Shorten
  the sub-step when it hangs in rags.
- `BodyPoint.Of` throws once its body is gone. `Detach` first.
- The particles do not meet one another, there is one contact plane per
  particle per step, and a surface thinner than the gap between two particles
  lets one through.

Draw it with `Orblit.Stage.ParticleSheet`:

```csharp
using Orblit.Filament;
using Orblit.Stage;
using Orblit.VectorMath;

var sheet = new ParticleSheet(columns: 20, rows: 12, spacing: 0.08);
RenderResources.Provide("flag/v1/flag.glb", sheet.ToGlb());
// Every frame:
var cloth = new RenderObject
{
    Key = 3,
    Mesh = "flag/v1/flag.glb",
    Colour = new Vector3(1, 1, 1),
    Transform = Matrix4.TranslationValues(corner.X, corner.Y, corner.Z),
    Joints = sheet.Poses(i => flag.PositionOf(i) - corner),
};
```

- A vertex and a joint per particle, so at most 256 (`ParticleSheet.MaxParticles`).
  Numbered as `SoftBody.Cloth` numbers them.
- `Poses` takes positions relative to the object's transform. Pass world
  positions with a transform that is not the identity and the cloth is drawn
  displaced by that transform.
- Provide the bytes once, under a name that changes if they do. `Provide`
  copies the bytes and returns, so call it before the first frame that names
  the mesh. A name asked for before it was provided is looked for again once it
  has been, and the object draws without it until then.
- The sheet is made at the origin: columns along x, rows down minus y and the
  face toward plus z.

## Traps

- **`Shape.Box` takes half sizes; `BodyComponent.size` is edge to edge.**
  `Shape.Capsule(radius, halfHeight)` is the straight part either side of the
  middle; `BodyComponent.height` is tip to tip, ends included. A cylinder's
  height is end to end, so `Shape.Cylinder(r, h)` is `BodyComponent.height`
  `2 * h`.
- **Ids are `ulong`.** A hull, mesh or body laid straight into `physics` wants
  an id from the top of the range, such as `ulong.MaxValue`, because
  `ScenePhysics` numbers its own from 1 and never reuses one.
- **Rotations differ.** The world takes and returns quaternions as x, y, z, w.
  A scene transform's rotation is degrees, applied Z, then Y, then X.
- **Read `scene.Events`, not `scene.Physics.Events`.** One `Advance` can take
  several steps or none. The world keeps only its last step's events, so its
  list loses collisions on a slow frame and repeats them on a fast one.
- **Never pass `Advance`'s diff to `scene.Apply`.** It is already in
  `scene.Document`. Applying it again rebuilds every body that moved and stops
  it dead.
- **Edits are teleports.** `Apply` rebuilds each body the diff touched, and
  every body under it, at rest. That includes moving, rescaling and changing
  the body.
- **`smooth: true` shows a body a step behind.** The document has the blended
  pose and `Physics` has the truth, so casts, events and contacts are up to a
  sixtieth of a second ahead of what is drawn. It is off by default. A call that
  takes no step still returns a diff, and a body at rest adds nothing to it.
- **A move through `Physics.Place` needs `ResetSmoothing(entity)`,** or the body
  is drawn crossing the gap. An edit through `Apply` needs nothing.
  `ResetSmoothing`, `StopSmoothing` and `StartSmoothing` answer false for an
  entity the document does not have. `StopSmoothing` covers the entity and
  everything under it.
- **A snapshot does not cross smoothing modes.** `Restore` throws an
  `ArgumentOutOfRangeException` when the scene and the snapshot differ in
  `Smooth`, and changes nothing.
- **Ids in a scene.** Numbers from 1 up are `ScenePhysics`'s. A body added
  straight to `scene.Physics` needs an id from the top of the `ulong` range; it
  is simulated but never written back. `BodyOf` and `EntityOf` convert between
  entity and body; an entity keeps its number while it has a body.
- **A body falling asleep does not end its touches.** A pair entirely at rest
  is held with its island and put back when it wakes, so there is no
  `TouchEnded`. A pair with a driven body or a character is left to end. Look
  for `Slept` and `Woke`, or ask `physics.Asleep(id)`.
- **A plane cannot be the shape of a cast.** An infinite surface is touching
  everything already, so asking gives null. A plane cannot be asked about by
  `Overlap` either, and that finds nothing. Other casts can hit a plane.
- **Removing ground wakes nothing.** A body asleep on it stays in the air until
  something wakes it (`Wake(id)`), which is what keeps a crate in place while
  its region streams out and back.
- **`Sync` with `refresh: false` during a stroke, then once without.** Left
  true, every frame of a brush stroke relays the regions under it. A colour or
  cover paint moves the revision too, so it relays as well.
- **`Orblit.Terrain` never depends on physics.** Collision is the bridge's job;
  `HeightAt` is still how to place a thing on the ground without it. Its
  `Terrain` class shares a name with the namespace, and `Orblit.Physics.Terrain`
  is a namespace too, so alias the class
  (`using TerrainData = Orblit.Terrain.Terrain;`).
- **A character does not fall on its own.** Leave gravity out of `Drive` and it
  hangs in the air.
- **Build each request from the footing, not from the last request.** Read
  `FootingOf` before `Drive`. Its `Velocity` has already lost whatever the
  floor or a wall stopped, so a character standing still does not pile up a
  fall it is not taking and then drop through the next hole at full speed.
- **A character on the way up is never grounded,** so `Grounded &&
  jumpPressed` launches it once. It does not keep adding the launch every step
  while the key is held.
- **Drive a character before every step.** Left alone, it keeps what survived
  of its last request and gains no more gravity, so it floats.
- **A character in a scene is a body with `BodyMotion.Character`.** Give it
  `stepHeight`, `steepest`, `skin`, `strength` and `crouchHeight` (zero is no
  crouch). Only a capsule, ball, box or cylinder is one.
- **`scene.Crouch(entity, true)`** answers whether the character now is as
  asked. Standing needs room, and under a beam it says false and `IsCrouching`
  stays true. The entity's feet stay put through either.
- **A character added straight to `scene.Physics`** with an id from the top of
  the range still works, and still has to be driven before each step.
- **A plane cannot be a character.** `AddCharacter` takes it, but it never has a
  footing.
- **Name clashes.** `Shape` and `Layers` are in both `Orblit.Physics` and
  `Orblit.Collide`. Write one of them in full, or alias it. `BodyComponent.Shape`
  is a property, while `Orblit.Physics.Shape` is a type. `Frame`, `Hit`,
  `Node` and `Wait` also exist in more than one Orblit package.
- **Determinism is per build.** A restored world stepped with the same commands
  and the same deltas gives the same bits on the same build. Nothing is
  promised between builds, compilers or machines, because floating point
  differs, so a replay that crossed one would drift. Sync state rather than
  inputs between machines.
- **Fast bodies go through thin ones.** Speculative contacts hold most of them.
  For a small fast body against a thin fixed wall, set `Continuous`.
- **A joint's axes are its first body's.** Every measure is `b` as `a` sees it,
  so with the world as `a` a door's angle is the door's, and with the world as
  `b` it reads the other way round. `ScenePhysics` puts the world, or the body
  above, first.
- **Turn limits are three angles: a twist about x, then a swing about y and
  z.** Within a quarter turn they are the angles they look like. Further, the
  three stop being independent, as any three angles do.
- **`Remove` drops a body's joints silently.** No `Broke` event, because
  nothing broke; what they held is woken and falls. Laying ground again keeps
  joints on it.
- **`Broke` names the joint, not a body.** `physicsEvent.A` is the joint id,
  which may equal a body id. Map it with `scene.EntityOfJoint`, not `EntityOf`.
  The mapping is kept after the joint breaks.
- **Radians in `Orblit.Physics`, degrees in a document.** `new JointLimit(0,
  90)` on a hinge is ninety radians, not a quarter turn. `JointStateOf` answers
  in radians even for a joint the document made.
- **An edit remakes a joint as things stand then.** A door edited half open
  reads nought half open, and its limits move with it.
- **A broken scene joint stays broken** until its own entity is edited. Moving
  the body, or a parent, does not mend it.
- **Joined bodies do not collide with each other** unless `collide` is true.
- **Trigger events name the trigger first,** not the smaller id. Keying every
  event on the smaller id misses them.
- **A solid question never sees a trigger.** A ray through a pond hits what is
  under it. Ask with `triggers: true` to find the zone. A trigger cannot be
  found by a solid `Overlap` either.
- **`TouchStay` stops while a pair sleeps, and `Inside` does not.** Do not read
  a missing `TouchStay` as "left".
- **A zone needs a body on its entity** and is a trigger, so it is fixed or
  driven. On a free body it does nothing. It never moves a character.
- **A zone's null field is the body's own,** not zero.
- **A belt's velocity is in the world,** not the entity's frame, and only the
  part along the touched surface counts.
- **A rule is per pair and data.** There is no per-contact callback, and no
  per-pair surface speed. A belt is per body.
- **`scene.Ignore` needs both bodies and takes entity ids.** It answers false
  for a missing body or an entity against itself. The pair is kept when an edit
  rebuilds a body, and forgotten when an entity loses its body or leaves the
  document. It is not in the document, because an id inside a prefab instance is
  a path that would not survive the file being read again. It replaces any rule
  the world had for the pair. A character walks through a body it ignores.
- **`SetRule` wakes both bodies,** so `ignore` wakes a sleeper too.
- **A cap is per step.** A body can pass `MaxSpeed` inside a step. It never
  leaves one above it.
- **A lock is in the world's axes,** not the entity's. A body turned a quarter
  about y and locked along x is locked along the world's x.
- **`SetControls` wakes the body** unless `quiet` is true. The bridge sends
  controls quietly when it adds a body, so `startsAsleep` still holds.
- **A given `Inertia` is used as it is.** It is not moved to the centre of mass
  and not scaled by the entity. One zero part hands all three back to the
  shape's.
- **A trigger cannot change motion.** `SetMotion` ignores a trigger, a
  character and ground, with no error.
- **Dispose snapshots.** They are native memory. `Restore` with a disposed one
  throws an `InvalidOperationException` and changes nothing. A snapshot is not
  a file and does not leave the process.
- **A restore gives the view a diff.** `scene.Restore` changes
  `scene.Document`, so pass its `SceneDiff` to `view.Apply`, or the view shows
  the frame before.
- **A replay needs the same step.** `ScenePhysics` rolls its body and joint
  numbers back, so an entity added after the snapshot gets the same number
  again. A snapshot restored into a scene with another `Step` does not replay.
- **`Contacts` and `Stats` are the last step's.** A pair asleep is not in
  `Contacts`, and `StepMicroseconds` is not in a snapshot.
- **See the contacts with `DebugLines`.** `Orblit.Stage`'s `DebugLines` draws
  segments over a scene, and knows nothing of physics. Turn the last step's
  `Contacts` into `DebugLine`s and pass them to `Draw`. The editor has inspector
  sections for bodies, zones and joints.

## Compound and scaled shapes

- `Physics.LayCompound(id, parts, scale)` takes 1 to 64 convex parts and
  returns false for an invalid or already used id. `Shape.Compound(id)` names
  it for bodies, casts and overlaps. A single part gives a scaled or offset
  shape. A compound keeps the empty gaps between its parts.
- `new ShapePart(shape, at, rotation, scale)` scales, turns, then translates a
  convex shape: a sphere, box, capsule, cylinder or hull. Rotation is xyzw. It
  throws `ArgumentException` for a shape that is not convex, a scale that is not
  positive or a rotation of all zeros. A hull part must be laid first. Planes,
  meshes, ground and other compounds cannot be parts.
- The whole compound has one uniform density. Overlapping parts count their
  mass twice.
- `DropCompound(id)` refuses while a body uses it. Remove the bodies, drop the
  compound, then drop the hulls it used. A snapshot keeps its own copy of the
  assets, so the live world can drop them.
- `new BodyComponent(BodyShape.Compound, parts: [...])` uses `BodyPart`. A part
  has `shape`, `size`, `radius`, `height`, `hull`, `centre`, `rotation` (a
  quaternion) and `scale`. Scale, turn and centre are local to the compound.
  Dynamics and material belong to the body that holds the part.
- `BodyComponent.shapeScale` defaults to all ones and must be positive in every
  component. It stretches the collision geometry before the entity's own
  transform, and rounded shapes stretch exactly. A plane ignores it.
- A body's origin is placed by `centre`. `centreOfMass` moves the balance point
  from the middle of the shape.
- The bridge makes and drops the hulls a scene body uses. Use ids from the top
  of the `ulong` range for hulls and compounds added straight into
  `scene.Physics`, since small ids belong to the document.
- The editor's body section edits the parts of a compound.

## Static mesh collision

- `Physics.LayMesh(id, vertices, indices)` copies xyz triples and triangle
  vertex indices. It leaves out degenerate triangles. It returns false for a
  repeated id, an index out of range, a non-finite coordinate or empty
  geometry.
- `Shape.Mesh(id)` names the asset. Only fixed bodies can use a mesh
  (`PhysicsMotion.Fixed`). The surface is two-sided.
- A mesh is a sheet, not a solid volume. Openings stay empty and an object
  wholly inside a closed mesh does not overlap until it touches a surface. Use
  hulls or convex compounds for moving concave bodies.
- Remove bodies before `DropMesh(id)`. Snapshots retain geometry
  independently.
- `CollisionMesh.FromGltfAsync(id, bytes, source)` in `Orblit.Asset` bakes the
  active scene's transforms and instances and expands strips and fans. Skins and
  morph targets are refused. `CollisionMesh.FromObj(bytes)` reads every face of a
  Wavefront OBJ as one mesh, with polygons cut into triangles. Model settings
  `collision: true` emit `collision.json`; `CollisionMesh.Decode` reads it. A
  `CollisionMesh` has `Vertices` and `Indices` to hand to `LayMesh`.
- A scene `new BodyComponent(BodyShape.Mesh, meshVertices: ..., meshIndices:
  ...)` forces fixed motion and applies entity scale and `shapeScale` before
  placing geometry at `centre`. Invalid geometry remains editable and inert.
  The bridge owns its mesh by body number. Use ids from the top of the `ulong`
  range for meshes laid directly in `scene.Physics`.
- In the editor, the **Collide with this model** button reads the model's
  triangles and stores them as a fixed collider in the scene. Press it again
  after changing the source geometry.
