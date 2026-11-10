using Orblit;
using System;

[Tool]
public partial class ExportedToolButtons : OrblitObject
{
    [ExportToolButton("Click me!")]
    public Callable MyButton1 => Callable.From(() => { Orb.Print("Clicked MyButton1!"); });

    [ExportToolButton("Click me!", Icon = "ColorRect")]
    public Callable MyButton2 => Callable.From(() => { Orb.Print("Clicked MyButton2!"); });
}
