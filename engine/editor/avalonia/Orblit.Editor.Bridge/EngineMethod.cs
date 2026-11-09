namespace Orblit.Editor.Bridge;

/// <summary>
/// One engine method, named by class, method and signature hash. The engine looks it up the first
/// time it is called and the answer stays valid for the life of the process.
/// </summary>
internal sealed unsafe class EngineMethod(string type, string name, long hash)
{
    nint _bind;

    internal nint Bind()
    {
        if (_bind != 0)
        {
            return _bind;
        }
        using var scope = new NativeScope();
        var found = NativeApi.GetMethodBind(scope.StringName(type), scope.StringName(name), hash);
        if (found == null)
        {
            throw new InvalidOperationException($"The engine has no method {type}.{name} with signature hash {hash}. Check its entry in EngineCalls.g.cs.");
        }
        return _bind = (nint)found;
    }
}
