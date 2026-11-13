using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// What the script panels need from the script: what it declares, and one call for each edit. The engine holds
/// the rules and renames every step that names a thing it renames. An edit it refuses comes back with its
/// reason and changes nothing. The panels own this interface.
/// </summary>
internal interface IScriptDeclarations
{
    GraphDocument Document { get; }

    /// <summary>The function on show. Null when the script has no function.</summary>
    GraphFunction? Function { get; }

    /// <summary>Raised after an edit the engine took.</summary>
    event Action? Changed;

    /// <summary>Shows another function. It is not an edit, so <see cref="Changed"/> does not fire.</summary>
    void OpenFunction(string name);

    GraphEditResult AddVariable(string name, int type);

    GraphEditResult RemoveVariable(string name);

    GraphEditResult RenameVariable(string name, string newName);

    GraphEditResult SetVariableType(string name, int type);

    /// <summary>Sets the default from Variant text, such as <c>2.5</c>.</summary>
    GraphEditResult SetVariableDefault(string name, string valueText);

    GraphEditResult SetVariableExported(string name, bool exported);

    GraphEditResult SetVariableInfo(string name, string category, string description);

    GraphEditResult AddSignal(string name);

    GraphEditResult RemoveSignal(string name);

    GraphEditResult RenameSignal(string name, string newName);

    GraphEditResult SetSignalArguments(string name, IReadOnlyList<GraphTyped> arguments);

    /// <summary>Adds a function with an Entry step, which holds its arguments and locals.</summary>
    GraphEditResult AddFunction(string name);

    /// <summary>The virtual functions of the script's base type, each as an entry whose parameters are the signature to copy.</summary>
    IReadOnlyList<GraphStepEntry> FindOverrides();

    /// <summary>Adds a function named after the entry, with the entry's signature. One edit: it all happens or none of it.</summary>
    GraphEditResult AddOverride(GraphStepEntry entry);

    GraphEditResult RemoveFunction(string name);

    GraphEditResult RenameFunction(string name, string newName);

    /// <summary>Sets a parameter of a step of the function on show, from Variant text.</summary>
    GraphEditResult SetStepParameter(int id, string parameter, string valueText);
}
