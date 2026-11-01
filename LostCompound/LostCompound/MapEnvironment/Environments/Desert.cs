using LostCompound.Items;
using LostCompound.Items.Materials;

namespace LostCompound.MapEnvironment.Environments
{
    /// <summary>
    /// 沙漠（DESIGN §5.3，地图 3）。
    ///
    /// 文档原文三条：
    ///   1. **无法获得除沙子以外任何道具**；
    ///   2. **怪物不掉落物品**；
    ///   3. **采沙**：花费 1 回合获得 5（困难 3）沙子。
    ///
    /// 三条怎么落地：
    ///   1 → `LootTables.GetLootTable(Desert)` / `GetStartPool(Desert)` 只返回沙子，
    ///      于是宝箱房、空房事件、开局道具池**全都**只剩沙子（用户裁定：所有来源都只给沙子）。
    ///   2 → <see cref="SuppressMonsterLoot"/> 返回 true，<see cref="Monsters.Monster.Die"/>
    ///      据此跳过掉落表。**不写死 `env is Desert`**：判定方只问「这个环境压不压掉落」。
    ///   3 → <see cref="CanDigSand"/> + <see cref="DigSand"/>；入口是战斗菜单第 6 项
    ///      （用户裁定：只能战斗中采）。一次采沙就是一个回合。
    ///
    /// ⚠️ 困难模式的数字（采沙 5→3、骆驼穿模 20%→30%）都还没做，等难度系统。
    /// </summary>
    public sealed class Desert : Environment
    {
        /// <summary>一次采沙得到的沙子数量（文档：5；困难 3）。</summary>
        public const int SandPerDig = 5;

        public Desert() : base(EnvType.Desert)
        {
        }

        /// <summary>沙漠没有每回合的环境推进（水位 / 失水之类都没有），也不理会高温房间。</summary>
        public override void OnBattleTurn(bool hotRoom)
        {
        }

        /// <summary>能不能在战斗里采沙。</summary>
        public override bool CanDigSand => true;

        /// <summary>采一次沙：返回要发到背包里的道具（沙子 × 5）。</summary>
        public override List<Item> DigSand() => [new Sand(SandPerDig)];

        /// <summary>沙漠不给战斗额外掉落（怪物本身也不掉，见 <see cref="SuppressMonsterLoot"/>）。</summary>
        public override List<Item> GetBattleLoot() => [];

        /// <summary>怪物掉落一律压掉（DESIGN §5.3 第 2 条）。</summary>
        public override bool SuppressMonsterLoot => true;

        public override string DescribeStatus()
        {
            return "沙漠：风把沙吹得老高（只能采沙、怪物不掉落）";
        }
    }
}
