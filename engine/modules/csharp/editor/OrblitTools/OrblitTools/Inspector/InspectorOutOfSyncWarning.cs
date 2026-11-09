using Orblit;
using OrblitTools.Internals;

namespace OrblitTools.Inspector
{
    public partial class InspectorOutOfSyncWarning : HBoxContainer
    {
        public override void OnStart()
        {
            SetAnchorsPreset(LayoutPreset.TopWide);

            var iconTexture = GetThemeIcon("StatusWarning", "EditorIcons");

            var icon = new TextureRect()
            {
                Texture = iconTexture,
                ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
                CustomMinimumSize = iconTexture.GetSize(),
            };

            icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;

            var label = new Label()
            {
                Text = "This inspector might be out of date. Please build the C# project.".TTR(),
                AutowrapMode = TextSystem.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(100f, 0f),
            };

            label.AddThemeColorOverride("font_color", GetThemeColor("warning_color", "Editor"));
            label.SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;

            AddChild(icon);
            AddChild(label);
        }
    }
}
