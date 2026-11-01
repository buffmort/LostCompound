# 存档 / 继续游戏 —— 实施方案

> §0 状态：**已实现（2026-10-31）**。与本文的差异如下：
> · 存档路径 `%APPDATA%\LostCompound\save.json`、单槽、死亡删档 —— 按默认值执行
> · 主菜单：有档时 `1 继续游戏 / 2 开始新游戏 / 3 退出游戏`，debug 项顺延到 4/5
> · 新增 `Tools\Players\Player.Save.cs`（读档入口）与 `Player.Tier1RecipesUnlocked`
> · `RoomData.PlayerHere` 最终**没有**存：它是「玩家坐标」的派生结果，
>   `ContinueGame` 按坐标重新打标记，存了反而有「两个 * 」的隐患
> · 验证结果见文末「八、验证结果」

> 目标：**对局退出时保存；下次启动主菜单多一项「继续游戏」。**
> 本文是动手前的勘察结果 + 方案 + 待拍板项。代码一行没改。

---

## 一、范围（重要）

本方案只支持**一局中途**的存档。原因是**层数系统还没做**：现在一局的全部内容就是
`TreeMazeGenerator(10, 10, envType)` 生成的一张地图，所以「存一整局」= 「存这一张地图」。

DTO 里预留 `Layer` 字段，等层数系统落地再扩成「第 N 层 + 已通关层列表」，现在做不会白做。

---

## 二、需要持久化的全部状态（已逐项核对源码）

### 2.1 玩家（`Tools\Players\*`）

| 状态 | 位置 | 说明 |
|---|---|---|
| `Hp` / `Shield` | `Player.cs` | 资源，必须原样存 |
| `BaseHp` / `BaseStr` / `BaseAtk` / `BaseDef` | `Player.cs` | 永久成长的载体 |
| `Name` / `IsDead` | `Player.cs` | |
| `InventoryCapacity` / `AccessorySlots` | `Player.Inventory.cs` / `Player.Equipment.cs` | |
| `FightCount` / `SkipTurn` | `Player.Combat.cs` / `Player.Buffs.cs` | |
| `PlayerRow` / `PlayerCol` | `Player.Map.cs` | 存档后要站在原位 |
| `Inventory` / `Accessories` | 同上 | 每件：ID + Count + **额外状态**（见 2.2） |
| `Buffs` | `Player.Buffs.cs` | 每个：ID + Stacks + Turns，`BuffFactory` 能造 |

**不存**：`Str` / `Atk` / `Def`（派生值）—— 读档最后调一次 `RecomputeStats()` 就回来了，
存了反而可能与来源不一致（CONVENTIONS §2.3「重算制」）。

**不存**：`Recipes` —— 里面是 `Func<Item>`，不可序列化。
改为存「解锁到第几档」（配方 0 / 配方 1），读档后调 `AddDefaultRecipes()` / `AddTier1Recipes()`。

### 2.2 道具的「额外状态」—— 最容易漏的一环

`ItemFactory.Create(id, count)` 只能还原 **ID + 数量**。下面 5 个类的内部状态会丢：

| 道具 | 字段 | 初值 |
|---|---|---|
| `Wood` | `Change` | 3（倒计时，烤成木炭） |
| `Pickaxe` | `Durability` | 20 |
| `Cloak` | `_damageTakenCount` | 0（受伤 50 次破损） |
| `IodineTablet` | `Durability` | 3 |
| `Medicine` | `Durability` | 3（一颗药 3 次） |

**方案**：新增能力位接口 `Items\Capabilities\IItemState.cs`，这 5 个类各实现一次：

```csharp
public interface IItemState
{
    Dictionary<string, int> SaveState();             // 空字典 = 没有额外状态
    void LoadState(IReadOnlyDictionary<string, int> state);
}
```

与项目现有风格一致（`IChangeable` / `ITurnCount` / `IDamageReduction` 都是这个套路），
`SaveCollector` 只问接口，不认识具体道具类。

### 2.3 地图（`Room[][]`）

每个格子要存：

| 字段 | 位置 |
|---|---|
| `Type`（`RoomType` 枚举） | `Room.Type`（只读，靠重建） |
| 4 扇门 | `Room._doors`（有 `SetDoor`，但没有读取口） |
| `IsVisited` / `IsCleaned` / `IsPlayerHere` | `Room`（可写属性） |

**三个缺口**：

1. **没有 `RoomFactory`** —— 现在只有 `TreeMazeGenerator` 里那几条 `new XxxRoom(env)`。
   读档必须能「按 `RoomType` 造房间」，所以要补一个。
2. **`StartRoom` 有私有字段 `_startItemsGiven`**（开局道具只发一次），读档要恢复它，
   否则读档后会**再发一遍开局道具**。
3. **`_doors` 没有读取口** —— 存档要读、重建要写，`SetDoor` 已有，还缺一个 `GetDoors()`（返回 4 位掩码）。

### 2.4 环境（`MapEnvironment\`）

同一层所有房间**共享同一个环境实例**（`TreeMazeGenerator.InitializeRooms` 里创建一次）。

| 环境 | 要存的状态 |
|---|---|
| `LavaWaterfall` | `Water` |
| `FloodedCavern` | `WaterLevel`、`_pluggedTurns` |

**方案**：环境基类加两个 `virtual` 空实现（能力位）：`SaveState()` / `LoadState(Dictionary<string,int>)`，
两个环境各自覆写。这样以后加新环境只写自己的覆写，不用回头改存档代码。

### 2.5 明确不存的东西

| 不存 | 理由 |
|---|---|
| `Str` / `Atk` / `Def` | 派生值，重算即可 |
| 战利品、怪物、回合数 | **战斗中的状态不存**（见第四节「时机」） |
| `Random` 状态 | 地图已定型，不需要重现 |
| `_screen` / `_statusHeader` / `_inputClosed` | 纯运行期 UI 状态 |
| `DebugState` | 全局开关，与存档无关 |

---

## 三、新增文件

```
LostCompound\
  Save\
    SaveData.cs        纯 DTO（只有字段，没有 Func / 没有控制台输出）
    SaveFile.cs        路径、读写、版本号、坏档兜底
    SaveCollector.cs   运行时状态 → DTO
    SaveRestorer.cs    DTO → 运行时状态
  Rooms\RoomFactory.cs
  Items\Capabilities\IItemState.cs
```

**序列化格式：JSON（`System.Text.Json`，.NET 内置，零依赖、零新增包）。**

文件头带版本号：

```json
{ "Version": 1, "SavedAt": "2026-10-31T21:00:00", ... }
```

**版本不符 / JSON 解析失败 / 文件损坏 → 丢弃存档并提示一句，绝不崩。**
（这与 `ItemFactory.Create` 遇到未知 ID 返回 `null` 是同一种容错态度。）

---

## 四、存档时机（关键设计）

**只在「非战斗、非房间事件中」存档。** 战斗中存了也恢复不了 —— 敌人列表、怪物血量、
本回合盾、buff 剩余回合、当前是第几回合，全都没有对应的恢复入口。

| 触发 | 行为 |
|---|---|
| 方向菜单输入 `4`（现有退出） | **存档并退出** |
| 玩家死亡 / 环境判负 | **删档**（这一局结束了） |
| 窗口被强杀 / 断电 | 不保证（不做自动定时存档） |
| 战斗中想退出 | 提示「战斗结束后才能保存」，退回菜单，**不覆盖旧档** |

---

## 五、待你拍板的 5 件事（括号里是我的默认值）

1. **存档路径** —— `%APPDATA%\LostCompound\save.json`（换 exe 位置不丢）／或 exe 同目录
2. **槽位** —— 单槽，每次覆盖 ／ 多槽
3. **主菜单编号** —— 有存档时：`1 开始游戏 / 2 继续游戏 / 3 退出游戏`
   （⚠️ 会挤动现有编号；也可以把「继续」放在最前面）
4. **死亡是否删档** —— 删 ／ 保留（下次还能读档回到死前）
5. **战斗中途退出** —— 禁止保存、提示退回 ／ 允许「保存到战斗开始前」

---

## 六、明天的实施顺序（每步都编译得过）

1. `IItemState` + 5 个道具类实现（不碰 `ItemFactory`）
2. `RoomFactory` + `Room.GetDoors()` + `StartRoom` 的恢复入口
3. `Environment.SaveState/LoadState`（两个环境各覆写）
4. `SaveData` / `SaveFile`（JSON + 版本 + 坏档兜底）
5. `SaveCollector` / `SaveRestorer`
6. `Player.StartGame` 拆出「从已有状态开局」的入口（读档复用同一套主循环）
7. 主菜单加「继续游戏」
8. 验证：存 → 退 → 重进 → 逐项对比；再造坏档 / 旧版本 / 无档三条路径各试一次

---

## 七、已知风险

| 风险 | 处理 |
|---|---|
| `TreeMazeGenerator._grid` 是 `readonly`，读档后画不出地图 | 加 `FromGrid(Room[][])` 工厂，跳过生成、直接注入网格 |
| `Player` 大量 `private set` | 读档走 `internal` 恢复方法，**不要为了存档把它们改成 `public set`**（那等于把「只能通过 GrowBaseXxx 改」这条纪律废掉） |
| 层数系统未实现 | DTO 预留 `Layer`，本方案只存单张地图 |
| 读档后 `_startItemsGiven` 丢失 → 重复发开局道具 | 2.3 已列为必做项 |
| `Item` 的 `MaxStack` 合并 | 读档时**逐个 `new` 再直接塞进列表**，不要走 `AddItem`（会触发合并、容量检查、播报） |

---

## 八、验证结果（2026-10-31）

| 验证 | 方法 | 结果 |
|---|---|---|
端到端 | 脚本输入：开局 → 起始三选一 → 方向菜单 `4` → 主菜单出现「继续游戏」→ `1` 读档 | 存档生成（19 KB）、读档成功、地图重绘 |
**读写往返** | 存档 → 读档 → 立刻再存 → 逐字节比对（抹掉时间戳） | **完全一致** |
实例状态 | 探针程序：木头 `Change=1`、斗篷承伤 3、药 ×2、流血 5/2、`Hp=123.5` | 读档后全部原值 |
派生值 | 基础 DEF 5 + 斗篷 5 → 读档后 `RecomputeStats()` | `Def=10`（没有把 10 存进档） |
地形 | 10×10 共 100 格的门掩码求和 / 已访问数 | `756→756`、`1→1` |
无档回归 | 删档后启动 | 菜单仍是 `1 开始游戏 / 2 退出游戏` |
坏档/旧版本 | 由 `SaveFile.Load` 的三条拒绝分支覆盖（JSON 异常 / 版本不符 / 文件为空） | 打印原因并返回主菜单，不崩 |

**已知未覆盖**：死亡删档这条分支没有自动化验证（需要把玩家打死），
代码上是 `RunMainLoop` 退出后 `if (!IsAlive) SaveFile.Delete()`，逻辑单薄，人工试一局即可。

---

## 九、v2：改成「种子 + 进度」（已按Hyberty方案落地）

Hyberty提出的方案：**只存种子，生成时把进度坐标点亮，再把玩家塞进去。**

落地要点：

| 项 | 做法 |
|---|---|
种子 | `TreeMazeGenerator(rows, cols, envType, seed)`，内部 6 个随机点全走 `_rng = new Random(seed)` |
新开局 | `GameLobby` 摇 `Random.Shared.Next()` 并打印「本局种子：…」 |
地形 | **不存**，读档时同种子重算 |
进度 | `Visited` / `Cleaned` 压平成 `row * Cols + col`，只记变了的格子 |
起始房间 | 单独一个 `StartItemsGiven` bool（整层只有一个起点） |
玩家落位 | 新增 `Player.JoinMap(map, row, col)` 重载，直接落在目标格（不再「先找起点再挪」） |
防护 | `GeneratorVersion` 不符 → 明确拒绝，避免「同种子算出另一张图」 |

顺带删掉的死代码：`RoomFactory`（改用种子重建后不需要按类型造房间）、
`Room.GetDoors/SetDoors`（地形由种子还原，不再存门掩码）、`TreeMazeGenerator.FromGrid`、
`Player.RestorePosition`（被 `JoinMap` 重载取代）。

**结果：存档 15.9 KB → 0.96 KB。**
验证（探针）：
- 同种子两次生成 → 房间类型 + 四向门逐格一致；换种子 → 不一致
- 地形还原一致、水位 163、坐标 (3,4)、该格 `IsPlayerHere=True`
- 已访问 4（3 个手标 + 起始房间自带 `IsVisited=true`）、已搜刮 1
- 木头 `change=2`、斗篷承伤 2、`Hp=88.5` 原值
- 存档 → 读档 → 再存，抹掉时间戳后逐字节一致
- 端到端：开局存档 0.96 KB → 主菜单「继续游戏」→ 读档 → 再存，仍一致