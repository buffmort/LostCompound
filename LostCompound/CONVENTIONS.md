# 《LostCompound》项目约定

> 本文件记录**协作约定**与**代码约定**。
> 与 `DESIGN.md` 的分工：**`DESIGN.md` 管「做什么」，本文件管「怎么做」。**
> 若某条约定已过时或被推翻，请直接改这里，不要留在对话里。

---

# 一、协作约定（人与 AI）

## 1.1 写入范围

- **只允许写** `C:\Users\dayli\IdeaProjects\` 和 `C:\Users\dayli\RiderProjects\`
- **其余路径一律只读**（包括临时目录里的探针文件）
- 若因工具副作用在范围外产生了文件，**必须主动报告**

## 1.2 什么时候可以直接改

| 情况 | 处理方式 |
|---|---|
| **明显的笔误 / 小错**（如堆叠数写错、漏 `return`、注释与实际矛盾） | **直接改完，告知一声**，然后提交 |
| **明确的 bug**（崩溃、越界、死循环、文档明说不符） | 直接改，报告时说明「改前 → 改后 → 为什么」 |
| **设计上有歧义** | **必须先问，不要自己设计** |
| **会改变游戏平衡的改动**（数值、概率、消耗品是否扣数） | 先问 |
| **大范围重构 / 删除 public API** | 先问 |

## 1.3 提交纪律

- **每完成一个可编译的改动就提交**，不要攒着
- 提交信息写清「改了什么 + 为什么」
- 提交前**必须编译通过**（0 错误）
- 提交后**打印 `git status`**，确认没有漏加/多加文件
- **`git add` 要检查完整输出** —— 曾经漏加过一个文件（`Player.cs`），导致仓库里缺一块，换台机器就编译不过

## 1.4 报告纪律

- **不要说「已确认无误」除非真的查过** —— 曾经把不存在的 `ShouldRemove()` 说成存在
- **发现了但没改的，单独列出来**，不要混在「已改」里
- **不确定的要明说「不确定」**
- **区分「编译通过」和「运行验证过」** —— 前者不等于后者
- 报告里要写**验证覆盖度**：哪些跑了、哪些没跑

## 1.5 验证纪律

- **编译只是最低标准**。功能改动必须实际跑一次
- 运行时验证优先用「驱动进程 + 渲染输出」的方式（游戏是交互式的，直接跑会卡住）
- **跑之前先 `Get-Process LostCompound | Stop-Process -Force`**，否则文件被锁导致编译失败
- **游戏每局地图是随机的** —— 需要特定条件（遇到某种怪）时，说明「这次没测到」

## 1.6 构建环境（⚠️ 新代理/新环境必看）

**本机 .NET SDK 装在用户目录，不在 `C:\Program Files\dotnet`：**

```
本机 .NET SDK 装在用户目录：`C:\Users\dayli\.dotnet\dotnet.exe`
（`C:\Program Files\dotnet` 下只有运行时，`sdk` 目录为空 —— 用裸 `dotnet` 可能报 "No .NET SDKs were found"）
所有构建命令请用全路径。
```

- **构建命令（用全路径）：**

  ```powershell
  & "C:\Users\dayli\.dotnet\dotnet.exe" build "C:\Users\dayli\RiderProjects\LostCompound"
  ```

- **⚠️ 不要把「No .NET SDKs were found」当成环境坏了** —— 那只是裸 `dotnet` 落到了
  `C:\Program Files\dotnet`（只有运行时）。换全路径即可，**不要因此跳过编译或改用别的 SDK**。
- 已核实（本次）：`C:\Users\dayli\.dotnet\dotnet.exe --list-sdks` 返回 **8.0.425 / 10.0.401**
  （都在 `C:\Users\dayli\.dotnet\sdk`）；`C:\Program Files\dotnet` 下只有 `host` / `shared` / `swidtag`，
  **没有 `sdk` 目录**。

---

# 二、代码约定

## 2.1 命名

| 项目 | 约定 |
|---|---|
| 类 / 方法 / 属性 | **PascalCase** |
| 局部变量 / 参数 | camelCase |
| 私有字段 | `_camelCase` |
| **枚举成员** | **PascalCase** |
| **接口** | **`I` 前缀**（`IUseable` / `IChangeable` / `IEquippable` / `ICraftable` / `ITurnCount`） |

**例外（用户明确要求保留原名）**：`Useable` / `Craftable` / `Changeable` / `TurnCount` 曾经是不带 `I` 的，后来已统一加 `I` 前缀。

## 2.2 语言陷阱（Java → C#）

| 项目 | Java | C# |
|---|---|---|
| **`string ==`** | 比较**引用**（要用 `.equals()`） | 比较**内容** |
| **方法默认虚** | 是（子类重写不用 `virtual`） | **否**（基类必须写 `virtual`，否则 CS0506） |
| **枚举可空** | 引用类型，可以是 null | 值类型，默认是 0 —— 需要可空时用 `ItemType?` |
| **无修饰符** | 包内可见 | **`private`** —— 要公开得显式写 `public` |
| **`const`** | 无（用 `static final`） | `const` = 编译期常量，**天然 static**，不要再写 `static` |
| **字符串插值** | 无 | `$"{a}/{b}"`（优先用它，但**输出必须逐字一致**） |

## 2.3 ⚠️ 累加 vs 赋值（本项目最容易踩的坑）

**判断标准：这个数值是「每次叠加」还是「每次重算」？**

| 语义 | 写法 | 例子 |
|---|---|---|
| **累加** | **方法** `AddXxx(int v) => Xxx += v;` | `AddSkipTurn` / `Buff.AddTurns`（流血叠加：伤害 min 夹取到 10，剩余回合相加） |
| **重算** | 先重置再应用 | `ApplyEquipmentPassives()` 每次都从 `BaseAtk` 重新算 |

**同一个概念在不同类里语义可能相反**：历史上 `setDOTdmg` 在 `Monster` 上是**累加**，在 `Player` 上是**赋值**（该 DOT 机制已按用户要求整体删除，周期性伤害全部改用 `Buffs` 包的流血）。改之前必须查原版。

**`Buff.Stacks` 的语义由子类定义 —— 方向也可能相反**（写新 buff 前先查一遍）：

| buff | `Stacks` 的含义 | 方向 |
|---|---|---|
| 流血（`BleedBuff`） | **强度**（层数 / 每跳伤害） | **越大越疼** |
| `NonEnvRadioactive`（辐射，DESIGN §7.4） | **回合间隔**（每隔几回合扣一次） | **越小越疼** |

所以辐射的 `Stacks` **不能**当强度用，而且「每次扣多少血」是**另一个数**（`Buff` 基类没有这个位，
得由子类自己存）—— 详见 DESIGN §7.4 的「buff 2」与「实现备注」。

**绝不能**把累加语义的方法改成属性 —— 编译会通过、运行不报错、**数值静默全错**。

**派生值 vs 资源（把 STR 那套推广到 ATK/DEF 时定下的一条口径）：**

| 类别 | 例子 | 存放与改写方式 |
|---|---|---|
| **派生值** | `Str` / `Atk` / `Def` | 永远等于「`BaseXxx` + 环境修正 + buff 修正 + 装备加成」，由 `Player.RecomputeStats()` 一处重算。想**永久**变强只能改基础值（`GrowBaseStr` / `GrowBaseAtk` / `GrowBaseDef`），想**临时**变强只能提供修正量（`Environment.StrModifier` / `Buff.StrBonus`、`AtkBonus`、`DefBonus`）。直接写派生值会被下次重算冲掉（金属的 `p.Def += 1` 就是这么静默失效的） |
| **资源** | `Hp` / `Shield` | 会被消耗（掉血 / 吃盾），**绝不能进重算通路**（重算 = 静默回满）。`Shield` 由 `ResetShieldForTurn()` 在每回合开头单独重置（走 `IShieldSource` 这条只算盾的通道），`Hp` 从不重置 |

**`RecomputeStats()` 的叠加顺序不能改**：环境 → buff → **装备放最后**。
原因：装备的通道 `IEquippable.ApplyPassive(p)` 是 `+=` 累加式，必须在重新赋值成基础值之后才能加（`CONVENTIONS 2.3`），否则打第二场会再叠一次。

## 2.4 道具类

**目录约定**：能力位接口在 `Items\Capabilities\`；道具根 `Items\` 只放 `Item` / `ItemType` / `ItemIds` / `ItemFactory`；
具体道具按类别放进 `Items\Materials\`（材料）、`Items\Consumables\`（消耗品）、
`Items\Containers\`（容器）、`Items\Equipment\`（装备）。命名空间与目录逐段对应。

**所有道具继承 `Item`（主构造器）：**

```csharp
public sealed class Xxx(int count) : Item(ItemIds.Xxx, "名称", 堆叠上限, 可堆叠, count), 接口们
{
    public override ItemType? Type => ItemType.Xxx;
}
```

**按需实现接口：**

| 接口 | 含义 | 必须做什么 |
|---|---|---|
| `IUseable` | 战斗中主动使用 | `Use(Player, List<Monster>)` |
| `IChangeable` | 数量归零后移出背包 / 变成别的 | `ShouldRemove()` + `Become()` |
| `IEquippable` | 放背包或饰品栏就生效（被动） | `ApplyPassive(Player)` + `DescribePassive()` |
| `ITurnCount` | 每战斗回合开始/结束触发 | `OnBattleStart` / `OnBattleEnd` |

**⚠️ 硬规定：任何在 `Use()` 里 `Count -= 1` 的道具，必须同时实现 `IChangeable`**，
否则数量归零后会留下一个「Xxx x0」永远占着背包格。
（`Metal` 和 `RedPhosphorus` 都踩过这个坑。）

**新增道具的完整清单：**
1. 在对应子包建类：`Items\Materials\Xxx.cs` / `Items\Consumables\Xxx.cs` /
   `Items\Containers\Xxx.cs` / `Items\Equipment\Xxx.cs`
2. `Items\ItemIds.cs` 加常量（**ID 按设计文档，允许跳号，不要挤号**）
3. `Items\ItemType.cs` 加枚举成员（**只追加在末尾**，否则改变已有成员的数值）
4. 若要能合成：`Tools\Player.Crafting.cs` 的 `AddDefaultRecipes()`（配方0）或 `AddTier1Recipes()`（配方1）
5. 若要能掉落：`Rooms\LootTables.cs` / `Monsters\MonsterLootTable.cs`
6. 若是新道具类：在 `Items\ItemFactory.cs` 的 `All` 表里补一行（ID 升序）

## 2.5 重复代码的判定

**如果同一段逻辑在 ≥2 个子类里各写一遍，就应该提到基类。**
本项目出过的事故：
- **跳过回合检查**散在 4 处（`Dragon`/`Zombie`/`Dummy`/`Player`），**第 5 处 `LavaSlime` 漏了** → 热硫对它无效
- 项目里其实**提供了公共方法** `Monster.TryConsumeSkipTurn()`，但**子类里零调用**（大家都内联重写了一遍）。
  **⚠️ 更正（后来已改）**：`Tools.RunBattle` 现在统一调用双方的 `TryConsumeSkipTurn()`
  （`m.TryConsumeSkipTurn()` 跳过怪物、`p.TryConsumeSkipTurn()` 跳过玩家），
  所以它**不再是零调用** —— 勿照旧描述删除它。

**结论**：每回合的通用结算（**流血 buff / 跳过 / 回合头 / 收尸**）**全部集中在 `Tools.RunBattle`**，
子类只实现 `OnMonsterTurn`（自己的行为）。

## 2.6 TUI 相关（`UI\ConsoleScreen.cs`）

- **文字宽度必须按显示宽度算，不能用 `string.Length`** —— 中文/全角占 **2 列**
  （`DisplayWidth()` + `IsWide()` 已实现，直接复用）
- **不要用 `\u001b[K`（清到行尾）** —— 左右分栏时它会擦掉右边邻居的边框。
  各框自己按外框宽度补齐空格即可
- **框的位置都是计算属性**，改 `MapRows` / `StatusRows` / `LogRows` / `EnemyRows` 会让下面自动跟随，
  但**变了之后要重画**（参考 `Relayout()`）。`EnemyRows` 只能走 `ConsoleScreen.SetEnemyRows()`：
  它会同时重算 `LogRows`（至少 1 行）、把 `EnemyRows` 夹到终端高度放得下的上限，并整屏重排；
  **不要**直接写 `EnemyRows`（会留下旧位置的边框、还可能顶破终端）。
- **总高度不能超过终端高度** —— 一旦顶到最底行，终端会滚动，**所有绝对定位全废**
- **输出文案尽量放在 `Player.RefreshStatus()` 里**，不要各房间自己拼状态栏

## 2.7 环境（`MapEnvironment\`）

**新增一个环境的清单：**
1. `MapEnvironment\EnvType.cs` 加枚举成员（**只追加在末尾**）
2. `MapEnvironment\EnvironmentFactory.cs` 的 `Create()` 加一个 `case`（未实现的返回 `null`）
3. 新建 `MapEnvironment\Environments\Xxx.cs` 继承 `Environment`，**照 `MapEnvironment\Environments\LavaWaterfall.cs` 的结构写**
   （该子包里的文件需要 `using Environment = LostCompound.MapEnvironment.Environment;`，
   否则 `Environment` 会与 `System.Environment` 撞名）
4. 要让玩家/调试菜单选得到：`MapEnvironment\EnvironmentCatalog.cs` 的 `All` 加一行（主菜单与调试菜单**共用这一份清单**，不要再各抄一份）
5. 本环境特有的能力（能挖矿 / 能回水 / 有判负条件 / 对某只怪有抗性…）一律**覆写能力位**，
   不要去改通用流程、道具或战斗循环

**基类成员：**

| 成员 | 语义 |
|---|---|
| `OnBattleTurn()` | 每个**战斗大回合**调用一次（`Tools.RunBattle` 里） |
| `StrModifier` | 对玩家有效 STR 的动态修正（**返回修正量，不是最终值**） |
| `GetBattleLoot()` | 战斗房间通关后的额外掉落 |
| `RollEmptyRoomEvent()` | 空房间随机事件：环境返回 `EmptyRoomEvent`（播报 + 道具），返回 `null` = 本环境没有专属事件、房间走通用掉落表。**环境不碰 Player**，播报与发道具由 `EmptyRoom` 执行。新环境要自己的空房事件就覆写它，**不要回头改 `EmptyRoom`** |
| `DescribeStatus()` | 拼进状态栏的一行或多行（`\n` 分行，**高度会自适应**） |
| `CanMine` | 镐子的「采掘」能否在此生效（水淹洞穴为 true） |
| `CanMineOre` | 硫酸能否开采金属矿。**与 `CanMine` 分开**：镐子挖随机矿脉、硫酸腐蚀指定金属脉，语义不同。⚠️ 硫酸已改问 `MineOreWithAcid()`（产物归环境），本属性**目前没有道具在读**，保留作声明（删除 public API 要先问）——**不要再用它决定产物** |
| `MineOreWithAcid()` | 硫酸在本环境腐蚀出的**产物**（`null` = 没有可腐蚀的矿脉，硫酸不消耗）。水淹洞穴给 1-3 金属；放射性区域将来给放射矿。产物必须由环境交出 —— 只问 `CanMineOre` 布尔值会让放射性区域静默产出金属矿 |
| `BurnsWood` | 本环境的高温是否把木头烤成炭（熔岩瀑布为 **true**）。木头道具每回合据此 `Change -= 1` |
| `CanRefillWater` / `RefillWater()` | 水瓶能否在此生效 / 把水回满 |
| `OnFusionSulphurUsed()` | 热硫在此环境的额外效果（水淹洞穴 → 堵出水口 3 回合） |
| `IsFatalToPlayer` / `FatalMessage` | 本环境是否已到「玩家必败」（水淹洞穴水位满）/ 判负时打印的那句话 |
| `GetDamageImmunity(Monster)` | 本环境给某只怪的伤害免疫比例（0 = 不免疫）；开打前由战斗循环写进怪物（`Monster.ApplyDamageImmunity`） |

**能力位的判据（不要 `is 具体环境类`）：** 问自己「这个判断会不会挡住将来合理的新增」——
会挡（每加一个有该特性的环境就得回头改通用代码）就把它做成基类上的 `virtual`（默认 false / no-op），
由有能力的子类覆写。参照 `CanMine` / `IsFatalToPlayer` / `GetDamageImmunity`。
`EnvironmentCatalog` 同理：环境清单只留一份，避免「主菜单能选、调试菜单选不到」。

**注意**：`Environment` 基类**不引用 `Player`** —— 它只暴露「修正量」，由 `Player` 主动来读。
这样避免 `MapEnvironment` 和 `Tools` 互相依赖。
（唯一引用 `Monsters` 的地方是 `GetDamageImmunity(Monster)`：免疫规则只与环境有关，
怪物侧只提供一个接收槽位 `Monster.ApplyDamageImmunity`，两边都拿不到对方的实例。）

## 2.8 目录 / 命名空间结构

**文件夹与命名空间逐段对应**（例：`Items\Materials\Sulphur.cs` → `namespace LostCompound.Items.Materials;`）。
根命名空间为 `LostCompound`。**不要用 `global using` 一把导入子包** —— 按文件补 `using`，保持依赖可见。

| 目录 | 命名空间 | 内容 |
|---|---|---|
| `Items\` | `LostCompound.Items` | `Item` / `ItemType` / `ItemIds` / `ItemFactory` |
| `Items\Capabilities\` | `LostCompound.Items.Capabilities` | 能力位接口（`IUseable` 等 10 个） |
| `Items\Materials\` | `LostCompound.Items.Materials` | 材料（8） |
| `Items\Consumables\` | `LostCompound.Items.Consumables` | 消耗品（9） |
| `Items\Containers\` | `LostCompound.Items.Containers` | 容器（3） |
| `Items\Equipment\` | `LostCompound.Items.Equipment` | 装备（7） |
| `Monsters\` | `LostCompound.Monsters` | `Monster` / `MonsterType` / `MonsterRank` / `MonsterLootTable` |
| `Monsters\Normal\` | `LostCompound.Monsters.Normal` | 普通怪（3） |
| `Monsters\Boss\` | `LostCompound.Monsters.Boss` | Boss 与剧情怪（4） |
| `Rooms\` | `LostCompound.Rooms` | `Room` / `RoomType` / `Direction` / `LootTables` / `MobSpawner` |
| `Rooms\Types\` | `LostCompound.Rooms.Types` | 具体房间（7） |
| `Tools\` | `LostCompound.Tools` | `Tools` / `Calculator` / `Recipe` / `Mining` |
| `Tools\Players\` | `LostCompound.Tools.Players` | `Player`（8 个 partial） |
| `Tools\Debug\` | `LostCompound.Tools.Debug` | `DebugMenu` / `DebugState` |
| `Tools\Launch\` | `LostCompound.Tools.Launch` | `GameLauncher` / `GameLobby` |
| `Buffs\` | `LostCompound.Buffs` | `Buff` / `BuffIds` / `BuffFactory` |
| `Buffs\Types\` | `LostCompound.Buffs.Types` | `BleedBuff` / `SuperMaleBuff` / `Strength` |
| `MapEnvironment\` | `LostCompound.MapEnvironment` | `Environment` / `EnvType` / `EnvironmentFactory` / `EnvironmentCatalog` |
| `MapEnvironment\Environments\` | `LostCompound.MapEnvironment.Environments` | `LavaWaterfall` / `FloodedCavern` |
| `UI\` | `LostCompound.UI` | `ConsoleScreen` / `LogRegionWriter` |
| `Maps\` | `LostCompound.Maps` | `TreeMazeGenerator` |

### `Player` 的拆分（partial class）

`Player` 有 1900+ 行、86 个成员，按职责拆成 8 个 `partial` 文件，全部在 `Tools\Players\`，
命名空间 `LostCompound.Tools.Players`。找成员按上表第 1 列定位：

| 文件 | 职责 |
|---|---|
| `Player.cs` | 基础值 / 派生值 / `GrowBaseXxx` / `RecomputeStats` / 生死 |
| `Player.Inventory.cs` | 背包：容量、`AddItem`、消耗品清扫、背包菜单 |
| `Player.Equipment.cs` | 饰品：穿脱、被动结算、饰品菜单 |
| `Player.Combat.cs` | 血与盾、受伤管线、`Attack` / `SelectTarget` / `FightCount` |
| `Player.Buffs.cs` | Buff 列表、`AddBuff`、回合开始结算、跳过回合、Buff 框 |
| `Player.Crafting.cs` | 配方表与手工合成 |
| `Player.Map.cs` | 地图、坐标、移动、`CurrentEnvironment` |
| `Player.UI.cs` | 分栏渲染、状态/敌人框、输入、主循环 `StartGame` / `OnPlayerTurn` |

**新增成员时先想清楚属于哪个文件** —— 塞回 `Player.cs` 就等于把这 1900 行重新养回来。
`partial` 只是分文件，**耦合没变**：任何一个 partial 里的 `private` 字段对另外 7 个都可见，
所以「能乱摸」这件事靠自觉，不靠编译器。

**⚠️ 包名不能叫 `Player`。** 命名空间和类型共用同一套名字查找：如果包是
`LostCompound.Tools.Player`、类型也叫 `Player`，那么从外部写 `Player p` 时，编译器先找到
**命名空间**，报 `CS0118`「命名空间不能当类型用」，而 `using` **修不好**这个问题。
所以包名取复数 `Players`。（同理以后不要建 `Tool\...` 之外的「类名同名包」。）

---

# 三、那些「看起来是 bug 其实不是」的东西

| 现象 | 真相 |
|---|---|
| `BossRoom` 打完龙无条件 `Environment.Exit(114514)` | **占位**：下一层地图还没做（用户已确认设计意图就是「打完 Boss 生成下一层」） |
| 只有 1 层（文档说 10-12 层） | 同上，缺的是「打完 Boss → 生成下一层」。**用户已决定：层数暂时留着不改** |
| 地图固定 10×10（文档 7×7~13×13） | **用户已裁定：不变**，文档与实现的不一致保留 |
| 药(20) / 咖啡(30) 的起始道具池状态 | 药**已进开局池**（用户裁定 6）：药已实现（配方 0：红磷 + 水瓶），池中给 **2 药 / 权重 1**（数量 2 按裁定 1 的「全部 +1」）；咖啡(30) 则**完全未实现**，**没进池子**（依赖雾都城市） |
| 岩浆怪身上的流血也被削 30% | **已知精度损失**：高温免疫在 `TakeDamage` 上生效，无法区分伤害来源。要精确区分需给伤害带来源标记（大改，未做） |
| `DOT` 字样还出现在若干 `.cs` 注释里 | **历史说明注释**（解释「DOT 机制已删除、改走流血 buff」），不是残留代码 |
| **换装备 / 开战时护盾清零** | **刻意的游戏设计**：换装等同重新整理防御姿态，战斗外攒的护盾随之失效。`Player.ResetShieldForTurn` 里的 `Shield = 0`（由 `ApplyEquipmentPassives` 在换装/开战时一并调用）是有意为之，**不要当 bug 挪走** |
| `Str` / `Atk` / `Def` 会被 `RecomputeStats` 覆盖（任何直接赋派生值的改动都只活到下一次重算） | 现算机制的**预期代价**：它们永远等于「基础值 + 当前所有修正」，任何直接赋值都会被下次重算冲掉。要「永久改」得走基础值（`GrowBaseStr` / `GrowBaseAtk` / `GrowBaseDef`），要「临时改」得走修正量（`env.StrModifier` / `buff.StrBonus` 等）—— 见 2.3 的「派生值 vs 资源」 |

**已不是问题（曾经列在这里）：**
- ~~岩浆怪 HP 15~~ → **已按文档改为 30**（中途一度写成 80，用户裁定 2 后已改回），并接上「熔岩瀑布高温免疫 30%」「水瓶扣当前 70% 血」
- ~~防御公式不符~~ → **已按文档把代码改为系数 0.5**（`Calculator.DefCoefficient`，用户裁定 3）：`DESIGN.md` 7.2 与 `Calculator.ApplyDefense` 现在一致。**困难模式的 1 还没实现**（缺难度系统）—— 等难度系统做出来时只需把这个常量改成按难度取值，调用点不用动（见 DESIGN 12.5）

---

# 四、当前关键数字（改数值前先对这里）

| 项目 | 值 | 依据 |
|---|---|---|
| 玩家初始 STR / ATK / DEF / HP | **3 / 20 / 5 / 200** | DESIGN 7.3（实现：`Player` 的 `BaseStr` / `BaseAtk` / `BaseDef` / `BaseHp`） |
| 攻击力公式 | `2 + STR × √ATK` | DESIGN 7.3 |
| 背包容量 | 初始 **15**，上限 **22** | DESIGN 8.1 |
| 饰品栏 | 初始 **5**，上限 **15** | DESIGN 8.2 |
| 熔岩瀑布水量 | 满 **200**，每战斗回合 **-5~10** | DESIGN 5.1 |
| 熔岩瀑布 STR 惩罚 | 每损失 1 点水 **-0.01** | DESIGN 5.1 |
| 水淹洞穴水位 | **15** 判负，50%+1 / 10%+2 / 40% 不变 | DESIGN 5.2 |
| 开局道具 | 池中随机 **5** 种选 1，共 **3** 轮 | DESIGN 2.2 |
| 开局道具的数量 | 代码 = DESIGN 2.2 表的**原值 +1**（用户裁定 1：保留 +1，以代码为准）。<br>✅ 已跟进（本次）：DESIGN §2.2 表已改写成 +1 后的**最终值**；两行硫磺的权重按用户裁定**改代码**修回 —— **2 硫磺 = 10 / 3 硫磺 = 8**（`BasicStartWeights = [10, 8, 8, 10, 5, 5, 5, 5, 1]`），逐项核对表见 DESIGN 12.4 末 | DESIGN 2.2 / 12.4 |
| 开局道具池 · 药 | 药(20) **已进池**：**2 药**，权重 **1** | DESIGN 2.2（用户裁定 6） |
| DEF 减免系数 | **0.5**（困难 1 待难度系统）；`Calculator.DefCoefficient`，最低取 1 仍生效 | DESIGN 7.2 / 12.5（用户裁定 3） |
| 岩浆怪血量 | **30** | DESIGN 六（用户裁定 2） |
