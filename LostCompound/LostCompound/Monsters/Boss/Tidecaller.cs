using LostCompound.Tools;
using LostCompound.Tools.Players;

// ⚠️ `Environment` 必须显式指到我们的环境基类：不写这一行会解析成 System.Environment，
//    于是 `Environment env = p.CurrentEnvironment;` 会报 CS0723（无法声明静态类型的变量）。
//    与 DebugMenu.cs 顶部同一处理。
using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.MapEnvironment.Environments;   // FloodedCavern：潮涌要调它的 ForceSurge

namespace LostCompound.Monsters.Boss
{
    /// <summary>
    /// 涨潮者 —— 水淹洞穴专属 Boss（设计书 m04290 / m04320）。
    ///
    /// 核心博弈：<b>它在涨潮，你在堵水</b>。
    ///   堵水 → 断它粮（潮涌变成普通攻击，不再回血），且水位冻结；
    ///   但堵水那 3 回合你没法攻击（热硫占掉出手）。
    ///   所以「堵水」是拿 3 回合输出换一张安全牌。
    ///
    /// 与沙葬者的对称：
    ///   沙葬者逼你「当场把沙变战斗力」；
    ///   涨潮者逼你「当场把水堵住」，等的是热硫而不是压缩沙瓶。
    ///   两者都是「不处理手头的库存就必死」，但处理方式一个花材料、一个花回合。
    ///
    /// ⚠️ 三条实现边界（见 CONVENTIONS / 设计书）：
    ///   ① 水位**不由怪物写**：它只调 <see cref="FloodedCavern.ForceSurge"/> 声明
    ///      「本回合我要涨 4」（<see cref="SurgeWater"/>），夹上限、播报都留在环境里，涨水规则只有一处；
    ///   ② 间隔数的是「它自己真出手的次数」——被跳过的回合不算，与沙葬者一致；
    ///   ③ 它**不认识** <see cref="FloodedCavern"/> 这个类型，只问基类的
    ///      <see cref="Environment.IsWaterSourceBlocked"/> 能力位。
    /// </summary>
    public sealed class Tidecaller : Monster
    {
        /// <summary>
        /// 档位：**Boss**。占掉水淹洞穴 <c>BossRoom</c> 的位置（原先那层是龙），
        /// 与沙葬者占沙漠层完全对称。
        /// </summary>
        public override MonsterRank Rank => MonsterRank.Boss;

        // ==================== 数值（全部来自设计书） ====================

        /// <summary>
        /// 普通攻击力。
        /// 设计书：Basic = 2 + 3×√9 = 11，对 DEF 5 的玩家约 8~9 伤。
        /// </summary>
        public const int AttackDamage = 11;

        /// <summary>
        /// 潮涌回合的水位涨幅。
        ///
        /// Hyberty 2026-11-22 裁定走 A 方案：**频次不动（仍是每 2 次出手一次），只把涨幅调大**。
        ///
        /// 为什么 +2 不够（这是改数字的唯一理由）：
        ///   潮涌平均每回合 +1，水位上限 15 → 15 回合才淹满。
        ///   而它自身「每回合 11 伤 + 每 2 回合回 6」算下来，玩家满水（每秒 13 伤）约 11 回合就能打完 ——
        ///   也就是说**不堵水也能赢**，堵水就没有价值，整个博弈不成立。
        ///
        /// 取 +4 之后：
        ///   平均每回合 +2 → 约 8 回合淹满，与设计书「7~8 回合到头」一致；
        ///   不堵水约 11 回合才能打完 → 先淹死；
        ///   堵水（3 回合冻结水位）→ 3 + 8 = 11 回合，卡在死亡线边缘。
        /// 数值改动点只有这一行，其余节奏自动跟着走。
        /// </summary>
        public const int SurgeWater = 4;

        /// <summary>潮涌成功吸水时自身恢复的 HP。</summary>
        public const int SurgeHeal = 6;

        /// <summary>潮涌间隔：每 2 次**它自己出手**发动一次。</summary>
        public const int SurgeEvery = 2;

        /// <summary>
        /// 血量。设计书中「Boss 70 血」。
        ///
        /// 为什么是 70 而不是龙那种 80：它的收益全在回血上，
        /// 血再堆高会让「堵住之后的对轰」拖过水位的死亡线，反而更逼玩家赌。
        /// </summary>
        private const double BaseHp = 70;

        /// <summary>防御。设计书没写；沿用龙/沙葬者那一档的 5。</summary>
        private const double BaseDef = 5;

        /// <summary>掉落（环境可能整体压制，沙漠/水淹洞穴都有各自的掉落规则）。</summary>
        public const int MinLootSand = 0;
        public const int MaxLootSand = 0;

        /// <summary>它自己已经真出手过几次（被跳过的回合不算，见类注释）。</summary>
        private int _turnsTaken;

        public Tidecaller()
        {
            Type = MonsterType.Tidecaller;
            Name = "涨潮者";

            Hp = BaseHp;
            Atk = AttackDamage;
            Def = BaseDef;
            Shield = 0;
            SkipTurn = 0;
        }

        // ==================== 意图 ====================

        /// <summary>
        /// 本次出手是不是潮涌回合。
        ///
        /// 与沙葬者的 <c>IsDevourTurn</c> 同一算法：+1 是这一次的序号，
        /// 能被 2 整除就是潮涌 → 第 1 次普攻、第 2 次潮涌、第 3 次普攻……
        /// </summary>
        public bool IsSurgeTurn => (_turnsTaken + 1) % SurgeEvery == 0;

        /// <summary>
        /// 意图：潮涌要提前告诉玩家，否则玩家没法决定「这一回合要不要先堵水」。
        ///
        /// 潮涌回合统一显示「潮涌」，不区分出水口堵没堵 ——
        /// 玩家在**自己回合里**才可能用热硫，回合开始显示时的堵住状态到执行时早就变了。
        /// 与沙葬者「吞噬」同样只报技能名，不报会骗人的数值。
        /// </summary>
        public override string IntentText()
        {
            return IsSurgeTurn ? "潮涌" : "打 " + AttackDamage.ToString("F0");
        }

        /// <summary>
        /// 意图完全由「第几次出手」决定，不摇骰子。
        /// 仍要显式 <see cref="Monster.SetPlannedOption"/>：不调的话基类留着 -1，
        /// <see cref="Monster.TakePlannedOption"/> 会在出手时自己摇一个 —— 这里没有可摇的东西。
        /// </summary>
        public override void PlanIntent(Player p) => SetPlannedOption(0);

        // ==================== 出手 ====================

        public override void Attack(Player p)
        {
            Console.WriteLine(Name + "甩出一道水鞭，狠狠抽了过来！");
            p.TakeDamage(AttackDamage);
        }

        public override void OnMonsterTurn(Player p)
        {
            // 回合头（「XXX的回合」）与跳过检查都在 Tools.RunMonsterTurns 里统一处理，
            // 走到这里 = 这一回合它真的要出手，计数可以安心 +1。
            _turnsTaken++;
            TakePlannedOption(1);   // 用掉本回合选项，与其它怪保持同一状态机

            if (_turnsTaken % SurgeEvery == 0)
            {
                Surge(p);
            }
            else
            {
                Attack(p);
            }
        }

        /// <summary>
        /// 潮涌。
        ///
        /// 两条分支的分界只有一个：<b>此刻</b>出水口堵没堵
        /// （<see cref="Environment.IsWaterSourceBlocked"/>，问的是玩家当前环境）。
        /// 不看回合开始的快照 —— 玩家在自己回合里丢热硫就能当场断它的粮。
        ///
        /// 未堵：水位由环境固定涨 <see cref="SurgeWater"/>（取代本回合的随机涨水），并自回 6 血。
        /// 已堵：吸水失败，这一回合退化成普通攻击（设计书「出水口已堵：无法吸水，本次改为普通攻击」）。
        /// </summary>
        private void Surge(Player p)
        {
            // 用战斗真正推进的那个环境（BattleEnvironment），不是 p.CurrentEnvironment ——
            // 调试战斗里玩家脚下没有房间，后者是 null，潮涌会静默变成「只回血不涨水」。
            Environment env = BattleEnvironment;
            bool blocked = env?.IsWaterSourceBlocked == true;

            if (blocked)
            {
                Console.WriteLine(Name + "扑向出水口，却发现那里被你堵死了 —— 它吸不到水！");
                Console.WriteLine(Name + "恼羞成怒，改为直接撞向你！");
                p.TakeDamage(AttackDamage);
                return;
            }

            Console.WriteLine(Name + "张口狂吸，整片水域都朝它涌去！");

            // 涨水的**规则**留在环境里：这里只说「这一回合涨 SurgeWater」，具体多少由常量决定。
            // 环境不在（理论上不会，BossRoom 一定带环境）时也不报错，只是没有涨水这一步。
            if (env is FloodedCavern flooded)
            {
                flooded.ForceSurge(SurgeWater);
            }

            Hp += SurgeHeal;
            Console.WriteLine(Name + "恢复了 " + SurgeHeal + " 点生命！");
        }

        // ==================== 受伤 ====================

        /// <summary>
        /// 挨打走基类的通用流程（先过防御、再过护盾、最后扣血）。
        ///
        /// 它**没有护盾**（设计书没给，与龙不同）：生存全靠潮涌回血。
        /// 所以不需要覆写 <see cref="Monster.ResetShieldForTurn"/>，
        /// 基类的空实现就够了 —— 它的 Shield 永远是 0。
        /// </summary>
        public override void TakeDamage(double dmg) => TakeDamageWithDefaultLog(dmg);

        /// <summary>
        /// 它不掉骰子（意图完全由「第几次出手」决定），
        /// 但 <see cref="Monster.RollIndex"/> 在基类是抽象成员，必须实现。
        /// 这里的实现只在基类兜底路径（PlanIntent 没被调用的异常情况）才会被用到。
        /// </summary>
        protected override int RollIndex(int count) => count <= 0 ? 0 : Random.Shared.Next(count);
    }
}
