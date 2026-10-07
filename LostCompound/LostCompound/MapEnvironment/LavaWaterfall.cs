using LostCompound.Items;

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
        /// 每损失 1 点水，STR 的修正量。
        /// 注意：玩家基础 STR 只有 2，所以这个系数不能大 ——
        /// 取 0.01 时水枯竭会扣掉整整 2.0 STR（等于扣光 100%），玩家直接失去战斗力
        /// （实测攻击力从 9.75 掉到 2.9，打个僵尸要 10 回合）。
        /// 取 0.002，满水到枯竭的惩罚是 -0.4（约基础 STR 的 20%），强度合理。
        /// </summary>
        private const double StrPenaltyPerWater = 0.002;

        /// <summary>当前水量。</summary>
        public int Water { get; private set; } = MaxWater;

        public LavaWaterfall() : base(EnvType.LavaWaterfall) { }

        public override void OnBattleTurn()
        {
            // Random.Shared.Next(min, max) 是左闭右开区间，所以要 +1 才包含上限
            int drop = Random.Shared.Next(MinWaterDrop, MaxWaterDrop + 1);
            Water -= drop;
            if (Water < 0) Water = 0;
        }

        /// <summary>水量越低玩家越弱：每损失 1 点水，STR -0.002（满水到枯竭最多 -0.4）。</summary>
        public override double StrModifier => -(MaxWater - Water) * StrPenaltyPerWater;

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

        /// <summary>回满水量（水瓶使用）。</summary>
        public void RefillWater() => Water = MaxWater;

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
