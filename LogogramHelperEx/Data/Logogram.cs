using System.Collections.Generic;

namespace LogogramHelperEx.Classes;

// 未鉴定的文理碎晶
internal class Logogram
{
    // key: Logogram id
    // value: MagiciteItem ids
    public static Dictionary<uint, List<uint>> Load()
    {
        return new()
        {
            { 24007, [24015, 24016, 24017, 24024, 24030, 24033, 24037] },
            { 24008, [24022, 24028, 24031, 24036, 24038] },
            { 24009, [24019, 24034] },
            { 24010, [24020, 24032] },
            { 24011, [24018, 24021] },
            { 24012, [24025, 24029] },
            { 24013, [24023, 24035] },
            { 24014, [24026, 24027] },
            { 24809, [24810, 24813, 24812, 24811] }
        };
    }
}
