using LostCompound.Items;
using LostCompound.Items.Containers;
using LostCompound.Save;

using LostCompound.Tools.Players;

namespace LostCompound.Tools
{
    /// <summary>
    /// 设计文档第十章「进度解锁」——按**累计通关次数**给新开局加东西。
    ///
    /// 为什么读的是 <see cref="Profile.Clears"/> 而不是存档：通关次数是**跨对局**的进度，
    /// 而 save.json 在通关 / 死亡时都会被删。档案（profile.json）才是它的家。
    ///
    /// 这是一张**纯查表**：次数到了就加，没到就不加，不记账、不需要「解锁状态」字段 ——
    /// 因为解锁条件本身就是单调的（通关次数只增不减），
    /// 每次都从 Clears 重新算一遍，永远不会出现「忘了解锁 / 重复解锁」。
    ///
    /// 已实现的条目（DESIGN 第十章）：
    ///   第 2 次通关：背包容量 +3（15→18）、自带 2 个玻璃罐
    ///   第 4 次通关：背包容量 +4（18→22）、自带 1 个玻璃罐
    ///   第 5 次通关：解锁配方 1
    ///   第 9 次通关：初始 DEF +10
    ///   第 10 次通关：初始生命值 +20
    ///
    /// 还没实现的条目（依赖不存在的内容，见各常量注释）：
    ///   · 第 1/3/7 次通关「可自选下一层地图」—— 换层时选环境的界面还没做；
    ///   · 第 2 次通关「初始堆量 3→5」—— 文档没写清这里的「堆量」指什么，待裁定；
    ///   · 第 8 次通关「解锁配方 2」、fatal 通关「配方 3」—— 配方 2/3 的道具都还不存在；
    ///   · 「打过 Hyberty」→ train 战斗 —— Hyberty 还没实现。
    /// </summary>
    public static class ProgressUnlocks
    {
        /// <summary>第几次通关解锁「背包 +3」。</summary>
        public const int InventoryBonus1Clears = 2;

        /// <summary>第几次通关解锁「背包 +4」。</summary>
        public const int InventoryBonus2Clears = 4;

        /// <summary>第几次通关解锁配方 1（DESIGN 第十章：第 5 次）。</summary>
        public const int Tier1RecipesClears = 5;

        /// <summary>第几次通关解锁「初始 DEF +10」。</summary>
        public const int BaseDefBonusClears = 9;

        /// <summary>第几次通关解锁「初始生命值 +20」。</summary>
        public const int BaseHpBonusClears = 10;

        /// <summary>
        /// 「可自选下一层地图」的解锁档位（DESIGN 第十章：第 1 / 3 / 5 / 7 次通关各 +1）。
        ///
        /// 用户裁定（方案 A）的口径：**解锁几次，一局里就有几层可以自选**，
        /// 从第 2 层开始数（第 1 层永远是开局选的那张图）。例如：
        ///   · 通关 1 次 → 1 层可自选 → 第 **2** 层能选；
        ///   · 通关 3 次 → 2 层可自选 → 第 **2、3** 层都能选（用户原话「第三层也能自选」）；
        ///   · 通关 5 次 → 3 层 → 第 2、3、4 层；
        ///   · 通关 7 次 → 4 层 → 第 2、3、4、5 层；再往后的层数恢复随机。
        ///
        /// 为什么用「层号 ≤ 解锁层数 + 1」判定而不是记一个「剩余自选次数」：
        ///   后者是**一局内的状态**，要进存档（否则读档后次数会被重置或多给）；
        ///   而「第几层该能不能选」完全由 (层号, 累计通关次数) 决定 —— 是纯函数，
        ///   不需要存任何东西，也不会出现读档前后不一致。
        /// </summary>
        public const int SelfSelectMilestone1 = 1;
        public const int SelfSelectMilestone2 = 3;
        public const int SelfSelectMilestone3 = 5;
        public const int SelfSelectMilestone4 = 7;

        /// <summary>一局里最多有几层可以自选（0 = 全都随机）。</summary>
        public static int SelfSelectLayerCount
        {
            get
            {
                int clears = Profile.Clears;
                int count = 0;
                if (clears >= SelfSelectMilestone1) count++;
                if (clears >= SelfSelectMilestone2) count++;
                if (clears >= SelfSelectMilestone3) count++;
                if (clears >= SelfSelectMilestone4) count++;
                return count;
            }
        }

        /// <summary>
        /// 第 <paramref name="layer"/> 层是否允许自选环境。
        /// 第 1 层不算（那是开局在主菜单选的），所以门槛是「层号 ≤ 可自选层数 + 1」。
        /// </summary>
        public static bool CanSelfSelectLayer(int layer)
        {
            return layer >= GameProgress.FirstLayer + 1 && layer <= SelfSelectLayerCount + 1;
        }

        /// <summary>第 2 次通关自带的玻璃罐数。</summary>
        public const int GlassJarClears2 = 2;

        /// <summary>第 4 次通关再自带的玻璃罐数。</summary>
        public const int GlassJarClears4 = 1;

        /// <summary>开局道具选择的**基础轮数**（DESIGN §2.2：从 5 个候选里选 1 个，共 3 轮）。</summary>
        public const int BaseStartItemRounds = 3;

        /// <summary>
        /// 开局道具选择**实际轮数** = 基础 3 轮 + 已解锁的「自选轮数 +1」。
        ///
        /// 来源（DESIGN 第十章，用户订正）：第 2 次通关「自选轮数 +1」、第 4 次通关再「+1」，
        /// 于是最多 5 轮。（原先我把它误读成「初始堆量 3→5」，是错的 —— 没有「堆量」这回事。）
        /// </summary>
        public static int StartItemRounds
        {
            get
            {
                int clears = Profile.Clears;
                int rounds = BaseStartItemRounds;
                if (clears >= InventoryBonus1Clears)
                {
                    rounds++;
                }
                if (clears >= InventoryBonus2Clears)
                {
                    rounds++;
                }
                return rounds;
            }
        }

        /// <summary>初始 DEF 加成量。</summary>
        public const double BaseDefBonus = 10;

        /// <summary>初始生命加成量。</summary>
        public const double BaseHpBonus = 20;

        /// <summary>
        /// 给**新开局**的玩家套用所有已达成的解锁。读档不走这里 ——
        /// 那些加成已经体现在存档里的 BaseXxx / InventoryCapacity 上，再加一次就重复了。
        ///
        /// 顺序有讲究：**先加容量，再塞道具**（否则玻璃罐可能因为背包满了而进不去），
        /// 最后才是属性与配方。
        /// </summary>
        public static void ApplyToNewRun(Player p)
        {
            int clears = Profile.Clears;

            if (clears >= InventoryBonus1Clears)
            {
                p.GrowInventoryCapacity(3);       // 15 → 18
            }
            if (clears >= InventoryBonus2Clears)
            {
                p.GrowInventoryCapacity(4);       // 18 → 22（= MaxInventoryCapacity）
            }
            if (clears >= InventoryBonus1Clears)
            {
                p.AddItem(new GlassJar(GlassJarClears2));
            }
            if (clears >= InventoryBonus2Clears)
            {
                p.AddItem(new GlassJar(GlassJarClears4));
            }
            if (clears >= BaseDefBonusClears)
            {
                p.GrowBaseDef(BaseDefBonus);
            }
            if (clears >= BaseHpBonusClears)
            {
                p.GrowBaseHp(BaseHpBonus);
            }
            if (clears >= Tier1RecipesClears)
            {
                // AddDefaultRecipes 由开局流程负责（RunMainLoop 的 unlockRecipes）；
                // 这里只补配方 1。它自带幂等守卫，重复调用不会重复添加。
                p.AddTier1Recipes();
            }
        }
    }
}
