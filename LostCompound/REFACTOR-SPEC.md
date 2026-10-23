# LostCompound C# 地道化重构规范

本文件是重构的**唯一权威约定**。所有模块必须严格遵守，否则跨模块引用会对不上。

## 0. 基本信息

| 项 | 值 |
|---|---|
| 目标根 | `C:\Users\dayli\RiderProjects\LostCompound\LostCompound` |
| 目标框架 | `net10.0`（C# 13/14，支持主构造器、集合表达式等） |
| `ImplicitUsings` | `enable`（`System`、`System.Collections.Generic`、`System.Linq`、`System.Threading`、`System.IO` 自动可用，**不要**写这些 using） |
| `Nullable` | **仍为 `disable`**（本次不改可空标注） |

**验证命令**（每次改完后必须能编译）：
```
& "C:\Users\dayli\.dotnet\dotnet.exe" build
```

## 1. 铁律（违反即算失败）

1. **行为必须完全不变**。重构只改写法，不改逻辑、不改数值、不改输出文本。
2. **`+=` 累加语义绝不能变成覆盖**（见第 2 节，这是最危险的陷阱）。
3. 保留所有中文注释；新增注释用中文。
4. 必须能编译通过。

## 2. ⚠️ 最危险的陷阱：名字像 setter、实际是「累加」

以下方法**看起来是 setter，实际做的是 `+=` 累加**。它们**绝不能**变成属性（属性赋值是覆盖），必须重命名为 `Add...` 方法：

| 现名 | 实际实现 | 改成 |
|---|---|---|
| `Monster.setDOTdur(double v)` | `DOTdur += v` | `AddDotDuration(double v)` |
| `Monster.setDOTdmg(double v)` | `DOTdmg += v` | `AddDotDamage(double v)` |
| `Monster.addSkipTurn(int c)` | `skipTurn += c` | `AddSkipTurn(int c)` |
| `Player.addDOTdmg(double v)` | `DOTdmg += v` | `AddDotDamage(double v)` |
| `Player.addDOTdur(int v)` | `DOTdur += v` | `AddDotDuration(int v)` |

对比：以下这些**是真正的赋值**，可以安全变成属性：

| 现名 | 实际实现 | 改成属性 |
|---|---|---|
| `Player.setDOTdmg(int/double v)` | `DOTdmg = v` | `DotDamage`（两个重载合并为一个 `double` 属性） |
| `Player.setDOTdur(int v)` | `DOTdur = v` | `DotDuration` |
| `Player.setSkipTurn(int v)` | `skipTurn = v` | `SkipTurn` |
| `Player.setDeath(bool v)` | `isDeath = v` | `IsDead` |
| `Player.setStr(double v)` | `str = v` | `Str` |
| `Monster.setStr(double v)` | `str = v` | `Str` |
| `Monster.setAtk(double v)` | `atk = v` | `Atk` |

## 3. 命名映射表

### 3.1 枚举成员改 PascalCase（枚举**类型名**不变）

| 枚举 | 旧成员 | 新成员 |
|---|---|---|
| `ItemType` | `SULPHUR` `WOOD` `CHARCOAL` `WHITE_PHOSPHORUS` `RED_PHOSPHORUS` `SAND` `GLASS_JAR` | `Sulphur` `Wood` `Charcoal` `WhitePhosphorus` `RedPhosphorus` `Sand` `GlassJar` |
| `ItemType` | `FushionSluphur` | `FushionSluphur`（**拼写保持不变**，已是 Pascal 形式） |
| `MonsterType` | `DRAGON` `DUMMY` `LAVA_SLIME` `TWISTEDPAIR1` `TWISTEDPAIR2` `ZOMBIE` | `Dragon` `Dummy` `LavaSlime` `TwistedPair1` `TwistedPair2` `Zombie` |
| `RoomType` | `START` `BLANK` `FIGHT` `EMPTY` `CRAFT` `BOSS` | `Start` `Blank` `Fight` `Empty` `Craft` `Boss` |
| `EnvType` | `NONE` `LAVA_WATERFALL` | `None` `LavaWaterfall` |

**接口名与类名不变**：`Useable`、`Craftable`、`Changeable`、`TurnCount`、`Tools` 保持原名。

### 3.2 新增 `Direction` 枚举（替代 int 常量）

新建文件 `Rooms\Direction.cs`：

```csharp
namespace LostCompound.Rooms
{
    /// <summary>房间四个方向。数值与原 int 常量保持一致，便于数组索引。</summary>
    public enum Direction
    {
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3,
    }
}
```

- `Room` 里的 `UP/DOWN/LEFT/RIGHT` 四个 `const int` **删除**，改用 `Direction`。
- `Room` 的 `doors` 数组改为 `private readonly bool[] _doors = new bool[4];`，用 `(int)direction` 索引。
- `Room.isDoor(int)` → `HasDoor(Direction direction)`
- `Room.setDoor(int, bool)` → `SetDoor(Direction direction, bool hasDoor)`
- `TreeMazeGenerator` 里自己那四个 `const int UP/DOWN/LEFT/RIGHT` **也删除**，统一用 `Direction`。
- `Player.move(int)` → `Move(Direction direction)`；`Player.checkDirections()` 内部对 `HasDoor` 的调用改用枚举。
  - **注意**：菜单显示的文字 `"0.上"` `"1.下"` `"2.左"` `"3.右"` **必须保持不变**（那是给玩家看的输入编号）。
  - `Move` 里原来的 `if (direction < 0 || direction > 3)` 校验改为 `if (!Enum.IsDefined(direction))`。
  - 读取用户输入后要 `(Direction)direction` 转换，并保留原有的无效输入提示文本。
- `TreeMazeGenerator.dfs` 里 `List<int> dirs = ...; Shuffle(dirs, ...)` → `List<Direction>`，`Shuffle` 改为泛型 `Shuffle<T>(IList<T> list)`。
- `opposite` 映射改为 switch 表达式：
  ```csharp
  private static Direction Opposite(Direction d) => d switch
  {
      Direction.Up => Direction.Down,
      Direction.Down => Direction.Up,
      Direction.Left => Direction.Right,
      Direction.Right => Direction.Left,
      _ => Direction.Up,
  };
  ```
- `dRow`/`dCol` 偏移可保留为数组，用 `(int)dir` 索引：
  ```csharp
  private static readonly int[] DRows = [-1, 1, 0, 0];
  private static readonly int[] DCols = [0, 0, -1, 1];
  ```

### 3.3 `Item` 及其子类

**`Item` 改为记录式基类，用基类主构造器做「构造时赋值」：**

```csharp
public abstract class Item(int id, string name, int maxStack, bool stackable, int count)
{
    public int Id { get; } = id;
    public string Name { get; } = name;
    public int MaxStack { get; } = maxStack;
    public bool IsStackable { get; } = stackable;
    public int Count { get; set; } = count;          // 会被增减，必须可写
    public abstract ItemType? Type { get; }           // 保留「可能为 null」语义

    public override string ToString() => $"{Name} x{Count}";
}
```

| 旧成员 | 新成员 |
|---|---|
| `id` / `getId()` | `Id`（get-only 属性） |
| `name` / `getName()` | `Name`（get-only 属性） |
| `maxStack` / `getMaxStack()` | `MaxStack`（get-only 属性） |
| `Stackable` / `isStackable()` | `IsStackable`（get-only 属性，**方法删除**） |
| `count` / `getCount()` | `Count`（可读写属性） |
| `type` 字段 | **删除**（冗余；`Type` 由子类实现） |
| `getType()` | `abstract ItemType? Type { get; }` |

**子类改为主构造器 + 基类实参传递**，例如：

```csharp
public sealed class Sulphur(int count) : Item(1, "硫磺", 999, true, count)
{
    public override ItemType? Type => ItemType.Sulphur;
}
```

```csharp
public sealed class Charcoal(int count) : Item(3, "木炭", 99, true, count), TurnCount
{
    // 保留 Java 原本的「无类型」语义：返回 null
    public override ItemType? Type => null;
    public void OnBattleStart(Player p, EnvType envType) { }
    public void OnBattleEnd(Player p, EnvType envType) { }
}
```

各子类的构造参数（**必须与原来完全一致**）：

| 类 | 基类实参 (id, name, maxStack, stackable) | 额外成员 |
|---|---|---|
| `Sulphur` | `1, "硫磺", 999, true` | — |
| `Wood` | `2, "木头", 99, true` | `Change` 属性（默认 3）、`TurnCount`、`Changeable` |
| `Charcoal` | `3, "木炭", 99, true` | `TurnCount` |
| `FushionSluphur` | `4, "热硫", 99, true` | `Timer` 属性（默认 3）、`Changeable`、`Useable`、`TurnCount` |
| `WhitePhosphorus` | `5, "白磷", 99, true` | `Useable`、`Changeable` |
| `Sand` | `6, "沙子", 99, true` | — |
| `GlassJar` | `7, "玻璃罐", 99, true` | — |
| `RedPhosphorus` | `31, "红磷", 99, true` | `Useable` |

**接口方法改 PascalCase**（接口名不变）：

| 接口 | 旧 | 新 |
|---|---|---|
| `Useable` | `use(Player, List<Monster>)` | `Use(Player p, List<Monster> m)` |
| `Useable` | `ExpBattle(Player)` | `ExplainBattleOnly(Player p)` |
| `Craftable` | `getName()` | `Name`（属性） |
| `Craftable` | `getMaterials()` | `Materials`（属性，`int[][]`） |
| `Craftable` | `getProduct()` | `Product`（属性，`Item`） |
| `Craftable` | `canCraft(Player)` | `CanCraft(Player player)` |
| `Craftable` | `craft(Player)` | `Craft(Player player)` |
| `Craftable` | `getMaterialsStr(Player)` | `DescribeMaterials(Player player)` |
| `Changeable` | `ShouldRemove()` | `ShouldRemove()`（不变） |
| `Changeable` | `Become()` | `Become()`（不变） |
| `TurnCount` | `onBattleStart(Player, EnvType)` | `OnBattleStart(Player p, EnvType envType)` |
| `TurnCount` | `onBattleEnd(Player, EnvType)` | `OnBattleEnd(Player p, EnvType envType)` |

`ExpBattle` 的语义是「这是战斗外不能用的道具」，改名比直译更清楚。

### 3.4 `Monster` 及其子类

| 旧成员 | 新成员 |
|---|---|
| `hp` / `getHp()` | `Hp` 属性 |
| `atk` / `getAtk()` / `setAtk()` | `Atk` 属性 |
| `str` / `getStr()` / `setStr()` | `Str` 属性 |
| `def` / `getDef()` | `Def` 属性 |
| `name` / `getName()` | `Name` 属性 |
| `block` / `getBlock()` | ~~`Block` 属性（setter 用 `protected internal`）~~ **已删除**：格挡次数制整体改为护盾值制，见 `Shield` 属性 + `AddShield(double)` |
| `DOTdmg` / `getDOTdmg()` | `DotDamage` 属性 |
| `DOTdur` / `getDOTdur()` | `DotDuration` 属性 |
| `skipTurn` | `SkipTurn` 属性 |
| `monsterType` | `Type` 属性（类型 `MonsterType?`） |
| `isAlive()` | `IsAlive` **计算属性**：`public bool IsAlive => Hp > 0;` |
| `takeDmg(double)` | `TakeDamage(double dmg)`（抽象） |
| `onMonsterTurn(Player)` | `OnMonsterTurn(Player p)`（抽象） |
| `Attack(Player)` | `Attack(Player p)`（抽象，保持原名） |
| `rdChoice(int)` | `RollIndex(int count)`（`protected abstract`） |
| `die(Player)` | `Die(Player player)` |
| `decreaseDOTdur()` | `DecreaseDotDuration()` |
| `clearDOT()` | `ClearDot()` |
| `tryConsumeSkipTurn()` | `TryConsumeSkipTurn()` |
| `ToString()` | `public override string ToString() => $"{Name}({Math.Max(Hp, 0):F1}) ";` |

**⭐ 合并重复代码（用户明确要求）**：
`Zombie`、`Dragon`、`Dummy`、`LavaSlime`、`TwistedPairP1`、`TwistedPairP2` **六个子类里的 `isAlive()` 实现完全相同**（都是 `return hp > 0;`）。
→ 改为基类 `Monster` 上的一个非虚计算属性 `IsAlive`，**把这 6 个重写全部删除**。
（`Player` 也有 `isAlive()`，但那属于 `Player` 类，改为 `Player.IsAlive` 属性即可。）

**⭐ `Random` 字段改为 `Random.Shared`（用户明确要求）**：
删除以下所有字段，调用处改用 `Random.Shared`：
- `Zombie.rd`、`Dragon.rd`、`TwistedPairP1.rd`、`TwistedPairP2.rd`
- `MobSpawner.random` 和 `MobSpawner.rd`
- `StartRoom` 里的 `Random random = new Random();`
- `TreeMazeGenerator.random` → 改为使用 `Random.Shared`（其构造器里接收 `Random` 的重载可保留，或一并简化；**若保留重载，默认走 `Random.Shared`**）

`rd.Next(i)` → `Random.Shared.Next(i)`；`rd.Next(i) + 1` → `Random.Shared.Next(i) + 1`；
`(int)(Random.Shared.NextDouble() * i)` 保持原取整方式。

**Monster 子类构造时赋值**：子类构造器里逐条赋值改为基类构造器参数或属性初始化器。例如 `Dragon`：

```csharp
public Dragon()
{
    Name = "龙";
    Hp = 80;
    Atk = 10;
    Str = 0.0;
    Def = 5;
    Type = MonsterType.Dragon;
}
```
（若采用基类主构造器更清晰，也可；**但各数值必须与原来完全一致**。）

### 3.5 `Room` 及其子类

| 旧成员 | 新成员 |
|---|---|
| `type` / `getRoomType()` | `RoomType Type { get; }` |
| `EnvType` 字段 | `Environment`（属性，类型 `EnvType`） |
| `doors` 字段 | `private readonly bool[] _doors = new bool[4];` |
| `visited` / `isVisited()` / `setVisited()` | `IsVisited` 属性 |
| `cleaned` | `IsCleaned` 属性 |
| `playerOnHere` / `isPlayerOnHere()` / `setPlayerOnHere()` | `IsPlayerHere` 属性（setter 可 `private`，但 `enterSetup`/`leaveSetup` 要用，保持 `public` 更省事） |
| `isDoor(int)` | `HasDoor(Direction direction)` |
| `setDoor(int, bool)` | `SetDoor(Direction direction, bool hasDoor)` |
| `hasFreeDoor()` | `HasFreeDoor()` |
| `onEnter(Player)` | `OnEnter(Player p)`（抽象） |
| `onLeave(Player)` | `OnLeave(Player p)`（virtual） |
| `enterSetup(Player)` | `EnterSetup(Player p)` |
| `leaveSetup(Player)` | `LeaveSetup(Player p)` |
| `UP/DOWN/LEFT/RIGHT` | **删除**，改用 `Direction` |

**Room 子类构造器**：`base(RoomType.XXX, envType)` 里的枚举成员名要跟着改（`RoomType.Start` 等），
并把 `this.EnvType = ...` 之类改为基类构造器传参。

其他映射：

| 类 | 旧 | 新 |
|---|---|---|
| `StartRoom` | `giveStartItems(Player)` | `GiveStartItems(Player p)` |
| `StartRoom` | `getStartItemArray()` | `CreateStartItems()` |
| `StartRoom` | `startItemsGiven` | `_startItemsGiven` |
| `EmptyRoom` | `possibleItems` | `_possibleItems` |
| `MobSpawner` | `spawn(EnvType)` | `Spawn(EnvType envType)` |
| `LootTables` | `getLootTable(EnvType)` | `GetLootTable(EnvType envType)` |

### 3.6 `Maps\TreeMazeGenerator`

| 旧 | 新 |
|---|---|
| `generate()` | `Generate()` |
| `getGrid()` | `Grid`（属性，`Room[][]`） |
| `getRoom(int,int)` | `GetRoom(int row, int col)` |
| `DebugMap()` | `PrintDebugMap()` |
| `printMapToString()` | `ToDisplayString()` |
| `initializeRooms(EnvType)` | `InitializeRooms(EnvType envType)` |
| `dfs(...)` | `Carve(...)` |
| `isValid(int,int)` | `IsInside(int row, int col)` |
| `getRoomTypeChar(RoomType)` | **删除**（与 `getRoomTypeLabel` 重复）；先确认无调用方 |
| `getRoomTypeLabel(RoomType)` | `GetRoomTypeLabel(RoomType type)` |
| `setVisible(...)` | `MarkVisible(...)` |
| `setCross(...)` | `SetCross(...)` |
| `Shuffle(List<int>, Random)` | `Shuffle<T>(IList<T> list)`（用 `Random.Shared`） |
| `rows` / `cols` | `_rows` / `_cols`（或保留为 get-only 属性 `Rows`/`Cols`，若外部用到） |

**⭐ 合并重复代码**：`DebugMap` 里内联的 switch 表达式 与 `getRoomTypeLabel`/`getRoomTypeChar` 有三个**完全相同的 `RoomType` → 三字符标签**映射，合并为一个 `GetRoomTypeLabel(RoomType type)`，所有地方复用它。

### 3.7 `Tools` 模块（类名 `Tools` 不变）

| 类 | 旧 | 新 |
|---|---|---|
| `Calculator` | `Attack(double,double)` | `CalculateAttack(double str, double atk)`（避免与怪物 `Attack` 混淆） |
| `Calculator` | `Define(double,double)` | `ApplyDefense(double def, double dmg)` |
| `Tools` | `Fight(...)` | `RunBattle(Player p, List<Monster> monsters, EnvType envType)` |
| `Tools` | `roundSetup(...)` | `ApplyDotEffects(Player p, List<Monster> monsters, int round)` |
| `Tools` | `cleanupDead(...)` | `RemoveDeadMonsters(Player p, List<Monster> monsters)` |
| `Recipe` | `getName/getMaterials/getProduct/canCraft/craft/getMaterialsStr` | 见 3.3 的 `Craftable` 映射 |
| `MonsterLootTable` | `drop(MonsterType?)` | `Drop(MonsterType? type)` |
| `GameLauncher` | `printBanner()` | `PrintBanner()` |
| `GameLobby` | `start()` | `Run()` |
| `GameLobby` | `printMenu()` | `PrintMenu()` |
| `GameLobby` | `handleChoice(int)` | `HandleChoice(int choice)` |
| `GameLobby` | `startRun()` | `StartNewRun()` |
| `GameLobby` | `exit()` | `ConfirmExit()` |
| `GameLobby` | `readChoice()` | `ReadChoice()` |
| `GameLobby` | `running` | `_running` |
| `demo01`/`demo02` | `Run(string[])` | 保持不变 |
| `TwistedPairState` | `p1AttackedThisTurn` / `p2AttackedThisTurn` | `P1AttackedThisTurn` / `P2AttackedThisTurn`（属性） |
| `TwistedPairState` | `checkAndResetIfBothTrue()` | `ResetIfBothActed()` |

**`Player` 映射**（最复杂，重点）：

| 旧 | 新 |
|---|---|
| `hp` | `Hp` 属性 |
| `MaxHp` | **删除**（原本就从未使用，留一个 CS0414 警告；删除后行为不变） |
| `atk` | `Atk` 属性 |
| `str` / `getStr()` / `setStr()` | `Str` 属性 |
| `def` | `Def` 属性 |
| `name` | `Name` 属性 |
| `block` | ~~`Block` 属性~~ **已删除**：改为 `Shield` 属性（`double`）+ `AddShield(double)` |
| `DOTdmg` / `getDOTdmg()` | `DotDamage` 属性 |
| `DOTdur` / `getDOTdur()` | `DotDuration` 属性 |
| `skipTurn` / `getSkipTurn()` / `setSkipTurn()` | `SkipTurn` 属性 |
| `isDeath` / `setDeath()` | `IsDead` 属性 |
| `debug` | `_debug` |
| `map` / `playerRow` / `playerCol` | `_map` / `PlayerRow` / `PlayerCol`（行列改为 get-only 属性） |
| `Items` 字段 | `Inventory` 属性（`List<Item>`，get-only，外部只读） |
| `WillAdd` 字段 | `PendingItems`（get-only） |
| `AllRecipe` 字段 | `Recipes`（get-only） |
| `addItem(Item)` | `AddItem(Item newItem)` |
| `isAlive()` | `IsAlive` 属性 |
| `takeDmg(double)` | `TakeDamage(double dmg)` |
| `Attack(List<Monster>)` | `Attack(List<Monster> monsters)` |
| `selectTarget(List<Monster>)` | `SelectTarget(List<Monster> monsters)` |
| `openItems(List<Monster>)` | `OpenItems(List<Monster> monsters)` |
| `handleChangeableItems(...)` | `HandleChangeableItems(...)` |
| `countItemById(int)` | `CountItemById(int id)` |
| `consumeItem(int,int)` | `ConsumeItem(int id, int amount)` |
| `getItemNameById(int)` | `GetItemNameById(int id)` |
| `AllRecipeUnlock()` | `UnlockAllRecipes()` |
| `addRecipe0()` | `AddDefaultRecipes()` |
| `handCraft()` | `HandCraft()` |
| `enterMap(...)` | `EnterMap(...)`（两个重载） |
| `checkDirections()` | `ShowAvailableDirections()` |
| `move(int)` | `Move(Direction direction)` |
| `JoinMap(Room[][])` | `JoinMap(Room[][] map)` |
| `startGame(Room[][], TreeMazeGenerator)` | `StartGame(Room[][] map, TreeMazeGenerator tmg)` |
| `onPlayerTurn(...)` | `OnPlayerTurn(...)` |
| `getItems()` | **删除**（`Inventory` 属性已替代） |

## 4. 应采用的 C# 语法糖

1. **属性**替代 getter/setter（见上；累加器除外）。
2. **表达式体成员**：`public bool IsAlive => Hp > 0;`、`public void X() => ...;`
3. **字符串插值**：`"你受到" + dmg + "点伤害"` → `$"你受到{dmg:F1}点伤害"`。
   注意保留原有格式（`{0:F1}` 这类 `Console.WriteLine` 格式串可保留，也可改插值，但**输出必须逐字符一致**）。
4. **switch 表达式**替代 int/枚举 switch 语句（已存在的地方保持）。
5. **模式匹配**：`if (x is Useable u) u.Use(...)`。
6. **集合表达式**（C# 12）：`new int[] { 5, 1 }` → `[5, 1]`；`new int[][] { ... }` → `[[5, 1], [3, 1]]`。
7. **目标类型 `new`**：`List<int> x = new();`。
8. **`readonly` 字段**、**`private` 字段命名 `_camelCase`**。
9. **`var`** 用于类型显而易见的局部变量。
10. **`nameof`** 用于日志/异常中的成员名。
11. **`sealed`** 加在没有子类的类上（`Sulphur`、`Wood` 等 Item 子类；怪物子类也可）。
12. **`Environment.Exit(114514)`** 保持不变。

## 5. 产出要求

每个模块完成后报告：
- 改动的文件清单
- **任何你拿不准或偏离本规范的地方**（逐条列出）
- 被删除的成员（如冗余重写、未使用方法）及删除依据

**不要运行构建**（由主任务统一编译验证）。
