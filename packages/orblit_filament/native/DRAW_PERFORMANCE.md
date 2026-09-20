# Batching and the depth prepass

Two switches on `OrblitScene` change how a scene becomes draws: `batching`,
which merges repeated objects into one instanced renderable, and
`depthPrepass`, which draws opaque geometry into depth before shading it.
Neither changes the picture by more than a few pixels; what they change is
time. This is the record of what was measured and why each one defaults the
way it does. The doc comments on the two fields say what they do; this says
what they cost.

## Batching

*Whether objects that are the same thing are drawn together.* On by default.

### What it does

A hundred crates with one mesh, one material and the same shadow and layer
settings are a hundred draws per pass without this, and one with it: the
renderer builds the group as a single renderable, manually instanced — each
copy's own transform in its own slot of a buffer built for the purpose —
rather than drawing each crate on its own. A group past sixty-four members
becomes more than one such renderable, because that is as many copies as one
can carry, but it is still a handful of draws rather than one per crate.
Nothing about the objects themselves changes — each is still its own entry
in `objects` with its own key, moving one moves only that one, and picking
still answers with the one that was clicked, because picking never asks the
renderer which entity is at a pixel; it works from this list, same as ever.

### What batches

What batches is decided per publish, by counting. Four or more objects with
the same `OrblitObject.mesh`, the same `OrblitObject.material` and the same
flags form a group; a placeholder cube on the default surface also needs the
same `OrblitObject.colour`, because on that surface the colour *is* the
material. An object with `OrblitObject.morphWeights` never batches, because
its shape is its own, and neither does a model wearing its own file's
materials, because every copy of a model comes with its own set of them —
give such a model an `OrblitMaterial` and the census counts it like anything
else, though the renderer does not yet build a merged draw for a named mesh,
only for the placeholder cube; such a model still draws correctly, just as
its own renderable, unmerged.

### What this costs

A merged group shares everything in Filament that is set per renderable
rather than per instance: the shadow and layer flags (already guaranteed
identical within a group by what makes a group) and, more visibly, culling.
Filament culls a renderable by one box, so a group's box is the union of its
members' — up to sixty-four of them — and a member outside the camera's view
still draws if another member of its own chunk of sixty-four is inside it.
The same is true of the shadow pass: a member outside the light's view can
still cast if a chunk-mate is inside it. Neither ever *hides* something that
should be visible or shadowing — the union box can only be a superset of
what a member-by-member account would cull — so the cost is some wasted
drawing at the edge of a chunk, not a wrong picture. Members are sorted by
where they are in the world before being split into chunks of sixty-four,
precisely so that a chunk is a compact patch rather than members scattered
across the whole group, which keeps this cost small in practice: a scene
with objects that are already laid out somewhat together — a grid, a
cluster, a tile — pays very little for it.

### On by default

Measured on three thousand crates: a third of the CPU time and GPU time of
drawing them unbatched, and the draw count falls from thousands to dozens.
Where nothing in a scene batches — nothing repeats often enough, or every
copy differs in colour or material — turning this on changes nothing,
measured to the pixel: there is nothing to merge, so nothing is drawn
differently.

Where something *does* batch but no shadow pass runs over it, the difference
is a pixel or two: three thousand crates with the shadow pass off differ by
two pixels in a 1600x1200 frame, and forty-eight overlapping slabs sharing
one material by one, every one of them by a single level in 255, against a
noise floor of exactly nothing. That is the last of the float rounding in
recovering a chunk's half-extent from the union of its members', and it is
the same rounding as the hundred and six pixels at one member to a chunk
below.

Where a batched group also casts shadows — crates in a pattern, one in five,
is the case this was measured on — the frame is close but not bit-identical:
with the clock pinned, 2.27% of pixels differ, by 2.6 parts in 255 on
average, along the edges of shadows rather than scattered across every
silhouette or missing from a whole object. Two runs of the same frame are
bit-identical, so that is a real difference and not noise. Turning the
shadow pass off takes it back down to those two pixels, which is what places
it in the shadow pass.

That 2.27% is the worst case rather than the usual one, and it is worth
knowing how far from usual. It comes from three thousand crates spread
across a wide grid, where a chunk of sixty-four spans a large volume and its
union box is correspondingly loose. Scenes that batch a dozen or a couple of
hundred objects standing near each other barely move at all, measured the
same way with the clock pinned: the Shadows example (twelve objects in one
group) differs by seven pixels, the Benchmark example (two hundred objects
in seventeen groups) by three — which is inside that example's own two-to-
four-pixel noise floor — and the Environment volumes example (fifteen
objects in two groups) not at all.

### It is the group's box, and now only the group's box

This record used to say the difference was 2.97%, and that the bounding box
had been ruled out because shrinking a group to one member still left 2.75%
behind. The test was sound and the conclusion was wrong, because the thing
it was compared against was itself wrong: the placeholder cube declared a
box that did not contain it — Filament's `Box` is a centre and a half-
extent, and the declaration was a {min,max} pair — so the *unbatched* side
was fitting its shadows from the wrong volume, and no amount of shrinking a
chunk could reveal that. With the declaration corrected, one member to a
chunk differs by 0.0055%: a hundred and six pixels, every one of them by a
single level, which is the float rounding left in recovering a chunk's half-
extent from the union of its members'. The two paths agree.

What is left at sixty-four members is the grouping, behaving as a union of
boxes should: a chunk's box is looser along the light axis than any
member's, so the shadow camera fits a deeper volume and the map's texels
land differently. It saturates immediately — eight members to a chunk
measures 2.22% against sixty-four's 2.27% — so no chunk size buys the
difference back while still batching anything.

So this is on by default, because the trade is now a named one rather than
an unexplained difference: a bounded, saturating difference at the edges of
shadows, against three thousand renderables where fifty-one would do. It is
safe in the sense that mattered most: it no longer touches the Filament
feature that used to blacken a frame outright (see below), so the worst this
can now do is a scattering of pixels near a shadow's edge, never a black
screen. A scene where nothing batched casts a shadow stays bit-identical.

What it was waiting on was a cause, not a smaller number. While the 2.97%
was unexplained it could have been anything, up to and including something
that would swallow a whole object; now that it is known to be a union of
boxes behaving as a union of boxes, its shape is known too — shadow edges, a
couple of levels, never a silhouette and never a missing object — and it
stops getting worse past eight members to a chunk. That is a difference a
scene can be shipped with.

### Not Filament's automatic instancing, and deliberately so

An earlier version of this switched on
`Engine::setAutomaticInstancingEnabled` and let Filament notice, after the
fact, that several draws it had already built could be merged. On stock
Filament 1.76 that path is broken: `RenderPass::instanceify()` compares a
leftover custom command as though it were a draw, and can fold the colour-
grading subpass into a neighbouring instanced run so it never executes — the
whole frame comes back entirely black, on some scenes and not others, with
no way to tell beforehand which a given scene is. Building the merged
renderable directly, with `RenderableManager::Builder::instances`, never
asks Filament to notice anything after the fact, so that bug is never
reached — which is what let three scenes that used to come back black with
instancing forced on render correctly once batching stopped asking for it. A
fix for the underlying Filament bug exists, on Orblit's own Filament fork,
in no release yet; it no longer matters to this switch, because nothing here
depends on it any more.

Turned off with `batching: false`, per scene and per publish — a scene that
wants every renderable culled on its own, or one being compared against a
frame drawn before this was the default, says so and gets exactly the old
path. Leaving this unset turns it on.

## The depth prepass

*Whether opaque objects are drawn into depth alone before they are shaded.*
Off by default.

### What it does

A depth prepass draws every opaque object twice: once writing depth and no
colour, then once shaded. The second time, every fragment the first pass
already covered from in front fails the depth test and is thrown away before
a single light is evaluated — so a pixel is shaded once however many
surfaces stand over it. What it costs is a second entity, a second frustum
cull and a second draw for every object it covers.

### Which objects it covers

Ones drawn as the placeholder cube, that are visible, and whose surface is
opaque. A model out of a glTF file is not covered: Filament's public
`RenderableManager` has `setGeometryAt` but no matching getter, so there is
no way to ask a primitive which buffers it draws and therefore no way to
build a second renderable over the same geometry. A masked surface punches
its own pixels out by alpha and a blended one never owns its pixels, so
depth written for either would be depth in the wrong place. Anything not
covered draws exactly as it always did — it simply gets no help. This is
also true of objects merged by `batching`: a batched group is one instanced
renderable and gets no prepass, so the two switches do not compound.

### Off by default, and that is a measurement rather than caution

On an Apple GPU this is worth nothing, because the hardware already does it:
a tile-based renderer works out which surface wins a tile before it shades
any of it, which is a prepass in silicon, so there is no fragment cost left
for a second pass to save and the second pass is pure addition. Measured on
an M4 Pro, on the Overdraw example — the scene built precisely to make
overdraw expensive, every slab crossing every other so that no order of
objects is the right order for any pixel — three runs each way, medians of
recent frames:

| slabs | prepass off | prepass on |
|-------|-------------|------------|
| 96    | 3.97 ms     | 4.10 ms    |
| 48    | 3.84 ms     | 3.89 ms    |
| 1     | 3.47 ms     | 3.47 ms    |

Run to run within one setting the spread is a hundredth of a millisecond, so
the ninety-six-slab figure is a real and repeatable *loss* of about three
per cent, not noise: the second pass adds ninety-seven draws and ninety-
seven culls and saves nothing, because there was nothing to save. See the
Overdraw example for the exact commands.

### On an immediate-mode GPU it is the other way round

Such a GPU shades every layer it is handed in the order it is handed them,
so hidden surface costs real time and removing it saves real time. Measured
on the same scene through the same renderer, on Mesa's llvmpipe under Vulkan
— a software rasteriser, and immediate-mode by construction — medians of
three runs each way:

| slabs | prepass off | prepass on |
|-------|-------------|------------|
| 96    | 40.9 ms     | 21.3 ms    |
| 48    | 44.0 ms     | 17.3 ms    |
| 1     | 11.0 ms     | 11.3 ms    |

Roughly half the frame, where there is real depth complexity to remove. The
one-slab row is the control and it says the same thing on both machines:
with nothing hidden there is nothing to save, and the second pass costs
about three per cent for its trouble.

### Why it still ships off

The obvious move — default it on wherever the backend is not Metal — is not
supported by what was actually measured. The only immediate-mode hardware
available here was a software rasteriser, and a software rasteriser is the
most favourable possible case for a prepass: it has no early-Z, no
hierarchical depth and no compression, so every fragment it skips is a
fragment genuinely shaded on the CPU. A real desktop GPU has all three and
behaves far more like the tile-based case for this workload. Defaulting this
on for every non-Metal backend would be extrapolating from llvmpipe to
hardware nobody has measured, so it stays an opt-in until somebody measures
a discrete GPU.

So: turn it on for a backend and a scene you have measured, and leave it off
otherwise. The picture is identical either way — bit-for-bit, on both Metal
and Vulkan — so the only thing at stake is time.

### What it does not change

The shaded draws keep the depth test they already had. Filament renders
reversed-Z and its opaque draws test greater-or-equal, so a fragment the
prepass has covered is already rejected; tightening the test to equal would
reject the same fragments and gain nothing, while risking an object
vanishing outright if the two vertex shaders disagree about a position by
one unit in the last place.

Turned off with `depthPrepass: false`, which is also what leaving this unset
does.
