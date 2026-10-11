using LostCompound.Items;
using LostCompound.Monsters;   // GetDamageImmunity(Monster)：环境声明「本环境对某只怪的免疫比例」
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

        /// <summary>
        /// 本环境给某个怪物的伤害免疫比例（0 = 不免疫，0.3 = 减伤 30%）。
        /// 默认 0。由有此类规则的环境覆写 —— 例如熔岩瀑布对岩浆怪的高温免疫。
        ///
        /// 为什么放这里而不是在 RunBattle 里判断类型：以后每加一条「环境 × 怪物」规则，
        /// 那种写法就要多一个 is（还要同时认出是哪个环境、哪只怪），
        /// 而这里只是多一个 override；战斗循环只负责把结果写进怪物。
        /// </summary>
        public virtual double GetDamageImmunity(Monster m) => 0.0;

        /// <summary>
        /// 本环境是否有可回的水（水瓶能不能在这里生效）。
        /// 默认 false，有水源的环境（如 <see cref="LavaWaterfall"/>）覆写为 true。
        ///
        /// 与 <see cref="DrainWater"/> 同一套做法：基类声明「有没有这件事 + 空实现」，
        /// 子类只覆写自己有语义的那一份。
        /// 为什么不在道具里判断具体环境类型：原先是
        /// `if (p.CurrentEnvironment is LavaWaterfall lw)`，每加一个有水源的环境
        /// （湖泊、雨水收集点……）就要回头改 WaterBottle。
        /// </summary>
        public virtual bool CanRefillWater => false;

        /// <summary>把本环境的水回满。默认什么也不做（见 <see cref="CanRefillWater"/>）。</summary>
        public virtual void RefillWater() { }

        /// <summary>
        /// 本环境能否用硫酸开采矿石（设计文档第九章：水淹洞穴开采金属矿）。
        /// 默认 false，水淹洞穴覆写为 true。
        ///
        /// 为什么不直接复用 <see cref="CanMine"/>（镐子的能力位）：
        /// 两者当前恰好落在同一个环境上，但语义不同 —— 镐子挖的是「全类别随机矿脉」，
        /// 硫酸腐蚀的是「指定的金属矿脉」；将来完全可能出现「能挖矿但硫酸没用」
        /// （矿洞里没有可腐蚀的岩缝）或反之的环境。合用一个能力位会把两者绑死。
        ///
        /// ⚠️ 现状：硫酸已改为向环境索取具体产物（见 <see cref="MineOreWithAcid"/>），
        /// 所以本属性**目前没有任何道具在读它**。按 CONVENTIONS 1.2「删除 public API 要先问」
        /// 保留在基类上，作为「这个环境有没有可腐蚀的矿脉」这一事实的声明
        /// （将来镐子/空房事件要问「这里有没有矿」时可以复用）。
        /// 千万别再用它来决定产物是金属 —— 「能不能开采」和「开出什么」是两个问题，
        /// 只问布尔值会让放射性区域静默产出金属矿。
        /// </summary>
        public virtual bool CanMineOre => false;

        /// <summary>
        /// 本环境「可被硫酸腐蚀出的矿脉」产出。返回 <c>null</c> = 这里没有可腐蚀的矿脉
        /// （硫酸不消耗，只提示「你不能饮用硫酸。」）。
        ///
        /// 为什么不让硫酸问 <see cref="CanMineOre"/> 的布尔值自己 new Metal：
        /// 布尔值只回答「能不能开采」，不回答「开出什么」。设计文档 5.6 要求放射性区域
        /// 「开采放射矿」—— 等那个环境覆写 `CanMineOre => true` 时，硫酸会**静默产出金属矿**，
        /// 不报错、不崩溃，只是产错了东西。所以产物必须由环境自己交出：
        /// 水淹洞穴给金属，放射性区域将来给放射矿，硫酸只负责「拿到什么就发什么」。
        ///
        /// 与 <see cref="GetBattleLoot"/> 同一套做法：环境自己知道「这一层会出什么」，
        /// 道具只负责搬运。
        ///
        /// 数量区间由各环境自己定（水淹洞穴沿用原来的 1-3，即设计文档未给数值前的取值）。
        /// </summary>
        public virtual Item MineOreWithAcid() => null;

        /// <summary>
        /// 本环境的高温是否会把木头烤成炭（木头道具的倒计时只在这样的环境里推进）。
        /// 默认 false，熔岩瀑布覆写为 true。
        ///
        /// 为什么放在基类、而不是在 <see cref="Items.Wood"/> 里判断具体类型：
        ///   原先木头写的是 `if (envType == EnvType.LavaWaterfall) Change -= 1;` ——
        ///   每加一个高温环境（岩浆池、火山区、着火的房间……）都要回来改这个道具，
        ///   而且它比的是**环境枚举**，与「环境对象自己声明能力」这套做法不一致
        ///   （见 CanMine / CanRefillWater / IsFatalToPlayer）。
        /// </summary>
        public virtual bool BurnsWood => false;

        /// <summary>
        /// 热硫在本环境被使用时的额外环境效果（设计文档 5.2：堵住水淹洞穴的出水口）。
        /// 默认什么也不做 —— 没有这类规则的环境不受影响。
        ///
        /// 为什么由环境来执行：热硫的攻击效果是通用的，只有「这一层有没有出水口」
        /// 是环境自己的知识。原先道具里写的是 `if (p.CurrentEnvironment is FloodedCavern flooded)`，
        /// 每加一种「某道具在某环境有额外效果」的规则，道具里就多一个 is。
        /// </summary>
        public virtual void OnFusionSulphurUsed() { }

        /// <summary>拼进玩家状态栏的一行环境状态。</summary>
        public abstract string DescribeStatus();
    }
}
