using Orblit.Filament;
using Orblit.Light;
using Orblit.Scene;
using Orblit.VectorMath;
using Orblit.Weather;

namespace Orblit.Stage;

/// <summary>What each part of a document becomes for the renderer, with nothing remembered between calls.</summary>
internal static class DocumentStaging
{
    private const double MoonHaloSize = 3;
    private const double MoonHaloFalloff = 240;
    private const double SunHaloSize = 12;
    private const double SunHaloFalloff = 70;

    // Where a camera or a light looks when it carries no rotation: down its own negative Z.
    public static Vector3 Facing(Matrix4 world)
    {
        var forward = world.GetRotation().Transformed(new Vector3(0, 0, -1));
        forward.Normalize();
        return forward;
    }

    public static RenderCamera CameraAt(Matrix4 world, CameraComponent camera)
    {
        var position = world.GetTranslation();
        return new RenderCamera
        {
            Position = position,
            Target = position + Facing(world),
            FieldOfView = camera.FieldOfView,
            Near = camera.Near,
            Far = camera.Far,
        };
    }

    public static RenderCamera DefaultCamera() =>
        new() { Position = new Vector3(6, 4, 8), Target = Vector3.Zero() };

    public static RenderLight LightFrom(long key, LightComponent light, Matrix4 world)
    {
        var ready = new LightSource(
            light.Kind,
            light.Colour.Linear,
            light.Power,
            light.SourceRadius,
            light.SunAngle,
            light.SpotSize,
            light.SpotBlend,
            sizeX: light.SourceRadius * 2,
            sizeY: light.SourceRadius * 2,
            castShadows: light.CastShadows).ToRenderer();
        var moon = light.Body == CelestialBody.Moon;

        return new RenderLight
        {
            Key = key,
            Kind = KindOf(ready.Kind),
            Colour = ready.Color,
            Intensity = ready.Intensity,
            Position = world.GetTranslation(),
            Direction = Facing(world),
            FalloffRadius = double.IsFinite(ready.FalloffRadius) ? ready.FalloffRadius : 0,
            InnerConeAngle = ready.InnerConeAngle,
            OuterConeAngle = ready.OuterConeAngle,
            SunAngularRadius = ready.SunAngularRadius,
            SourceRadius = ready.SourceRadius,
            HaloSize = moon ? MoonHaloSize : SunHaloSize,
            HaloFalloff = moon ? MoonHaloFalloff : SunHaloFalloff,
            CastShadows = ready.CastShadows,
            Width = ready.Width > 0 ? ready.Width : 1,
            Height = ready.Height > 0 ? ready.Height : 1,
        };
    }

    public static SpriteLayer SpriteFrom(int key, SpriteComponent sprite, Matrix4 world, string? image)
    {
        var colour = sprite.Colour.Linear;
        var one = new LayerSprite
        {
            X = 0,
            Y = 0,
            Width = sprite.Width,
            Height = sprite.Height,
            Depth = sprite.Depth,
            PivotX = sprite.PivotX,
            PivotY = sprite.PivotY,
            Red = colour.X,
            Green = colour.Y,
            Blue = colour.Z,
            Alpha = sprite.Opacity,
        };

        return new SpriteLayer(key, SpriteLayer.Pack([one]))
        {
            Image = image is null ? null : new Texture(image),
            Transform = world,
            Blend = sprite.Additive ? SpriteBlend.Add : SpriteBlend.Alpha,
        };
    }

    public static SplatCloud SplatsFrom(int key, string path, Matrix4 world, SplatsComponent splats) => new(key, path)
    {
        Transform = world,
        Harmonics = (int)Math.Clamp(splats.Harmonics ?? 2, 0, 3),

        // Clamped into int range first, so a huge budget is refused by the cloud and does not wrap.
        Limit = splats.Budget is { } budget ? (int)Math.Clamp(budget, int.MinValue, int.MaxValue) : null,
    };

    public static Sky SkyFrom(SceneSettings settings, WeatherState? air)
    {
        var colour = settings.Sky.Linear;
        var grey = air is null ? 0 : Math.Clamp(air.Greying * 0.8, 0, 1);
        return new Sky
        {
            Colour = colour * (1 - grey),
            Ambient = settings.Ambient * (air?.Scattered ?? 1),
        };
    }

    public static Fog FogFrom(WeatherState? air) => air is null || air.FogDensity <= 0
        ? Fog.None
        : new Fog
        {
            Colour = air.FogColour.Linear,
            Density = air.FogDensity,
            Height = air.FogHeight,
            HeightFalloff = air.FogFalloff,
            Structure = air.Mist,
            FeatureSize = air.MistSize <= 0 ? 0.02 : 1 / air.MistSize,
        };

    public static Precipitation PrecipitationFrom(WeatherComponent? weather)
    {
        var falling = Math.Max(weather?.Air.Rain ?? 0, weather?.Air.Snow ?? 0);
        if (weather is null || falling <= 0)
        {
            return Precipitation.None;
        }

        return new Precipitation
        {
            Amount = falling,
            Fall = weather.Air.Snow > weather.Air.Rain ? 1.2 : 9,
            Wind = WindVector(weather.WindDirection, weather.Air.WindSpeed),
        };
    }

    // The bearing is where the wind comes from, and the vector is where it blows to.
    private static Vector2 WindVector(double bearing, double speed)
    {
        var towards = Doubles.Radians(bearing + 180);
        return new Vector2(Math.Sin(towards), Math.Cos(towards)) * speed;
    }

    private static RenderLightKind KindOf(RendererLightKind kind) => kind switch
    {
        RendererLightKind.Directional => RenderLightKind.Directional,
        RendererLightKind.Point => RenderLightKind.Point,
        RendererLightKind.Spot => RenderLightKind.Spot,
        _ => RenderLightKind.Area,
    };
}
