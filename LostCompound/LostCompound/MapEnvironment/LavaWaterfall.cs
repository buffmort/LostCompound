using LostCompound.Items;
using LostCompound.Monsters;   // GetDamageImmunity(Monster m)
using LostCompound.Tools;   // Player：环境直接改玩家（见 Environment 类注释的「方案 A」）

namespace LostCompound.MapEnvironment
{
    /// <summary>
    /// 熔岩瀑布（设计文档 5.1）——固定第一层。
    ///
    /// 水量：满 200，战斗中每回合降 5-10（普通模式；困难模式的 15-20 等困难模式做出来再接）。
    /// 影响：水量越低玩家越弱 —— 每损失 1 点水，有效 STR -0.002（动态计算，不改 Player.Str）。
    /// 水到 0 不死亡，只是 STR 被扣到底。
    /// 掉落：每个战斗房间必掉硫磺，数量 2-5 随机。
    /// </summary>
    public sealed class LavaWaterfall : Environment 
    {
        /// <summary>满水量。</summary>
        public const int MaxWater = 200;

        /// <summary>普通模式每回合掉水量的下限与上限（含）。</summary>
        private const int MinWaterDrop = 5;
        private const int MaxWaterDrop = 10;

        /// <summary>
        /// 每损失 1 点水，STR 的修正量（设计文档 5.1：每降 1 点 STR 降 0.01）。
        ///
        /// 历史：这个系数一度被调成 0.002 / 0.005，因为当时玩家 STR 只有 2
        /// （偏离文档），枯竭时 -2.0 会把 STR 扣光，攻击力从 9.75 掉到 2.00。
        /// 现在 STR 已按文档改回 3，0.01 就是合理的：
        ///   满水攻击力 = 2 + 3 × √20 = 15.42
        ///   枯竭攻击力 = 2 + 1 × √20 =  6.47（-58%，有压力但不至于打不动）
        /// 所以恢复成文档原值。
        /// </summary>
        private const double StrPenaltyPerWater = 0.01;

        /// <summary>当前水量。</summary>
        public int Water { get; private set; } = MaxWater;

        public LavaWaterfall() : base(EnvType.LavaWaterfall) { }

        public override void OnBattleTurn()
        {
            // Random.Shared.Next(min, max) 是左闭右开区间，所以要 +1 才包含上限
            int drop = Random.Shared.Next(MinWaterDrop, MaxWaterDrop + 1);
            Water -= drop;
            if (Water < 0) Water = 0;
            // 这里不再直接改玩家：STR 的削弱由 StrModifier 暴露出去，
            // 玩家每次重算 STR 时自己来读（见 Player.RecomputeStr）。
        }

        /// <summary>
        /// 水量越低玩家越弱：每损失 1 点水，STR -0.01（满水到枯竭最多 -2.0）。
        ///
        /// 只返回修正量、不接触 Player —— 这样它和 buff 的修正、将来装备的加成
        /// 能在 Player 那一处统一相加，不会互相覆盖。
        /// </summary>
        public override double StrModifier => -(MaxWater - Water) * StrPenaltyPerWater;

        /// <summary>
        /// 熔岩瀑布是高温环境 → 木头在这里每回合被烤掉 1 点倒计时，烧完变木炭。
        /// 覆写基类的 false：能力位由环境自己声明（原先写在 Wood 里比 EnvType）。
        /// </summary>
        public override bool BurnsWood => true;

        /// <summary>
        /// 熔岩瀑布有水源（满 200），所以水瓶在这里生效。
        /// 覆写基类的 false：这是本属性存在的意义 —— 环境自己声明「我能回水」。
        /// </summary>
        public override bool CanRefillWater => true;

        /// <summary>
        /// 回满水量（水瓶使用）。
        /// 不需要再管 STR：StrModifier 是按当前水量现算的，
        /// 水一满惩罚自然归零，玩家下次重算 STR 时就恢复 —— 这就是「现算」的好处。
        /// </summary>
        public override void RefillWater()
        {
            Water = MaxWater;
        }

        public override List<Item> GetBattleLoot()
        {
            // 每战斗房间必掉硫磺，数量 2-5 随机
            int count = Random.Shared.Next(2, 6);
            return [new Sulphur(count)];
        }

        public override void DrainWater(int amount)
        {
            Water -= amount;
            if (Water < 0) Water = 0;
        }

        /// <summary>
        /// 「环境 × 怪物」规则：熔岩瀑布是高温环境 → 岩浆怪免疫 30% 高温伤害（设计文档六）。
        /// 规则内容（哪个怪、免疫多少）只有本环境知道，所以判定写在这里；
        /// 战斗循环只把返回值写进怪物（见 Tools.ApplyEnvironmentMonsterTraits）。
        /// 别的怪一律 0 —— 与改造前「只有岩浆怪被写入 30%」完全一致。
        /// </summary>
        public override double GetDamageImmunity(Monster m)
            => m is LavaSlime ? LavaSlime.HighTempImmunityInLavaWaterfall : 0.0;

        public override string DescribeStatus()
        {
            string s = $"熔岩瀑布水量：{Water}/{MaxWater}";
            if (Water == 0)
                s += "\n水已枯竭！你的力量大幅下降";
            else if (Water < MaxWater / 4)
                s += "\n水源告急，力量正在衰退";
            return s;
        }
    }
}
