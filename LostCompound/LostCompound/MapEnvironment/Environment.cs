using LostCompound.Items;

namespace LostCompound.MapEnvironment
{
    /// <summary>
    /// 地图环境基类。每种环境一个子类，由 <see cref="EnvironmentFactory"/> 按 <see cref="EnvType"/> 创建。
    ///
    /// 职责分工（按设计文档）：
    ///   环境对象   —— 持有本层环境状态（如水量），负责每回合推进、给战斗房间掉落；
    ///   EmptyRoom  —— 负责空房事件的判定与执行（事件内容写在 EmptyRoom 里）。
    /// </summary>
    public abstract class Environment
    {
        public EnvType Type { get; }

        protected Environment(EnvType type) => Type = type;

        /// <summary>战斗中的每个回合调用一次，推进环境状态（如水量下降）。</summary>
        public abstract void OnBattleTurn();

        /// <summary>
        /// 对玩家有效 STR 的修正量。动态计算，不修改 Player.Str 本身。
        /// 默认不修正，由有需求的环境（如熔岩瀑布）重写。
        /// </summary>
        public virtual double StrModifier => 0.0;

        /// <summary>战斗房间通关后的额外掉落。默认无。</summary>
        public virtual List<Item> GetBattleLoot() => [];

        /// <summary>事件用：扣除环境资源（水量）。只有具备水量的环境才有意义，默认什么也不做。</summary>
        public virtual void DrainWater(int amount) { }

        /// <summary>拼进玩家状态栏的一行环境状态。</summary>
        public abstract string DescribeStatus();
    }
}
