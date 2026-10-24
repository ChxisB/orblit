namespace Orblit.Input;

/// <summary>A control a rebinding asked for is already bound in that context.</summary>
public sealed class BindingClashException(string action, Control control)
    : Exception($"{control} is already bound to \"{action}\".")
{
    /// <summary>The action that has <see cref="Control"/>.</summary>
    public string Action { get; } = action;

    public Control Control { get; } = control;
}
