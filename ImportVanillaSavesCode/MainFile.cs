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
        var assembly = Assembly.GetExecutingAssembly();

        Godot.Bridge.ScriptManagerBridge.LookupScriptsInAssembly(assembly);

        new Harmony(ModId).PatchAll(assembly);

        if (Engine.GetMainLoop() is SceneTree tree)
        {
            tree.NodeAdded += OnNodeAdded;
        }
    }

    private static PackedScene ImportButtonScene =>
        PreloadManager.Cache.GetScene("res://ImportVanillaSaves/import_button.tscn");

    private static PackedScene ExportButtonScene =>
        PreloadManager.Cache.GetScene("res://ImportVanillaSaves/export_button.tscn");

    private static readonly StringName profileScreenName = new("ProfileScreen");

    private static void OnNodeAdded(Node node)
    {
        if (node is not Control profileScreen || profileScreen.Name != profileScreenName)
            return;

        profileScreen.OnReady(() =>
        {
            for (int i = 1; i <= 3; i++)
            {
                var deleteProfileButton = profileScreen.GetNode<Control>($"DeleteProfileButton{i}");
                int index = deleteProfileButton.GetIndex();

                profileScreen.RemoveChild(deleteProfileButton);

                var importButton = ImportButtonScene.Instantiate<ImportVanillaSaveButton>();
                importButton.Initialize(i);

                var exportButton = ExportButtonScene.Instantiate<ExportModdedSaveButton>();
                exportButton.Initialize(i);

                var container = new HBoxContainer() { Position = deleteProfileButton.Position };
                container.AddChild(deleteProfileButton);
                container.AddChild(importButton);
                container.AddChild(exportButton);

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
