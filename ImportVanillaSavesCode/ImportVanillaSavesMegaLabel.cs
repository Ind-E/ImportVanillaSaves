using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;

namespace ImportVanillaSaves.ImportVanillaSavesCode;

[GlobalClass]
public partial class ImportVanillaSavesMegaLabel : MegaLabel
{
    public static Font LabelFont = PreloadManager.Cache.GetAsset<Font>(
        "res://themes/kreon_bold_glyph_space_one.tres"
    );

    public override void _Ready()
    {
        AddThemeFontOverride("font", LabelFont);
        base._Ready();
    }
}
