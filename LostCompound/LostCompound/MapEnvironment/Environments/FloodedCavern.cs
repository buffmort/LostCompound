using LostCompound.Items;
using LostCompound.Tools;   // Player：环境直接改玩家（见 Environment 类注释的「方案 A」）
using LostCompound.Items.Materials;
using Environment = LostCompound.MapEnvironment.Environment;
using LostCompound.MapEnvironment;

namespace LostCompound.MapEnvironment.Environments
{
    /// <summary>
    /// 水淹洞穴（设计文档 5.2）。
    ///
    /// 水位计数：每回合 50% 涨 1，10% 涨 2，40% 不变（困难模式 40% / 20% 等困难模式做出来再接）。
    /// 15 计数：全部对象死亡，玩家直接算输。
    /// 对策：热硫堵出水口 3（困难 2）回合，堵住后水位不再上涨。
    ///
    /// 结构完全对照 <see cref="LavaWaterfall"/>：环境对象自己持有本层状态、负责每回合推进。
    /// </summary>
    public sealed class FloodedCavern : Environment
    {
        /// <summary>
        /// 水淹洞穴可用硫酸开采金属矿（设计文档第九章）。
        /// 与 <see cref="CanMine"/>（镐子）分开声明：见基类 CanMineOre 的说明。
        /// </summary>
        public override bool CanMineOre => true;

        /// <summary>硫酸在水淹洞穴腐蚀出的金属数量区间（含两端）。文档只写「开采金属矿」，未给数量，沿用 1-3。</summary>
        private const int MinAcidMetal = 1;
        private const int MaxAcidMetal = 3;

        /// <summary>
        /// 硫酸在水淹洞穴的产出：一块金属矿脉（数量 1-3）。
        ///
        /// 这条规则原先写在 SulfuricAcid.Use 里（`new Metal(Random.Shared.Next(MinMetal, MaxMetal))`），
        /// 也就是「环境决定能不能挖、道具决定挖出什么」—— 等放射性区域（5.6）覆写
        /// `CanMineOre => true` 时，硫酸会静默产出金属矿。现在产物归环境：
        /// 水淹洞穴给金属，放射性区域自己给放射矿，硫酸只负责搬运。
        /// </summary>
        public override Item MineOreWithAcid() => new Metal(Random.Shared.Next(MinAcidMetal, MaxAcidMetal + 1));

        /// <summary>水位上限。到 15 就是设计文档 5.2 的「15 计数」。</summary>
        public const int MaxWaterLevel = 15;

        /// <summary>当前水位计数，从 0 开始（刚进洞时还没淹起来）。</summary>
        public int WaterLevel { get; private set; }

        /// <summary>
        /// 出水口还堵着几个回合。> 0 表示被堵住，此时本回合不涨水。
        /// 单独用字段而不是把 IsPlugged 做成可写属性：堵住状态只允许通过 PlugOutlet 推进，
        /// 避免外部随便改数字导致「堵住但不递减」的死状态。
        /// </summary>
        private int _pluggedTurns;

        /// <summary>是否处于被堵住状态。</summary>
        public bool IsPlugged => _pluggedTurns > 0;

        /// <summary>
        /// 本回合的涨水被某个 Boss 技能接管（潮涌）。
        ///
        /// 语义：本回合**不再掷随机数**，改为固定涨 <see cref="_surgeAmount"/> 点。
        /// 由出怪（<c>Tidecaller</c>）在自己出手前调 <see cref="ForceSurge"/> 设置，
        /// <see cref="OnBattleTurn"/> 读到之后用一次就清掉 —— 所以它只管一个回合，
        /// 怪物死了或者被跳过都不会把状态挂到下一回合。
        ///
        /// 为什么不让怪物直接改 WaterLevel：
        ///   水位是环境的私有状态，写入口留在环境里，涨水规则（含上限夹取、播报）
        ///   才能保持只有一处。怪物只负责声明「这一回合我要涨多少」。
        /// </summary>
        private int _surgeAmount;

        /// <summary>
        /// 让本回合的水位固定上涨 <paramref name="amount"/> 点（潮涌）。
        /// 堵住期间调用无效 —— 出水口被堵就是吸水失败，这一点由 <see cref="IsPlugged"/> 决定。
        /// </summary>
        public void ForceSurge(int amount)
        {
            if (amount <= 0) return;
            if (amount > _surgeAmount) _surgeAmount = amount;
        }

        public FloodedCavern() : base(EnvType.FloodedCavern) { }

        /// <summary>
        /// 水淹洞穴可开采（设计文档 5.2：硫酸在此开采金属矿；镐子的「采掘」也在这里生效）。
        /// 覆盖基类的 false —— 这是本属性存在的意义：环境自己声明「我能开采」，
        /// 将来矿洞(5.4) / 放射性区域(5.6) 照抄这一行即可。
        /// </summary>
        public override bool CanMine => true;

        /// <summary>
        /// 水位满 = 玩家必败（设计文档 5.2 的「15 计数」）。
        /// 判定条件留在环境自己身上：战斗循环只问 <see cref="Environment.IsFatalToPlayer"/>，
        /// 不需要知道「水淹洞穴」这个具体类型，也不需要知道阈值是 15。
        /// </summary>
        public override bool IsFatalToPlayer => WaterLevel >= MaxWaterLevel;

        /// <summary>判负文案：与原先写死在战斗循环里的那句逐字一致。</summary>
        public override string FatalMessage => "水位涨到了极限，整个洞穴被淹没——你失去了意识……";

        /// <summary>
        /// 每回合推进水位。<paramref name="hotRoom"/> 在这里不用 —— 高温房间只影响水量消耗，
        /// 水淹洞穴管的是**水位**（涨水），与玩家的水壶无关。
        /// </summary>
        public override void OnBattleTurn(bool hotRoom)
        {
            // 被堵住时只走倒计时，且完全不掷随机数 —— 设计文档说「堵住后水位不再上涨」，
            // 堵住期间不应该留下任何涨水的可能。
            if (_pluggedTurns > 0)
            {
                _pluggedTurns--;
                if (_pluggedTurns == 0)
                {
                    Console.WriteLine("堵住出水口的热硫失效了，水位又开始上涨！");
                }
                return;
            }

            // ① 先看本回合有没有被潮涌接管。
            //    接管时**完全不掷随机数**：潮涌说的是「本回合涨 N」（N 由 Tidecaller.SurgeWater 定），
            //    再叠一层随机涨水就等于同一个回合涨两次，玩家数不准回合数也就没法规划。
            //    这里仍然要先判堵：堵住期间吸水失败，潮涌不生效（见 Tidecaller）。
            int surge = _surgeAmount;
            _surgeAmount = 0;
            if (surge > 0)
            {
                WaterLevel += surge;
                if (WaterLevel > MaxWaterLevel) WaterLevel = MaxWaterLevel;
                Console.WriteLine("水流轰然上涨，水位 +" + surge + "！");
                if (WaterLevel >= MaxWaterLevel)
                {
                    Console.WriteLine("水流已经漫过了你的胸口——水位到达了极限！");
                }
                return;
            }

            // 一次掷点决定本回合涨多少：0.00-0.50 涨 1（50%），0.50-0.60 涨 2（10%），其余 0.60-1.00 不变（40%）。
            // 只掷一次是关键：分开掷两次会变成 50%×10% 的联合概率，不符合文档的区间描述。
            double roll = Random.Shared.NextDouble();
            if (roll < 0.50)
            {
                WaterLevel += 1;
            }
            else if (roll < 0.60)
            {
                WaterLevel += 2;
            }

            // 夹到上限：到 15 就是「淹满」，后续由战斗循环负责判负，这里不再往上加。
            if (WaterLevel > MaxWaterLevel)
            {
                WaterLevel = MaxWaterLevel;
            }

            if (WaterLevel >= MaxWaterLevel)
            {
                Console.WriteLine("水流已经漫过了你的胸口——水位到达了极限！");
            }
        }

        /// <summary>
        /// 堵住出水口指定回合数。设计文档 5.2 的对策：热硫堵出水口 3 回合。
        /// 重复使用取较长者，避免短的一次覆盖掉长的（否则连丢热硫反而缩短堵住时间）。
        /// </summary>
        public void PlugOutlet(int turns)
        {
            if (turns <= 0) return;
            if (turns > _pluggedTurns) _pluggedTurns = turns;
            Console.WriteLine($"你用热硫堵住了出水口，水位在{_pluggedTurns}回合内不再上涨。");
        }

        /// <summary>
        /// 热硫在本环境被使用时的额外效果：堵住出水口（设计文档 5.2 的对策）。
        /// 回合数 3 与原先写在 FushionSluphur.Use 里的字面量一致 —— 现在这份规则
        /// 「热硫在水淹洞穴堵 3 回合」整条都归环境，道具侧只负责喊一声「我用了热硫」。
        /// </summary>
        public override void OnFusionSulphurUsed() => PlugOutlet(3);

        /// <summary>
        /// 把「堵住」暴露成基类的能力位，怪物（<c>Tidecaller</c> 的潮涌）
        /// 据此判断「这一回合能不能吸水回血」。怪物不需要认识本类型。
        /// </summary>
        public override bool IsWaterSourceBlocked => IsPlugged;

        /// <summary>存档键名。**发布后不能改**。</summary>
        private const string StateKeyWaterLevel = "waterLevel";
        private const string StateKeyPluggedTurns = "pluggedTurns";

        /// <summary>
        /// 水淹洞穴要存的是**水位**和**出水口还堵着几回合**。
        /// 后者不存的话，读档后堵住状态丢失 —— 水位会立刻开始涨，玩家手里的热硫白用了。
        /// </summary>
        public override Dictionary<string, int> SaveState() => new()
        {
            [StateKeyWaterLevel] = WaterLevel,
            [StateKeyPluggedTurns] = _pluggedTurns,
        };

        public override void LoadState(IReadOnlyDictionary<string, int> state)
        {
            // 两个键各自独立判缺：缺哪个就保持哪个的默认值，不要因为一个键缺失就整体放弃
            if (state.TryGetValue(StateKeyWaterLevel, out int level))
            {
                WaterLevel = Math.Clamp(level, 0, MaxWaterLevel);
            }
            if (state.TryGetValue(StateKeyPluggedTurns, out int plugged))
            {
                _pluggedTurns = Math.Max(plugged, 0);   // 负数会让 IsPlugged 逻辑变味
            }
        }

        public override string DescribeStatus()
        {
            string s = $"水淹洞穴水位：{WaterLevel}/{MaxWaterLevel}";
            if (WaterLevel >= 12)
                s += "\n水位逼近极限！";
            if (IsPlugged)
                s += $"\n出水口已堵住（剩余 {_pluggedTurns} 回合）";
            return s;
        }
    }
}
