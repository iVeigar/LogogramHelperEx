using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Utility;
using ECommons.Configuration;

namespace LogogramHelperEx;

public class ActionRecipe
{
    public int ActionIndex = 0;
    public int RecipeIndex = 0;
}

public class ActionSet
{
    public ActionRecipe ActionRecipe1 = new();
    public ActionRecipe ActionRecipe2 = new();
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

public class Configuration : IEzConfig, IPluginConfiguration
{
    public void Save() => EzConfig.Save();
    public int Version { get; set; } = 1;
    public List<ActionSetGroup> Groups { get; set; } = [];
    public bool AutoSynthesis { get; set; } = false;
}
