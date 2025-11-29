using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Callback = ECommons.Automation.Callback;
namespace LogogramHelperEx.UIHelpers.AddonMasterImplementations;
public partial class AddonMaster
{
    public unsafe class EurekaMagiciteItemSynthesis : AddonMasterBase<AtkUnitBase>
    {
        public override string AddonDescription { get; } = "Logos Manipulator window";
        public EurekaMagiciteItemSynthesis(nint addon) : base(addon) { }
        public EurekaMagiciteItemSynthesis(void* addon) : base(addon) { }
        public AtkComponentButton* ExtractButton => Addon->GetComponentButtonById(14);
        public void Extract() => ClickButtonIfEnabled(ExtractButton);

        public uint[] Mnemes
        {
            get
            {
                var ret = new uint[6];
                for (var i = 0; i < ret.Length; i++)
                    ret[i] = Addon->AtkValues[15 + i].UInt;
                return ret;
            }
        }
        /// <summary>
        /// 从文理融合器中取回一个文理碎晶
        /// </summary>
        /// <param name="index">文理碎晶在融合器里的位置, 0-2 in 星极融合器, 3-5 in 灵极融合器, 从上到下</param>
        public void RetrieveMneme(int index)
            => Callback.Fire(Addon, true, 18, index);

        /// <summary>
        /// 从文理融合器中清空所有文理碎晶
        /// </summary>
        public void ClearArrays()
        {
            for (var i = 5; i >= 0; i--)
                RetrieveMneme(i);
        }

        /// <summary>
        /// 把一个文理碎晶放入文理融合器。
        /// 警告: 文理碎晶持有数量为0也能放进去，但最终合成会出问题
        /// </summary>
        /// <param name="array">Array id, umbral array (1) or astral array (0).</param>
        /// <param name="item">Magicite item id (1-28).</param>
        public void PutMneme(int array, int mneme)
            => Callback.Fire(Addon, true, 22, (array << 16) + mneme);
        public void PutMnemeIntoAstralArray(int mneme)
            => PutMneme(0, mneme);
        public void PutMnemeIntoUmbralArray(int mneme)
            => PutMneme(1, mneme);

        /// <summary>
        /// 检查两个融合器是否为空。
        /// </summary>
        /// <returns>
        /// 第一个值为灵极融合器是否为空，第二个值为星极融合器是否为空
        /// </returns>
        public (bool, bool) AreArraysEmpty()
        {
            var mnemes = Mnemes;
            return (mnemes[3] == 0, mnemes[0] == 0);
        }
    }
}

