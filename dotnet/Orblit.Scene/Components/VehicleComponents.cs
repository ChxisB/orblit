using System.Collections.ObjectModel;

namespace Orblit.Scene;

/// <summary>
/// A car: the body this entity has, held up by the wheels under it.
/// </summary>
/// <remarks>
/// The entity also has a body component, which is the chassis: its shape, its mass, its centre of
/// mass and its layers. The wheels are the entity's children that have a <see cref="WheelComponent"/>,
/// and only its direct children. A chassis that is scaled is not a vehicle.
/// <para>
/// Every field is written every time, including the engine of a car that has no wheel that drives,
/// for the reason the body component gives.
/// </para>
/// </remarks>
public sealed class VehicleComponent : SceneComponent
{
    /// <summary>A small petrol engine: revolutions a minute and newton metres, in turn.</summary>
    public static readonly IReadOnlyList<double> DefaultTorque = [1000.0, 160.0, 3000.0, 220.0, 5000.0, 240.0, 6500.0, 200.0];

    /// <summary>Five forward gears.</summary>
    public static readonly IReadOnlyList<double> DefaultRatios = [3.6, 2.2, 1.5, 1.1, 0.9];

    // F2: nine fields of one car, called by name.
    public VehicleComponent(
        double maxSteer = 30,
        double antiRoll = 0,
        IReadOnlyList<double>? torque = null,
        double idle = 900,
        IReadOnlyList<double>? ratios = null,
        double reverse = 3.2,
        double finalDrive = 3.7,
        double @lock = 0,
        long ground = 0xFFFFFFFF)
    {
        MaxSteer = maxSteer;
        AntiRoll = antiRoll;
        Torque = new ReadOnlyCollection<double>([.. torque ?? DefaultTorque]);
        Idle = idle;
        Ratios = new ReadOnlyCollection<double>([.. ratios ?? DefaultRatios]);
        Reverse = reverse;
        FinalDrive = finalDrive;
        Lock = @lock;
        Ground = ground;
    }

    /// <summary>Degrees the middle of the steering axle turns at full lock.</summary>
    public double MaxSteer { get; }

    /// <summary>
    /// Newtons a metre of difference in compression across an axle, as a bar joining its two wheels.
    /// Nought is no bar.
    /// </summary>
    public double AntiRoll { get; }

    /// <summary>
    /// The engine's torque with the pedal down: revolutions a minute, then newton metres, for each
    /// point in order of speed. The last is the redline.
    /// </summary>
    public IReadOnlyList<double> Torque { get; }

    /// <summary>Revolutions a minute the engine ticks over at.</summary>
    public double Idle { get; }

    /// <summary>
    /// The forward gears, first to last, as turns of the engine for a turn of the gearbox's output.
    /// Used only when a wheel drives.
    /// </summary>
    public IReadOnlyList<double> Ratios { get; }

    public double Reverse { get; }

    public double FinalDrive { get; }

    /// <summary>
    /// Newton metres the differential of an axle may move to the slower wheel. Nought is open. A very
    /// large one is locked.
    /// </summary>
    public double Lock { get; }

    /// <summary>The layers a wheel sees as ground, one bit each, so a car does not stand on a character.</summary>
    public long Ground { get; }

    public override string Type => SceneComponents.Vehicle;

    public static VehicleComponent FromJson(JsonMap json) => new(
        Values.Number(json, "maxSteer", 30),
        Values.Number(json, "antiRoll", 0),
        SomeNumbers(Values.At(json, "torque")),
        Values.Number(json, "idle", 900),
        SomeNumbers(Values.At(json, "ratios")),
        Values.Number(json, "reverse", 3.2),
        Values.Number(json, "finalDrive", 3.7),
        Values.Number(json, "lock", 0),
        Values.Bits(json, "ground", 0xFFFFFFFF));

    // F2: nine fields of one car, called by name.
    public VehicleComponent CopyWith(
        double? maxSteer = null,
        double? antiRoll = null,
        IReadOnlyList<double>? torque = null,
        double? idle = null,
        IReadOnlyList<double>? ratios = null,
        double? reverse = null,
        double? finalDrive = null,
        double? @lock = null,
        long? ground = null) => new(
        maxSteer ?? MaxSteer,
        antiRoll ?? AntiRoll,
        torque ?? Torque,
        idle ?? Idle,
        ratios ?? Ratios,
        reverse ?? Reverse,
        finalDrive ?? FinalDrive,
        @lock ?? Lock,
        ground ?? Ground);

    public override JsonMap ToJson() => new OrderedDictionary<string, object?>
    {
        ["maxSteer"] = MaxSteer,
        ["antiRoll"] = AntiRoll,
        ["torque"] = Values.NumbersToJson(Torque),
        ["idle"] = Idle,
        ["ratios"] = Values.NumbersToJson(Ratios),
        ["reverse"] = Reverse,
        ["finalDrive"] = FinalDrive,
        ["lock"] = Lock,
        ["ground"] = Ground,
    };

    /// <summary>
    /// The list a file gives, or null for the default when it gives none. An empty list is kept: it
    /// is a car the spec will refuse, not a car with an engine nobody wrote.
    /// </summary>
    private static IReadOnlyList<double>? SomeNumbers(object? raw) => raw is JsonList ? Values.Numbers(raw) : null;
}

/// <summary>
/// One wheel of a <see cref="VehicleComponent"/>, on an entity that is a direct child of it.
/// </summary>
/// <remarks>
/// The entity's position is where the wheel sits when the car is at rest. Its rotation is the
/// vehicle's to write: it steers, and it rolls about its own x axis, so a tyre drawn on a child of
/// this entity turns with it. The wheel's own scale is kept.
/// </remarks>
// F2: nine fields of one wheel, called by name.
public sealed class WheelComponent(
    double radius = 0.35,
    bool steers = false,
    bool drives = false,
    double brake = 3000,
    bool handbrake = false,
    double travel = 0.3,
    double hertz = 1.5,
    double damping = 0.5,
    double grip = 1) : SceneComponent
{
    /// <summary>Metres.</summary>
    public double Radius { get; } = radius;

    /// <summary>It turns with the steering.</summary>
    public bool Steers { get; } = steers;

    /// <summary>The engine turns it.</summary>
    public bool Drives { get; } = drives;

    /// <summary>Newton metres of brake at full pressure. Nought is a wheel with no brake.</summary>
    public double Brake { get; } = brake;

    /// <summary>The handbrake holds it, with the same brake.</summary>
    public bool Handbrake { get; } = handbrake;

    /// <summary>Metres the wheel can move, from hanging free to the top of its suspension.</summary>
    public double Travel { get; } = travel;

    /// <summary>Cycles a second of the car's weight on this spring, and its damping as a share of critical.</summary>
    public double Hertz { get; } = hertz;

    public double Damping { get; } = damping;

    /// <summary>The most force the rubber gives for each newton of load.</summary>
    public double Grip { get; } = grip;

    public override string Type => SceneComponents.Wheel;

    public static WheelComponent FromJson(JsonMap json) => new(
        Values.Number(json, "radius", 0.35),
        Values.Flag(json, "steers", false),
        Values.Flag(json, "drives", false),
        Values.Number(json, "brake", 3000),
        Values.Flag(json, "handbrake", false),
        Values.Number(json, "travel", 0.3),
        Values.Number(json, "hertz", 1.5),
        Values.Number(json, "damping", 0.5),
        Values.Number(json, "grip", 1));

    // F2: nine fields of one wheel, called by name.
    public WheelComponent CopyWith(
        double? radius = null,
        bool? steers = null,
        bool? drives = null,
        double? brake = null,
        bool? handbrake = null,
        double? travel = null,
        double? hertz = null,
        double? damping = null,
        double? grip = null) => new(
        radius ?? Radius,
        steers ?? Steers,
        drives ?? Drives,
        brake ?? Brake,
        handbrake ?? Handbrake,
        travel ?? Travel,
        hertz ?? Hertz,
        damping ?? Damping,
        grip ?? Grip);

    public override JsonMap ToJson() => new OrderedDictionary<string, object?>
    {
        ["radius"] = Radius,
        ["steers"] = Steers,
        ["drives"] = Drives,
        ["brake"] = Brake,
        ["handbrake"] = Handbrake,
        ["travel"] = Travel,
        ["hertz"] = Hertz,
        ["damping"] = Damping,
        ["grip"] = Grip,
    };
}
