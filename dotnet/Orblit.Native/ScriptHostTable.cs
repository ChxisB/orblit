using System.Runtime.InteropServices;

namespace Orblit.Native;

/// <summary>
/// What a script may call back into, laid out as <c>orblit_script.h</c> declares
/// it. Order and type are the contract: a test compiles a script that asserts
/// the struct's size, because a member added on one side and forgotten on the
/// other is otherwise a silent call through the wrong slot.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ScriptHostTable
{
    public uint Abi;
    public uint Size;
    public nint World;
    public nint Log;
    public nint ComponentRegister;
    public nint ComponentLookup;
    public nint EntityCreate;
    public nint EntityDestroy;
    public nint EntityAlive;
    public nint EntityCount;
    public nint EntityAdd;
    public nint EntityRemove;
    public nint EntityHas;
    public nint EntityGet;
    public nint QueryCreate;
    public nint QueryDestroy;
    public nint QueryChunkCount;
    public nint QueryChunkLength;
    public nint QueryChunkColumn;
    public nint QueryChunkEntities;
    public nint TransformRegister;
    public nint DataNumber;
    public nint DataToggle;
    public nint DataText;
    public nint NumberAt;
    public nint ToggleAt;
    public nint TextAt;
}
