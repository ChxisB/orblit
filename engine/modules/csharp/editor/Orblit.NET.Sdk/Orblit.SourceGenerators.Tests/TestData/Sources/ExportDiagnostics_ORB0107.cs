using Orblit;
using Orblit.Collections;

public partial class ExportDiagnostics_ORB0107_OK : Entity
{
    [Export]
    public Entity NodeField;

    [Export]
    public Entity[] SystemArrayOfNodesField;

    [Export]
    public Array<Entity> OrblitArrayOfNodesField;

    [Export]
    public Dictionary<Entity, string> OrblitDictionaryWithNodeAsKeyField;

    [Export]
    public Dictionary<string, Entity> OrblitDictionaryWithNodeAsValueField;

    [Export]
    public Entity NodeProperty { get; set; }

    [Export]
    public Entity[] SystemArrayOfNodesProperty { get; set; }

    [Export]
    public Array<Entity> OrblitArrayOfNodesProperty { get; set; }

    [Export]
    public Dictionary<Entity, string> OrblitDictionaryWithNodeAsKeyProperty { get; set; }

    [Export]
    public Dictionary<string, Entity> OrblitDictionaryWithNodeAsValueProperty { get; set; }
}

public partial class ExportDiagnostics_ORB0107_KO : Resource
{
    [Export]
    public Entity {|ORB0107:NodeField|};

    [Export]
    public Entity[] {|ORB0107:SystemArrayOfNodesField|};

    [Export]
    public Array<Entity> {|ORB0107:OrblitArrayOfNodesField|};

    [Export]
    public Dictionary<Entity, string> {|ORB0107:OrblitDictionaryWithNodeAsKeyField|};

    [Export]
    public Dictionary<string, Entity> {|ORB0107:OrblitDictionaryWithNodeAsValueField|};

    [Export]
    public Entity {|ORB0107:NodeProperty|} { get; set; }

    [Export]
    public Entity[] {|ORB0107:SystemArrayOfNodesProperty|} { get; set; }

    [Export]
    public Array<Entity> {|ORB0107:OrblitArrayOfNodesProperty|} { get; set; }

    [Export]
    public Dictionary<Entity, string> {|ORB0107:OrblitDictionaryWithNodeAsKeyProperty|} { get; set; }

    [Export]
    public Dictionary<string, Entity> {|ORB0107:OrblitDictionaryWithNodeAsValueProperty|} { get; set; }
}
