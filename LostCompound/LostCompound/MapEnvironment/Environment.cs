using LostCompound.Items;
using LostCompound.Tools;

namespace LostCompound.MapEnvironment
{
    /// <summary>
    /// 地图环境基类。每种环境一个子类，由 <see cref="EnvironmentFactory"/> 按 <see cref="EnvType"/> 创建。
    ///
    /// 职责分工（按设计文档）：
    ///   环境对象   —— 持有本层环境状态（如水量），负责每回合推进、给战斗房间掉落；
    ///   EmptyRoom  —— 负责空房事件的判定与执行（事件内容写在 EmptyRoom 里）。
    ///
    /// 设计取向（已裁定为「方案 A」）：
    ///   环境**直接改玩家**，而不是「只暴露一堆修正量、由玩家自己来读」。
    ///
    ///   为什么改成直接改：早先的写法是基类上一排虚属性：
    ///       public virtual double StrModifier => 0.0;
    ///       public virtual double AtkModifier => 0.0;   // 再来第 3、第 4 个……
    ///   每加一种「环境能影响的东西」，基类就长一截，而绝大多数环境只关心其中一两项。
    ///   现在改成：基类只认「回合」和「玩家」两个概念，具体影响什么由子类自己决定 ——
    ///   加第 10 个环境、第 20 种效果，本文件一行都不用动。
    ///
    ///   ⚠️ 代价与纪律：既然子类能直接写 Player 的字段，就必须自己保证「可恢复」。
    ///   典型的错误写法是 p.Str -= 0.075（每回合累加）——那样惩罚会与「打了多少回合」
    ///   挂钩，而且水回满了也恢复不了。正确做法是**每次从基础值重算**：
    ///       p.Str = Player.BaseStr - 惩罚;
    ///   这样水回满时惩罚自然归零，不需要谁去「加回来」。
    /// </summary>
    public abstract class Environment
    {
        public EnvType Type { get; }

        protected Environment(EnvType type) => Type = type;

        /// <summary>
        /// 战斗中的每个回合调用一次：推进环境状态（如水量下降、水位上涨）。
        ///
        /// 注意这里**不接收 Player**：环境只负责推进自己，对玩家的影响通过
        /// <see cref="StrModifier"/> 这类「修正量」暴露出去，由 Player 重算时统一读取。
        ///
        /// 为什么不让环境直接改玩家（曾经这么做过，已改回）：
        ///   改 STR 的来源不止一个 —— 环境、buff、将来的装备都要参与。
        ///   如果各自都往 p.Str 上写就会互相覆盖；写成 += 又会累加、没人负责恢复。
        ///   统一成「大家都只提供修正量，由 Player 一处重算」，才不会打架。
        /// </summary>
        public abstract void OnBattleTurn();

        /// <summary>
        /// 对玩家 STR 的修正量（负值表示削弱，如熔岩瀑布缺水）。
        /// 默认不修正。Player 重算 STR 时会读它，环境本身不接触 Player。
        /// </summary>
        public virtual double StrModifier => 0.0;

        /// <summary>战斗房间通关后的额外掉落。默认无。</summary>
        public virtual List<Item> GetBattleLoot() => [];

        /// <summary>事件用：扣除环境资源（水量）。只有具备水量的环境才有意义，默认什么也不做。</summary>
        public virtual void DrainWater(int amount) { }

        /// <summary>
        /// 本环境是否可开采矿物（镐子的「采掘」只在可开采环境里生效）。
        /// 默认 false，可开采的环境（如 <see cref="FloodedCavern"/>）覆写为 true。
        ///
        /// 为什么放在基类、而不是在 Tool.Pickaxe 里判断具体类型：
        ///   将来矿洞（5.4）的矿道、放射性区域（5.6）的指定房间做出来时，
        ///   只要各自覆写这个属性，开采入口就自动接上 —— 不用回头改 Mining / Pickaxe。
        /// </summary>
        public virtual bool CanMine => false;

        /// <summary>
        /// 本环境是否已到「玩家必败」的条件（如水淹洞穴水位满）。
        /// 由环境自己判定，战斗循环只问结果 —— 这样以后再加有死亡条件的环境
        /// （如熔岩瀑布被岩浆淹没）不用回头改 RunBattle。
        ///
        /// 为什么放在基类、而不是在 Tools.RunBattle 里判断具体类型：
        ///   写死的写法只有在一个环境有「判负条件」时看不出问题，
        ///   第二个这样的环境（岩浆淹上来、毒气充满……）一出现，
        ///   战斗循环里就会多一个 is，而且每加一种失败条件就得再改一次通用循环。
        ///   现在循环只认「这个环境是不是已经必败了」这一个问题。
        /// </summary>
        public virtual bool IsFatalToPlayer => false;

        /// <summary>判负时打印的那句话。默认给一句通用的，特殊环境可覆写。</summary>
        public virtual string FatalMessage => "环境已然失控——你失去了意识……";

        /// <summary>拼进玩家状态栏的一行环境状态。</summary>
        public abstract string DescribeStatus();
    }
}
