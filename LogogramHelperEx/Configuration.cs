using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Utility;
using ECommons.DalamudServices;

namespace LogogramHelperEx;

public class ActionSet
{
    public (int ActionIndex, int RecipeIndex) MagiaAction1 = (0, 0);
    public (int ActionIndex, int RecipeIndex) MagiaAction2 = (0, 0);
}

public class ActionSetGroup
{
    public string Name = "New";
    public List<ActionSet> Sets = [];
    public ActionSetGroup() { }
    public ActionSetGroup(string name)
    {
        if (!name.IsNullOrEmpty()) Name = name.Trim();
    }
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    public void Save() => Svc.PluginInterface!.SavePluginConfig(this);

    public int Version { get; set; } = 1;
    public List<ActionSetGroup> Groups { get; set; } = [];
    public bool AutoSynthesis { get; set; } = false;
}
