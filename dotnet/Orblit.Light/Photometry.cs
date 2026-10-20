namespace Orblit.Light;

/// <summary>
/// Converting between how a light is stated and how it is rendered.
/// An artist states a light in watts, because that is what a real fixture is
/// labelled with. A renderer works in lumens and lux, which describe what the
/// eye receives. Getting the conversion wrong is why imported scenes arrive
/// blown out or black.
/// </summary>
public static class Photometry
{
    /// <summary>
    /// Lumens per watt at 555 nm, the peak of the eye's sensitivity. Using the
    /// peak matches Blender's own glTF export, so a scene round trips at the
    /// brightness it was authored with.
    /// </summary>
    public const double LuminousEfficacy = 683.0;

    /// <summary>Watts to lumens, for a point or spot light, which emit into the whole sphere.</summary>
    public static double WattsToLumens(double watts) => watts * LuminousEfficacy;

    /// <summary>Irradiance in watts per square metre to lux, for a sun, whose rays are parallel.</summary>
    public static double IrradianceToLux(double irradiance) => irradiance * LuminousEfficacy;

    /// <summary>Luminous power to intensity, for a source radiating in every direction.</summary>
    public static double LumensToCandela(double lumens) => lumens / (4 * Math.PI);

    public static double CandelaToLumens(double candela) => candela * 4 * Math.PI;

    /// <summary>
    /// How far light of a given power falls to a threshold. Light never reaches
    /// zero, so a renderer needs a distance to stop caring at.
    /// </summary>
    public static double InfluenceRadius(double lumens, double cutoffLux = 0.1) =>
        lumens <= 0 || cutoffLux <= 0 ? 0 : Math.Sqrt(LumensToCandela(lumens) / cutoffLux);
}
