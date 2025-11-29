using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace LogogramHelperEx.UIHelpers.AddonMasterImplementations;

public partial class AddonMaster
{
    public unsafe class EurekaMagiciteItemShardList : AddonMasterBase<AtkUnitBase>
    {
        public override string AddonDescription { get; } = "Logograms shards window";
        public EurekaMagiciteItemShardList(nint addon) : base(addon) { }
        public EurekaMagiciteItemShardList(void* addon) : base(addon) { }
        public AtkComponentButton* AllButton => Addon->GetComponentButtonById(3);
        public void All() => ClickButtonIfEnabled(AllButton);
    }
}

