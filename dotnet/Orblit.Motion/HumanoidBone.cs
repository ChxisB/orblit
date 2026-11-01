namespace Orblit.Motion;

/// <summary>
/// A place in a person's body that a bone can fill.
/// </summary>
/// <remarks>
/// The same set whichever tool made the rig, so two skeletons with nothing in common but their shape can be
/// paired slot for slot.
/// </remarks>
public enum HumanoidBone
{
    Hips,
    Spine,
    Chest,
    UpperChest,
    Neck,
    Head,
    LeftShoulder,
    LeftUpperArm,
    LeftLowerArm,
    LeftHand,
    RightShoulder,
    RightUpperArm,
    RightLowerArm,
    RightHand,
    LeftUpperLeg,
    LeftLowerLeg,
    LeftFoot,
    LeftToes,
    RightUpperLeg,
    RightLowerLeg,
    RightFoot,
    RightToes,
}
