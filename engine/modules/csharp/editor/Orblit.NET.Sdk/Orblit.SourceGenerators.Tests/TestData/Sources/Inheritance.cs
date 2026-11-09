using System;
using Orblit;

public partial class InheritanceBase : Entity
{
    public virtual string MyString { get; set; }
    public virtual int MyInteger { get; set; }
}

public partial class InheritanceChild : InheritanceBase
{
    public override string MyString { get; set; }
    [Export]
    public override int MyInteger => 0;
}
