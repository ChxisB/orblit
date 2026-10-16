/// Armatures, poses and the maths a control rig is generated on top of.
///
/// A skeleton is described by where its bones start and end, because that is
/// what an artist adjusts. Everything else — orientations, world matrices, the
/// matrices that skin a mesh — is derived from that rather than stored beside
/// it, so there is one source of truth about where a bone is.
library;

export 'src/armature.dart'
    show Armature, ArmatureError, IkChain, Pose, PoseTransform;
export 'src/bone.dart' show Bone;
export 'src/collections.dart' show BoneCollection, BoneCollections;
export 'src/constraints.dart'
    show
        BoneConstraint,
        CopyRotation,
        CopyTransform,
        DampedTrack,
        LimitRotation,
        StretchTo,
        TrackAxis;
export 'src/face_rig_types.dart' show EyeRig, EyelidRig, JawRig, faceCollection;
export 'src/generator.dart'
    show GeneratedRig, MetaRig, RigGenerator, RigProblem;
export 'src/ik.dart'
    show
        IkSolution,
        rotationBetween,
        solveFabrik,
        solveTwoBoneIk,
        swingTwist,
        twistAngle;
export 'src/modifiers/bone_attachment.dart' show BoneAttachment;
export 'src/modifiers/bone_edit.dart' show aimBone, placeBone, turnBone;
export 'src/modifiers/chain_ik.dart' show ChainIk;
export 'src/modifiers/constrain.dart' show Constrain;
export 'src/modifiers/foot_placement.dart'
    show FootLeg, FootPlacement, GroundHit, GroundProbe;
export 'src/modifiers/joint_limit.dart' show ConeLimit, HingeLimit;
export 'src/modifiers/look_at.dart' show LookAt;
export 'src/modifiers/modifier.dart'
    show Modifier, ModifierFrame, ModifierSlot, ModifierStack, ModelPoint;
export 'src/modifiers/speed_limit.dart' show SpeedLimit;
export 'src/modifiers/spline_ik.dart' show SplineIk, sampleCurve;
export 'src/modifiers/spring_bones.dart'
    show
        CapsuleCollider,
        PlaneCollider,
        SphereCollider,
        SpringBones,
        SpringCollider;
export 'src/modifiers/twist_spread.dart' show TwistSpread;
export 'src/modifiers/two_bone_ik.dart' show TwoBoneIk;
export 'src/naming.dart' show BoneNaming, BoneRole, Side;
export 'src/rig_type.dart' show RigContext, RigType;
export 'src/rig_types.dart' show CopyRig, FingerRig, LimbRig, SpineRig;
export 'src/widgets.dart' show BoneWidget, WidgetShape;
