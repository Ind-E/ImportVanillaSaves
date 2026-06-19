using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Modding;

namespace ImportVanillaSaves.ImportVanillaSavesCode;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "ImportVanillaSaves";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        Godot.Bridge.ScriptManagerBridge.LookupScriptsInAssembly(Assembly.GetExecutingAssembly());

        new Harmony(ModId).PatchAll();

        if (Engine.GetMainLoop() is SceneTree tree)
        {
            tree.NodeAdded += OnNodeAdded;
        }
    }

    private static readonly PackedScene ImportButtonScene = PreloadManager.Cache.GetScene(
        "res://ImportVanillaSaves/import_button.tscn"
    );

    private static void OnNodeAdded(Node node)
    {
        if (node is not Control control)
            return;

        if (control.Name != "ProfileScreen")
            return;

        control.OnReady(() =>
        {
            var profileScreen = control;

            for (int i = 1; i <= 3; i++)
            {
                var button = profileScreen.GetNode<Control>($"DeleteProfileButton{i}");
                int index = button.GetIndex();

                profileScreen.RemoveChild(button);

                var importButton = ImportButtonScene.Instantiate<ImportSaveButton>();
                importButton.Initialize(i);

                var container = new HBoxContainer() { Position = button.Position };
                container.AddChild(button);
                container.AddChild(importButton);

                profileScreen.AddChild(container);
                profileScreen.MoveChild(container, index);
            }
        });
    }
}

public static class NodeExtensions
{
    public static void OnReady(this Node node, Action action)
    {
        if (node.IsNodeReady())
            action();
        else
            node.Ready += action;
    }
}
