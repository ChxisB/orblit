namespace Orblit.Editor.App.Shell;

/// <summary>
/// A kind of thing in plain words: its name, a line on what it is, more on what it does, and what it is good for.
/// </summary>
internal sealed record PlainName(string ClassName, string Name, string Line, string About, string GoodFor);

/// <summary>
/// The plain names of the engine's common classes. A class with no row here goes by its class name. The engine
/// has no summary of a class the editor can read, so the rows hold their own.
/// </summary>
internal static class PlainNames
{
    static readonly Dictionary<string, PlainName> s_rows = new PlainName[]
    {
        new("Entity", "Empty thing", "Holds a script or groups things, with no place.", "The base of every thing. It has no place in 2D or 3D.", "a game manager, a script that runs on its own."),
        new("Timer", "Timer", "Counts down, then sends a message.", "Waits for the time you set, then sends its timeout message. It can repeat.", "cooldowns, waves of enemies, a clock."),
        new("AnimationPlayer", "Animation player", "Plays animations made on the timeline.", "Holds animations that change any setting over time, and plays them.", "doors, cutscenes, character moves."),
        new("AudioStreamPlayer", "Sound", "A sound with no place, like music.", "Plays a sound at the same loudness everywhere.", "music, menu clicks, a voice over."),

        new("Entity3D", "Empty 3D point", "Holds other things in one place.", "A point in 3D with a place, a turn and a size. Things inside it move with it.", "grouping parts, a pivot to turn round, a spawn holder."),
        new("MeshInstance3D", "Shape you can see", "A box, ball or model on screen.", "Draws a mesh: a box, a ball or a model you brought in.", "walls, props, characters, anything people see."),
        new("Camera3D", "Camera", "Shows the world from where it sits.", "Shows the world from where it sits. The game draws what the current camera sees.", "the player's view, a cutscene, a security screen."),
        new("DirectionalLight3D", "Sun light", "Light from far away, like the sun.", "Lights the whole scene from one direction, like the sun. Shadows fall the same way everywhere.", "daylight, moonlight, any outdoor scene."),
        new("Marker3D", "Marker", "A spot to place or spawn things at.", "A point you see in the editor but not in the game.", "spawn points, targets, where a camera looks."),
        new("OmniLight3D", "Lamp", "Light in every direction from one point.", "Shines all round from one point and fades with distance.", "bulbs, torches on a wall, glowing pickups."),
        new("SpotLight3D", "Spotlight", "A cone of light, like a torch.", "Shines a cone from one point. Things outside the cone stay dark.", "torches, headlights, stage lights."),
        new("WorldEnvironment", "Sky and mood", "The sky, fog and glow of the scene.", "Sets the sky, the light that fills shadows, fog and glow for every camera in the scene.", "the look of a level, day or night, a misty mood."),
        new("StaticBody3D", "Solid that stays put", "Things bump into it, and it never moves.", "A body the physics treats as fixed. Put a collision shape inside it.", "floors, walls, rocks."),
        new("RigidBody3D", "Physics object", "Falls, rolls and bounces on its own.", "A body the physics moves with gravity, pushes and hits. Put a collision shape inside it.", "crates, balls, falling bits."),
        new("CharacterBody3D", "Character", "Your code moves it, and it stops at walls.", "A body your script moves. It slides along walls and stands on floors. Put a collision shape inside it.", "the player, enemies, moving platforms."),
        new("Area3D", "Trigger zone", "Notices when things come in or go out.", "A space that sends a message when a body comes in or goes out. Put a collision shape inside it.", "pickups, doors that open, damage zones."),
        new("CollisionShape3D", "Collision shape", "The shape a body bumps into things with.", "Gives the body or zone it sits in a box, ball or capsule to collide with.", "the bumper of a character, a crate or a zone."),
        new("RayCast3D", "Ray", "A line that says what it touches first.", "A line from its point that reports the first thing it hits, every frame.", "line of sight, ground checks, aiming."),
        new("Path3D", "Path", "A curve things can follow.", "A curve you draw in the view. A path follower inside it moves along it.", "patrol routes, rails, camera moves."),
        new("PathFollow3D", "Path follower", "Moves along the path it sits in.", "Sits inside a path and carries its children along the curve as its progress changes.", "a cart on rails, a guard on patrol."),
        new("GPUParticles3D", "Particles", "Many small bits, like sparks or smoke.", "Draws many small bits that start, move and fade by rules you set.", "fire, smoke, rain, sparks."),
        new("AudioStreamPlayer3D", "Sound in the world", "A sound that comes from a place.", "Plays a sound that is louder near the camera and comes from its side.", "engines, footsteps, a radio in a room."),
        new("Label3D", "Text in the world", "Words that float in 3D.", "Draws a line of text in the 3D world. It can turn to face the camera.", "name tags, signs, damage numbers."),
        new("NavigationRegion3D", "Walkable area", "Where characters can find a way.", "Holds the mesh that characters search for paths on.", "enemies that chase, crowds, guides."),
        new("CSGBox3D", "Block-out box", "A quick box to sketch a level with.", "A box you can join with, or cut from, other block-out shapes.", "sketching a level before the real models."),

        new("Entity2D", "Empty 2D point", "Holds 2D things in one place.", "A point in 2D with a place, a turn and a size. Things inside it move with it.", "grouping 2D parts, a pivot, a spawn holder."),
        new("Sprite2D", "Picture", "Shows an image in 2D.", "Draws one image, or one frame of a sheet, in 2D.", "characters, items, backgrounds."),
        new("AnimatedSprite2D", "Moving picture", "Flips through images, like a flipbook.", "Plays frames of images in order, at the speed you set.", "walk cycles, flickering flames, coins that spin."),
        new("Camera2D", "2D camera", "Shows the 2D world from where it sits.", "Sets the part of the 2D world that fills the screen. It can follow and smooth.", "following the player, screen shake."),
        new("StaticBody2D", "2D solid that stays put", "Things bump into it, and it never moves.", "A 2D body the physics treats as fixed. Put a 2D collision shape inside it.", "floors, walls, platforms."),
        new("RigidBody2D", "2D physics object", "Falls, rolls and bounces on its own.", "A 2D body the physics moves with gravity, pushes and hits. Put a 2D collision shape inside it.", "crates, balls, falling bits."),
        new("CharacterBody2D", "2D character", "Your code moves it, and it stops at walls.", "A 2D body your script moves. It slides along walls and stands on floors. Put a 2D collision shape inside it.", "the player, enemies, moving platforms."),
        new("Area2D", "2D trigger zone", "Notices when things come in or go out.", "A 2D space that sends a message when a body comes in or goes out. Put a 2D collision shape inside it.", "pickups, doors, damage zones."),
        new("CollisionShape2D", "2D collision shape", "The shape a 2D body bumps into things with.", "Gives the 2D body or zone it sits in a box, circle or capsule to collide with.", "the bumper of a character, a crate or a zone."),
        new("TileMapLayer", "Tile grid", "Paints a level from tiles.", "A grid you paint with the tiles of a tile set.", "platform levels, maps seen from above."),
        new("PointLight2D", "2D light", "Light from one point in 2D.", "Lights the 2D things round it in the shape of a picture you pick.", "torches, glowing eyes, lamps in 2D."),

        new("Control", "Empty UI box", "Holds menu parts in one place.", "The base of every menu part. It has a size and holds to the edges of the screen as you set.", "grouping a menu, a screen that holds others."),
        new("ColorRect", "Filled box", "A filled rectangle for menus.", "A rectangle filled with one flat shade.", "menu backdrops, fades, health bars."),
        new("Panel", "Panel", "A plain backdrop for menus.", "Draws the panel look of the theme behind the parts on it.", "menu windows, dialogs, tooltips."),
        new("Label", "Text", "Words on screen.", "Shows text in menus and on the screen. It can wrap.", "titles, scores, hints."),
        new("Button", "Button", "Something to press.", "A button that sends its pressed message when clicked.", "menus, dialog choices, toolbars."),
        new("TextureRect", "Image in a menu", "Shows a picture in the UI.", "Shows an image that fits its box.", "icons, logos, portraits."),
        new("LineEdit", "Text field", "One line people type in.", "A box for one line of typing, with a hint while it is empty.", "names, search boxes, chat."),
        new("ProgressBar", "Progress bar", "Fills up as a value grows.", "A bar that fills from its least to its most value.", "health, loading, timers."),
        new("VBoxContainer", "Column", "Stacks menu parts top to bottom.", "Lays out the parts inside it in a column, each under the last.", "menus, lists, settings pages."),
        new("HBoxContainer", "Row", "Lines menu parts up left to right.", "Lays out the parts inside it in a row, each after the last.", "toolbars, a row of buttons."),
        new("MarginContainer", "Padded box", "Leaves space round menu parts.", "Keeps a gap between its edges and the parts inside it.", "the edge of a screen, padding in a panel."),
    }.ToDictionary(row => row.ClassName);

    /// <summary>The row for this class, or none.</summary>
    internal static PlainName? Of(string className) => s_rows.GetValueOrDefault(className);
}
