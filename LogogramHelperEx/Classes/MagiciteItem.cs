using System.Collections.Generic;
using Dalamud.Utility;
using ECommons.DalamudServices;
using Lumina.Excel.GeneratedSheets;

namespace LogogramHelperEx.Classes
{
    // 鉴定出来的文理碎晶
    public class MagiciteItem
    {
        public static Dictionary<uint, (int Index, string Name)> Load()
        {
            Dictionary<uint, (int Index, string Name)> Items = [];
            foreach (var row in Svc.Data.GetExcelSheet<EurekaMagiciteItem>()!)
            {
                var item = row.Item.Value;
                if (item != null && item.RowId != 0)
                {
                    Items[item.RowId] = ((int)row.RowId, item.Name.ToDalamudString().TextValue.Replace("文理", string.Empty).Replace("的记忆", string.Empty).Replace("的加护", string.Empty));
                }
            }
            return Items;
        }
    }
}
