using LostCompound.Items;
using LostCompound.Monsters;   // GetDamageImmunity(Monster)：环境声明「本环境对某只怪的免疫比例」
using LostCompound.Tools;
using LostCompound.Tools.Players;

namespace LostCompound.MapEnvironment
{
    /// <summary>
    /// 地图环境基类。每种环境一个子类，由 <see cref="EnvironmentFactory"/> 按 <see cref="EnvType"/> 创建。
    ///
    /// 职责分工（按设计文档）：
    ///   环境对象   —— 持有本层环境状态（如水量），负责每回合推进、给战斗房间掉落；
    ///   EmptyRoom  —— 负责空房事件的判定与执行（事件内容写在 EmptyRoom 里）。
    ///
    /// 设计取向（**现行方案**）：
    ///   环境**只提供修正量**（如 <see cref="StrModifier"/>），由 <c>Player.RecomputeStats()</c>
    ///   一处统一重算最终值。
    ///
    ///   ⚠️ 本段的上一版写的是「已裁定为方案 A：环境**直接改玩家**」，并给出示例
    ///   <c>p.Str = Player.BaseStr - 惩罚;</c> —— 那**既不成立也已经过时**：
    ///     - 不成立：<c>Player.Str</c> 现在是 <c>private set</c>，环境根本写不进去（该行编译不过）；
    ///     - 过时：改 STR 的来源不止环境一个（还有 buff、装备），谁都能写 p.Str 就会互相覆盖，
    ///             写成 += 又会累加、没人负责恢复。所以最终落回了「大家都只提供修正量」这套，
    ///       也就是本文件下面 OnBattleTurn / StrModifier 的注释、以及 CONVENTIONS 2.7
    ///       「Environment 基类不引用 Player —— 它只暴露修正量，由 Player 主动来读」讲的那套。
    ///   历史教训保留在这里当反面教材：环境直接往派生值上写（p.Str -= x）会与
    ///   「打了多少回合」挂钩，而且水回满了也恢复不了 —— 想永久/临时改属性，只能分别走
    ///   基础值（GrowBaseXxx）与修正量（StrModifier / Buff.StrBonus），见 CONVENTIONS 2.3。
    /// </summary>
    public abstract class Environment
    {
        public EnvType Type { get; }

        protected Environment(EnvType type) => Type = type;

        /// <summary>
        /// **往本回合的执行清单里追加步骤**（Hyberty 2026-11-21 的 B 方案）。
        ///
        /// 这是「不同环境让回合流程不一样」的唯一入口。典型用法 —— 城市地图让玩家双动：
        ///
        /// <code>
        /// public override void ContributeTurnSteps(TurnPipeline pipeline, Player p, List&lt;Monster&gt; monsters)
        /// {
        ///     AddExtraPlayerAction(pipeline, "玩家行动（城市双动）");
        /// }
        /// </code>
        ///
        /// 注意三件事：
        ///   ① 传进来的 pipeline 是**本回合的副本**，随便改，跑完就丢 ——
        ///      所以这里每回合都会重新调用一次，不要试图在里面存状态（要存就存字段）。
        ///   ② 不要往模板上改：副本是浅拷贝，但列表本身是新的，所以 Add/InsertAfter 安全。
        ///   ③ 追加的步骤同样要返回 bool（false = 战斗结束），和内置步骤一个规矩。
        ///
        /// 默认什么都不做：绝大多数环境不改回合结构。
        /// </summary>
        public virtual void ContributeTurnSteps(TurnPipeline pipeline, Player p, List<Monster> monsters) { }

        /// <summary>
        /// 给「玩家双动」用的现成实现：在「玩家行动」后面再插一次。
        ///
        /// 为什么不直接写进基类的 ContributeTurnSteps：
        ///   双动是**个别环境**（城市地图）的特性，绝大多数环境不该有。
        ///   基类留空、需要的子类在覆写里调一句这个方法即可：
        ///
        /// <code>
        /// public override void ContributeTurnSteps(TurnPipeline pipeline, Player p, List<Monster> monsters)
        /// {
        ///     AddExtraPlayerAction(pipeline, "玩家行动（城市双动）");
        /// }
        /// </code>
        ///
        /// 注意它插的是「玩家行动」而**不是**「玩家回合开始」：
        /// 双动只管多一次行动，不该再清一次盾、不该让道具再涨一次耐久。
        /// </summary>
        protected static void AddExtraPlayerAction(TurnPipeline pipeline, string name)
        {
            pipeline.InsertCopyAfter(TurnPipeline.PlayerActionStep, name);

            // ⚠️ 插入位置是「玩家行动」的**正后方**，而「额外回合」（ExtraTurn buff）
            //    是排在「玩家行动」之后的另一独立步骤。所以同时存在两者时，顺序是：
            //        玩家行动 → 玩家行动（本方法插入的） → 额外回合 → 怪物回合
            //    也就是「双动」会先跑完，再由 buff 追加额外行动。
            //    如果将来想让额外回合夹在两次行动中间，就得改成「在额外回合步骤之后再插一次」，
            //    那属于玩法顺序的裁定，别顺手改 —— 改之前先确认设计意图。
        }

        /// <summary>
        /// 战斗中的每个回合调用一次：推进环境状态（如水量下降、水位上涨）。
        ///
        /// 注意这里**不接收 Player**：环境只负责推进自己，对玩家的影响通过
        /// <see cref="StrModifier"/> 这类「修正量」暴露出去，由 Player 重算时统一读取。
        ///
        /// <paramref name="hotRoom"/> = 玩家当前所在的房间是不是高温房间
        /// （<see cref="Rooms.Room.IsHot"/>）。为什么用参数传、不让环境自己去查房间：
        /// 环境对象是**整层共享**的，它没有「玩家在哪」这个概念；
        /// 而且这样环境仍然不依赖 Player（与「环境只推进自己」的纪律一致）。
        /// 目前只有熔岩瀑布读它（水量消耗 +50%）。
        ///
        /// 为什么不让环境直接改玩家（曾经这么做过，已改回）：
        ///   改 STR 的来源不止一个 —— 环境、buff、将来的装备都要参与。
        ///   如果各自都往 p.Str 上写就会互相覆盖；写成 += 又会累加、没人负责恢复。
        ///   统一成「大家都只提供修正量，由 Player 一处重算」，才不会打架。
        /// </summary>
        public abstract void OnBattleTurn(bool hotRoom);

        /// <summary>
        /// 对玩家 STR 的修正量（负值表示削弱，如熔岩瀑布缺水）。
        /// 默认不修正。Player 重算 STR 时会读它，环境本身不接触 Player。
        /// </summary>
        public virtual double StrModifier => 0.0;

        /// <summary>战斗房间通关后的额外掉落。默认无。</summary>
        public virtual List<Item> GetBattleLoot() => [];

        /// <summary>
        /// 空房间随机事件：本环境决定玩家在空房间里遇到什么。
        /// 返回 <c>null</c> = 本环境没有专属事件，房间退回通用的「按掉落表搜刮」逻辑。
        /// </summary>
        /// <remarks>
        /// 为什么把这件事交给环境（原来写在 <c>EmptyRoom.OnEnter</c> 里，是个 bug）：
        ///   原先空房间的 30% 分支写死了「山壁岩浆涌下 → <c>DrainWater(40)</c> → 播报蒸干了 40 点水」，
        ///   **完全不看当前是哪个环境**。于是水淹洞穴里也会照发这句话，而那边根本没有「水量」，
        ///   <c>DrainWater</c> 在基类里又是空实现 —— 玩家看到一句纯属虚构的损失。
        ///   那不是「文案写错了」，而是事件本该属于环境、却被写在了公共房间里：
        ///   每加一个新环境（沙漠 / 矿洞 / 墓地……）都得到 EmptyRoom 里再补一个 if，
        ///   而漏补的那一次只会安静地发一句假话。
        ///   现在环境自己声明「我这里空房间会发生什么」，EmptyRoom 只负责转交和播报，
        ///   新增环境写不写覆写都不影响别的环境，也不会串台词。
        ///
        /// 约束：环境**不接触 Player** —— 只改自己的状态、把道具和播报放进返回值，
        /// 由房间去发。这与 <see cref="StrModifier"/>「只提供修正量」是同一个约定。
        /// </remarks>
        public virtual EmptyRoomEvent RollEmptyRoomEvent() => null;

        /// <summary>
        /// 本环境是否可开采矿物（镐子的「采掘」只在可开采环境里生效）。
        /// 默认 false，可开采的环境（如 <see cref="FloodedCavern"/>）覆写为 true。
        ///
        /// 为什么放在基类、而不是在 Tool.Pickaxe 里判断具体类型：
        ///   将来矿洞（5.4）的矿道、放射性区域（5.6）的指定房间做出来时，
        ///   只要各自覆写这个属性，开采入口就自动接上 —— 不用回头改 Mining / Pickaxe。
        ///
        /// 为什么带 <c>Player p</c> 参数（2026-11-22 改）：
        ///   放射性区域的可采性取决于「**玩家脚下这间房**有没有矿脉」，环境自己不知道玩家在哪。
        ///   入口（Pickaxe）手上本来就有 `p`，顺着传进来比让环境反查玩家安全得多。
        ///   不带参数的环境（水淹洞穴）直接忽略它。
        /// </summary>
        public virtual bool CanMine(Player p) => false;

        /// <summary>
        /// 开采**成功之后**调一次，让环境消耗掉「这一次的矿源」。
        ///
        /// 为什么需要这个钩子（而不是「能挖就一直能挖」）：
        ///   放射性区域（§5.6）的矿源是**房间**上的矿脉，一间房只能挖一次 ——
        ///   没有这个回调，镐子就能在同一间房反复刷，3-5 个矿物的上限形同虚设。
        ///   水淹洞穴的矿是「有硫酸就能采」，不设上限，所以它不覆写（默认什么都不做）。
        ///
        /// 默认空实现：不支持矿源管理的环境写不写都不影响。
        /// </summary>
        public virtual void ConsumeOreVein(Player p) { }

        /// <summary>
        /// 本层 Boss 被打死之后调一次（由 <see cref="Rooms.Types.BossRoom"/> 在赢了的分支里调）。
        ///
        /// 设计文档 §7.4：「<c>EnvRadioactive</c> —— 击杀 Boss 后删除」。
        /// 也就是说 Boss 一死，这一层压在你身上的辐射状态就该撤掉 ——
        /// 玩家不会再被逼着往下一层跑，原地清干净是合理的奖励。
        ///
        /// 默认空实现：绝大多数环境没有「Boss 死了要撤掉的状态」。
        /// </summary>
        public virtual void OnBossDefeated(Player p) { }

        /// <summary>
        /// 能不能在**战斗菜单里采沙**（目前只有沙漠，DESIGN §5.3「采沙：花费 1 回合获得 5 沙子」）。
        ///
        /// 与 <see cref="CanMine"/> 同一套做法：入口（`Player.OnPlayerTurn` 的第 6 项）
        /// 只问「这里能不能采」，不认识具体环境；将来再加一个能采东西的环境，
        /// 覆写这个属性 + <see cref="DigSand"/> 即可。
        /// </summary>
        public virtual bool CanDigSand => false;

        /// <summary>采一次沙的产物（默认什么都没有；沙漠返回沙子 × 5）。</summary>
        public virtual List<Item> DigSand() => [];

        /// <summary>
        /// 本环境是否**压掉怪物掉落**（DESIGN §5.3 第 2 条「怪物不掉落物品」）。
        ///
        /// 判定点在 <see cref="Monsters.Monster.Die"/>：它拿着玩家对象，能问到当前环境；
        /// 写成「问环境」而不是「`is Desert`」，是为了以后再有「贫瘠环境」时不用改怪。
        /// </summary>
        public virtual bool SuppressMonsterLoot => false;

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
        /// 这一层**有没有高温房间**（设计文档 §5.1：熔岩瀑布的特殊房间）。
        /// 默认 false，熔岩瀑布覆写为 true —— 由建图器据此决定要不要随机挑房间标成高温。
        ///
        /// 为什么是「环境声明有没有」而不是在生成器里比 <c>envType == LavaWaterfall</c>：
        /// 与 CanMine / BurnsSulphur 那一批能力位同一套做法 —— 以后加一个火山区，
        /// 生成器一行都不用改。
        ///
        /// ⚠️ 高温**不是**环境级的了：热量挂在房间上（<see cref="Rooms.Room.IsHot"/>），
        ///    所以「整个熔岩瀑布都烤木头」已经改成「只有高温房间烤木头」。
        /// </summary>
        public virtual bool HasHotRooms => false;

        /// <summary>
        /// 热硫在本环境被使用时的额外环境效果（设计文档 5.2：堵住水淹洞穴的出水口）。
        /// 默认什么也不做 —— 没有这类规则的环境不受影响。
        ///
        /// 为什么由环境来执行：热硫的攻击效果是通用的，只有「这一层有没有出水口」
        /// 是环境自己的知识。原先道具里写的是 `if (p.CurrentEnvironment is FloodedCavern flooded)`，
        /// 每加一种「某道具在某环境有额外效果」的规则，道具里就多一个 is。
        /// </summary>
        public virtual void OnFusionSulphurUsed() { }

        /// <summary>
        /// 水源是否被堵住了（设计文档 5.2：热硫堵住水淹洞穴的出水口）。
        /// 默认 false —— 没有水源的环境永远不算堵。
        ///
        /// 为什么要提升到基类：怪物（<c>Tidecaller</c> 的潮涌）需要问「我现在能不能吸水」，
        /// 而**怪物不该认识具体环境类型**。原先只能在怪物里写
        /// `if (p.CurrentEnvironment is FloodedCavern fc &amp;&amp; fc.IsPlugged)`
        /// —— 每加一种可堵的环境，怪物就多一个 is。
        /// 与 <see cref="CanMine"/> / <see cref="OnFusionSulphurUsed"/> 同一套做法：
        /// 环境声明能力，调用方只问能力位。
        /// </summary>
        public virtual bool IsWaterSourceBlocked => false;

        /// <summary>
        /// **每刷新一屏调用一次**（地图主循环与战斗内都会走到）。
        ///
        /// 为什么需要这个钩子：环境里唯一需要「秒」这个概念的是放射性区域，
        /// 而秒不能靠回合推进（回合是玩家按出来的，卡着不动就永远不走）。
        /// 但它也不能自己乱开线程 —— 谁来启动、谁来把后台攒下的播报画到屏幕上，
        /// 都得有一个**主线程的落点**。这个钩子就是那个落点：
        ///   · 第一次调用时启动计时线程（幂等）；
        ///   · 之后每次调用把后台排队的播报排空到信息框。
        ///
        /// ⚠️ 一定只在**主线程**调用（绘制控制台不是线程安全的），
        ///    所以后台线程只能往队列里塞字符串，不能自己画。
        /// 默认什么也不做 —— 不需要秒的环境完全不受影响。
        /// </summary>
        public virtual void OnScreenRefresh(Player p) { }

        /// <summary>拼进玩家状态栏的一行环境状态。</summary>
        public abstract string DescribeStatus();

        /// <summary>
        /// 存档：导出**本环境自己持有的**状态（水量、水位……）。
        /// 默认返回空字典 —— 没有状态的环境（将来的静态场景）不用写任何东西。
        ///
        /// 为什么用「字符串键 + int 值」而不是给每个环境定一个 DTO 类：
        ///   环境状态都是计数器（水量 / 水位 / 堵住几回合），DTO 类会变成
        ///   「一个字段一个类」的样板；键值对让新增环境只写自己那两行，
        ///   存档层完全不需要认识具体环境。
        ///
        /// ⚠️ 键名一旦发布就不能改：改了等于把旧存档里的这一项丢掉（<see cref="LoadState"/>
        /// 遇到缺键是**保持默认值**，不会崩，但玩家会看到水量莫名其妙回满）。
        /// </summary>
        public virtual Dictionary<string, int> SaveState() => [];

        /// <summary>
        /// 读档：灌回状态。默认什么也不做。
        /// 约定与 <see cref="Items.Capabilities.IItemState.LoadState"/> 一致：
        /// **缺键时保持当前值（构造时的默认值），不要当成 0。**
        /// </summary>
        public virtual void LoadState(IReadOnlyDictionary<string, int> state) { }
    }
}
