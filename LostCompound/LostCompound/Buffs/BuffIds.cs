namespace LostCompound.Buffs
{
    /// <summary>
    /// Buff 的 ID 常量表。与 <see cref="Items.ItemIds"/> 同一套做法。
    ///
    /// 为什么需要 ID：Buff 现在只能靠「new 一个具体类」来挂，
    /// 没有任何地方能「按编号引用一个 buff」—— 调试发放、将来的存档读档、
    /// 事件按 ID 施加状态，都需要这张表。
    ///
    /// 维护约定：新增 buff **必须**在这里补一个常量、并在 BuffFactory 注册，
    /// 否则它无法被按 ID 挂载。
    ///
    /// 这里没有 ID 分段（不像 ItemIds 按配方层级分）—— buff 目前数量很少，
    /// 且没有「一个 buff 属于哪一层」这种概念，编号从小到大连续排即可。
    /// </summary>
    public static class BuffIds
    {
        /// <summary>流血（BleedBuff）。</summary>
        public const int Bleed = 1;

        /// <summary>超雄综合症（SuperMaleBuff）。</summary>
        public const int SuperMale = 2;

        /// <summary>锐化（Sharpen）。原「力量」，效果已改为 +ATK。**编号 3 不能改**（存档按它还原）。</summary>
        public const int Sharpen = 3;

        // ---- 洞潜僵尸的 4 种 debuff（DESIGN §6，持续回合由Hyberty给定）----

        /// <summary>减压病：-1 STR、-2 ATK，3 回合。</summary>
        public const int DecompressionSickness = 4;

        /// <summary>呼吸性碱中毒：每回合 -2 血，5 回合。</summary>
        public const int RespiratoryAlkalosis = 5;

        /// <summary>搅乱泥沙：这一回合打不开背包（尝试一次即消费）。</summary>
        public const int MuddyWater = 6;

        /// <summary>缺氧：每回合 -0.1 STR，5 回合，到期自动恢复。</summary>
        public const int Hypoxia = 7;

        /// <summary>磷灼烧：每回合 10 伤害、3 回合，期间木乃伊 DEF 归零。</summary>
        public const int PhosphorusBurn = 8;

        /// <summary>易伤：受伤倍率 = 1 + 0.01 × 层数（木乃伊给自己上的）。</summary>
        public const int Vulnerability = 9;

        /// <summary>锈蚀：每层 -1 DEF，最多 5 层（锈铁傀儡给玩家上的）。</summary>
        public const int Rusted = 10;

        /// <summary>
        /// 额外回合：层层叠加，每层换一次「再行动一次」。
        /// 消费点在 <c>Player.TryConsumeExtraTurn</c>（玩家每次行动结束），不在回合头的 BuffTick 里。
        /// </summary>
        public const int ExtraTurn = 11;

        /// <summary>
        /// 非环境辐射（<see cref="NonEnvRadioactive"/>）：身上带辐射源时按**回合**掉血。
        /// 与 EnvRadioactive（按秒）成对，但那个不走 buff 体系（环境自己用秒表）。
        /// </summary>
        public const int NonEnvRadioactive = 12;

        /// <summary>
        /// 辐照（<see cref="RadiationStack"/>）：**纯读数**，没有直接效果。
        /// 放射僵尸按它的层数给自己加攻击（10 + 层数 × 5）。
        /// </summary>
        public const int RadiationStack = 13;
    }
}
