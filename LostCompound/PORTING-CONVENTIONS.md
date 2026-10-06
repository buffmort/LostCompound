# LostCompound Java → C# 移植约定

本文件是移植的**唯一权威约定**。所有模块必须严格遵守，否则跨模块引用会对不上。

## 0. 基本信息

| 项 | 值 |
|---|---|
| Java 源根 | `C:\Users\dayli\IdeaProjects\LostCompound\src` |
| C# 目标根 | `C:\Users\dayli\RiderProjects\LostCompound\LostCompound` |
| 目标框架 | `net10.0`（已配好，`.csproj` 会自动递归包含所有 `.cs`） |
| `ImplicitUsings` | `enable`（`System`、`System.Collections.Generic`、`System.Linq`、`System.Threading`、`System.IO` 已自动可用，**不必**写这些 using） |
| `Nullable` | `disable`（本阶段不做可空引用类型标注，避免一堆 CS8618 警告） |

**注意**：`ImplicitUsings` 已开启，所以 **不要** 写 `using System;`、`using System.Collections.Generic;`、`using System.Linq;`。只写本项目内部的 `using`（例如 `using LostCompound.Items;`）。

## 1. 目录与命名空间映射

Java 包名 → C# 命名空间，Java 目录 → C# 子目录：

| Java 包 | C# 命名空间 | C# 目录 |
|---|---|---|
| `items` | `LostCompound.Items` | `Items\` |
| `monsters` | `LostCompound.Monsters` | `Monsters\` |
| `Rooms` | `LostCompound.Rooms` | `Rooms\` |
| `Maps` | `LostCompound.Maps` | `Maps\` |
| `MapEnvironment` | `LostCompound.MapEnvironment` | `MapEnvironment\` |
| `tools` | `LostCompound.Tools` | `Tools\` |

C# 命名空间用**块式**（`namespace X { ... }`），与现有 `HelloWorld01` 风格一致，不用文件作用域 `namespace X;`。

每个文件开头按需写内部 using，例如 `Rooms\Room.cs` 里：

```csharp
using LostCompound.MapEnvironment;
using LostCompound.Tools;
```

## 2. 命名规则（重要）

**本阶段保留 Java 的原始标识符，不做 C# 风格重命名。**

- 字段名原样保留：`hp`、`atk`、`str`、`def`、`name`、`block`、`skipTurn`、`monsterType`、`DOTdmg`、`DOTdur`
- 方法名原样保留：`getHp()`、`setStr()`、`isAlive()`、`isStackable()`、`isDoor()`、`takeDmg()`、`onMonsterTurn()`、`addItem()`、`countItemById()`
- 枚举成员名原样保留：`SULPHUR`、`LAVA_SLIME`、`TWISTEDPAIR1`、`START`、`FIGHT`
- 常量名原样：`UP`、`DOWN`、`LEFT`、`RIGHT`

这样做的原因：移植是机械翻译，先保证「能编译、行为一致」，**重命名成 C# 习惯（属性、PascalCase）留作第二阶段单独做**，那时才有对照价值。

**唯一必须改的**：`toString()` → `public override string ToString()`（C# 要求这个确切签名）。

## 3. Java → C# 构造逐条映射

### 类型与集合

| Java | C# |
|---|---|
| `String` | `string` |
| `boolean` | `bool` |
| `ArrayList<T>` | `List<T>` |
| `new ArrayList<>()` | `new List<T>()`（**显式写出类型参数**，别用 `new()`，减少歧义） |
| `T[]` | `T[]` |
| `T[][]` | `T[][]`（**保持交错数组**，不要改成 `T[,]`） |
| `int[][]` | `int[][]` |
| `Map<K,V>` | `Dictionary<K,V>` |
| `Math.max(a,b)` / `Math.min` / `Math.sqrt` | `Math.Max` / `Math.Min` / `Math.Sqrt`（**C# 大写首字母**） |
| `Math.round(x)` | `Math.Round(x)` |
| `Math.abs` | `Math.Abs` |

### 数组长度

Java `arr.length` → C# 数组用 `arr.Length`。

注意交错数组：Java `map.length` → `map.Length`；Java `map[0].length` → `map[0].Length`。

### 修饰符与声明

| Java | C# |
|---|---|
| `package x;` | `namespace LostCompound.X { }` |
| `import a.b.C;` | `using LostCompound.A.B;` |
| `import static ...EnvType.LAVA_WATERFALL;` | `using static LostCompound.MapEnvironment.EnvType;` |
| `public class A extends B implements I` | `public class A : B, I` |
| `abstract class` | `abstract class` |
| `interface` | `interface` |
| `@Override` | `override` |
| `final` 实例字段 | `readonly`（仅当在构造函数中赋值） |
| `static final` 字段 | `static readonly` |
| `final` 局部变量 | 直接去掉 `final` |
| `public` / `protected` / （默认） | `public` / `protected` / `internal`（Java 包级私有 ≈ `internal`，但**若不确定就保持原样用 `public`**，宁可放宽可见性也不要造成引用失败） |

### 方法

- `abstract` 方法 → `public abstract`（C# 抽象成员默认 private，**必须显式写 `public`/`protected`**）
- 接口方法 → 不写修饰符（隐式 public）
- 重写 → `public override`
- 构造函数用类名，与 Java 相同

#### **`virtual` 与 `override`（最关键，漏了必报 CS0506）**

**Java 的方法默认就是虚方法**，子类写 `@Override` 即可覆盖。
**C# 的方法默认不是虚方法**，基类必须**显式**写 `virtual`，子类才能 `override`。

因此：**凡是 Java 子类 `@Override` 覆盖的「具体（非 abstract）方法」，其 C# 基类声明都必须加 `virtual`。**

| Java 基类 | C# 基类 |
|---|---|
| `public void onLeave(Player p) { ... }`（被子类覆盖） | `public virtual void onLeave(Player p) { ... }` |
| `public boolean isAlive() { ... }`（被子类覆盖） | `public virtual bool isAlive() { ... }` |
| `public abstract void onEnter(Player p);` | `public abstract void onEnter(Player p);` |

漏写 `virtual` 会让子类的 `override` 报 **CS0506**。
（`override` 一个 `abstract` 成员、或 `override` 一个 `override` 成员则始终合法，无需额外处理。）

### `object` 相关

| Java | C# |
|---|---|
| `toString()` | `public override string ToString()` |
| `instanceof T` | `is T` |
| `(T) obj` | `((T)obj)` |
| 推荐写法 `if (x instanceof T) { ((T)x).m(); }` | `if (x is T t) { t.m(); }` |

### 输出与格式化（**容易错，重点看**）

| Java | C# |
|---|---|
| `System.out.println(x)` | `Console.WriteLine(x)` |
| `System.out.println()` | `Console.WriteLine()` |
| `System.out.print(x)` | `Console.Write(x)` |
| `System.out.printf("血量:%.1f%n", hp)` | `Console.WriteLine("血量:{0:F1}", hp)` |
| `String.format("%.1f", x)` | `x.ToString("F1")` |
| `String.format("[%d] %s", a, b)` | `string.Format("[{0}] {1}", a, b)` 或插值 `$"[{a}] {b}"` |
| `sb.append(x)` | `sb.Append(x)` |
| `new StringBuilder()` | `new StringBuilder()` |
| `sb.toString()` | `sb.ToString()` |

**Java 的 `%.1f` / `%d` / `%s` / `%n` 在 C# 里全部无效**，必须转成 `{0:F1}` / `{0}` / `{1}` 形式。

### 输入（Scanner）

C# 没有 `Scanner`。原代码里 `Scanner sc = new Scanner(System.in);` 这类字段**直接删除**。

| Java | C# |
|---|---|
| `sc.nextInt()` | `int.Parse(Console.ReadLine())` |
| `sc.nextLine()` | `Console.ReadLine()` |

（`Nullable` 已关闭，`Console.ReadLine()` 返回值不会产生警告。）

若原代码用 `Scanner scanner = new Scanner(System.in);` 局部变量，删除该变量声明，把 `scanner.nextInt()` 换成上面的写法。

### 迭代器与「边遍历边删除」

C# 没有 `Iterator.remove()`。Java 中形如：

```java
Iterator<Item> iter = list.iterator();
while (iter.hasNext()) {
    Item item = iter.next();
    if (cond) { iter.remove(); }
}
```

C# 用**正向索引 + `RemoveAt` 后回退索引**（保持与原 Java 相同的正向顺序语义）：

```csharp
for (int i = 0; i < list.Count; i++) {
    Item item = list[i];
    if (cond) { list.RemoveAt(i); i--; }
}
```

不需要删除时，直接用 `foreach (Item item in list)`。

### 异常

| Java | C# |
|---|---|
| `throws X` | 删除（C# 不声明受检异常） |
| `try { } catch (InterruptedException e) { e.printStackTrace(); }` | `try { } catch (Exception) { }` 或捕获 `ThreadInterruptedException` |
| `throw new IllegalStateException("...")` | `throw new InvalidOperationException("...")` |
| `NumberFormatException` | `FormatException` |
| `e.printStackTrace()` | `Console.WriteLine(e)` |

### 线程

| Java | C# |
|---|---|
| `Thread.sleep(ms)` | `Thread.Sleep(ms)` |
| `Integer.parseInt(s)` | `int.Parse(s)` |
| `str.equalsIgnoreCase("y")` | `string.Equals(str, "y", StringComparison.OrdinalIgnoreCase)` |
| `str.trim()` | `str.Trim()` |
| `str.isEmpty()` | `str.Length == 0`（或 `string.IsNullOrEmpty(str)`） |

### 枚举

四个枚举（`ItemType`、`MonsterType`、`RoomType`、`EnvType`）都是**无字段的简单枚举**，直接翻译：

```csharp
namespace LostCompound.Items
{
    public enum ItemType
    {
        SULPHUR,
        WOOD,
        CHARCOAL,
        FushionSluphur,
        WHITE_PHOSPHORUS,
        RED_PHOSPHORUS,
        SAND,
        GLASS_JAR,
    }
}
```

枚举成员名**原样保留**（包括 `FushionSluphur` 这个拼写）。

### Random（java.util.Random）

| Java | C# |
|---|---|
| `import java.util.Random;` | **删除**（`System` 已隐式 using，直接用 `Random`） |
| `new Random()` | `new Random()` |
| `rd.nextInt(n)` | `rd.Next(n)` |
| `rd.nextDouble()` | `rd.NextDouble()` |
| `rd.nextBoolean()` | `rd.Next(2) == 0` |
| `Math.random()` | `Random.Shared.NextDouble()` |
| `private final Random rd = new Random();` | `private readonly Random rd = new Random();` |
| `private static final Random random = new Random();` | `private static readonly Random random = new Random();` |
| `Random rd = new Random();`（无修饰符） | `public Random rd = new Random();`（见下方「包级私有」规则） |

`(int)(Math.random() * i)` → `(int)(Random.Shared.NextDouble() * i)`。

**重要**：**保留原有的取整/取模方式**。原代码是 `nextInt(i)` 才用 `rd.Next(i)`；原代码是 `(int)(Math.random()*i)` 就保持 `(int)(Random.Shared.NextDouble()*i)`，不要擅自改成 `Next(i)`。

### 箭头 switch（Java 14+ 新语法）

Java:
```java
String label = switch (type) {
    case START -> " S ";
    case BOSS  -> " B ";
    default    -> " . ";
};
```
C# 用 **switch 表达式**（C# 8+）：
```csharp
string label = type switch
{
    RoomType.START => " S ",
    RoomType.BOSS  => " B ",
    _              => " . ",
};
```
注意：C# 的 `default` 写作 `_`；每个分支用 `,` 结尾；枚举成员**要写全限定名**。

若原 Java 用的是**传统** `case x: ... break;` 语句形式，则翻译成 C# **传统 switch 语句**，每个 `case` 末尾必须有 `break;`（C# 禁止贯穿），case 标签写全限定枚举名。

### Collections / Arrays / 泛型

| Java | C# |
|---|---|
| `Arrays.asList(UP, DOWN, LEFT, RIGHT)` | `new List<int> { UP, DOWN, LEFT, RIGHT }` |
| `new ArrayList<>(Arrays.asList(...))` | `new List<int> { ... }` |
| `List<Integer>` | `List<int>` |
| `Collections.shuffle(dirs, random)` | **C# 无内置等价物**，手写 Fisher-Yates（见下） |

`Collections.shuffle` 等价实现（写成所在类的私有静态方法）：
```csharp
private static void Shuffle(List<int> list, Random random)
{
    for (int i = list.Count - 1; i > 0; i--)
    {
        int j = random.Next(i + 1);
        int tmp = list[i];
        list[i] = list[j];
        list[j] = tmp;
    }
}
```
调用 `Collections.shuffle(dirs, random);` → `Shuffle(dirs, random);`

### 字符串下标

| Java | C# |
|---|---|
| `label.charAt(0)` | `label[0]` |

### 构造器链

Java 在构造器里用 `this(...)` 调用同类另一个构造器：
```java
public TreeMazeGenerator(int rows, int cols, EnvType envType) {
    this(rows, cols, new Random(), envType);
}
```
C# 改成**构造器初始化列表** `: this(...)`：
```csharp
public TreeMazeGenerator(int rows, int cols, EnvType envType)
    : this(rows, cols, new Random(), envType)
{
}
```

### 包级私有可见性（**关键，漏了会编译失败**）

Java 中**没有修饰符**的字段/方法/构造器是「包级私有」，同包的其它类可以访问。
而 **C# 中没有修饰符 = `private`**，只有本类能访问。直接照搬会导致同包其它类访问失败、编译报错。

**规则：Java 中无修饰符的成员，在 C# 里一律加上 `public`。**

例：`TwistedPairState`
```java
boolean p1AttackedThisTurn = false;
void checkAndResetIfBothTrue() { ... }
```
→ C#
```csharp
public bool p1AttackedThisTurn = false;
public void checkAndResetIfBothTrue() { ... }
```

宁可放宽可见性，也**不能**造成跨类访问编译失败。

## 4. 入口点规则（重要）

原 Java 项目里**有多个 `main` 方法**（`GameLauncher`、`demo01`、`demo02`、`Tools` 等可能都有）。
C# 的 `Exe` 项目**只能有一个入口点**，否则报 CS0017。

规则：

- **`GameLauncher.main` → 保留为 `public static void Main(string[] args)`**，这是唯一入口点。
- **其它所有 `main` 方法 → 改名为 `public static void Run(string[] args)`**（或 `Run()`），**不再是入口点**。

## 5. 各模块文件清单

### 模块 1：Items（14 个文件 → `Items\`）
`Item.cs` `ItemType.cs` `Useable.cs` `Craftable.cs` `Changeable.cs` `TurnCount.cs`
`GlassJar.cs` `Sulphur.cs` `Sand.cs` `WhitePhosphorus.cs` `RedPhosphorus.cs` `Charcoal.cs` `Wood.cs` `FushionSluphur.cs`

### 模块 2：Monsters（10 个文件 → `Monsters\`）
`Monster.cs` `MonsterType.cs` `MonsterLootTable.cs` `Zombie.cs` `Dragon.cs` `Dummy.cs` `LavaSlime.cs` `TwistedPairState.cs` `TwistedPairP1.cs` `TwistedPairP2.cs`

### 模块 3：Rooms / Maps / MapEnvironment（12 个文件）
→ `Rooms\`：`Room.cs` `RoomType.cs` `StartRoom.cs` `BlankRoom.cs` `FightRoom.cs` `EmptyRoom.cs` `CraftRoom.cs` `BossRoom.cs` `LootTables.cs` `MobSpawner.cs`
→ `Maps\`：`TreeMazeGenerator.cs`
→ `MapEnvironment\`：`EnvType.cs`

### 模块 4：Tools 基础（5 个文件 → `Tools\`）
`Calculator.cs` `Recipe.cs` `Tools.cs` `demo01.cs` `demo02.cs`

### 模块 5：Tools 玩家（3 个文件 → `Tools\`）
`Player.cs` `GameLobby.cs` `GameLauncher.cs`

## 6. 必须遵守的硬性要求

1. **逐行忠实翻译**，不要「顺手优化」逻辑、不要改数值、不要合并不必要的类。
2. **不要**把 `getX()/setX()` 改成 C# 属性——本阶段保留方法形式。
3. **不要**删除任何字段、方法、类，即使看起来没用到。
4. 注释（含中文注释）**原样保留**，可以保留在对应位置。
5. `Item` 是 `abstract class`，`getType()` 是抽象方法 —— 不要漏掉 `abstract`。
6. `Item.monsterType` 之类的**枚举字段**照常保留。
7. 每个文件必须**语法完整、可编译**。特别注意：类结尾大括号、`;` 结尾、`case` 后 `break`。
8. C# 的 `switch` **不允许贯穿**（Java 也不允许，但 C# 更严格），每个 `case` 必须 `break`/`return`。
9. C# 中**局部变量必须先赋值再使用**，Java 允许某些默认值情况，注意初始化。
10. 若遇到本约定没覆盖的 Java 构造，**按最小改动原则直译**，并在最终报告里**明确列出**该处，说明你的处理方式。

## 7. 产出要求

每个模块完成后，报告：

- 已创建的文件完整路径列表
- 遇到的不确定点 / 无法直译的地方（逐条列出）
- 你做的任何偏离本约定的决定及原因
