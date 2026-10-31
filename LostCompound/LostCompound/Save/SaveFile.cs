using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace LostCompound.Save
{
    /// <summary>
    /// 存档文件的**读写**：只管「把对象变成文件 / 把文件变成对象」，不懂任何游戏规则。
    ///
    /// ─────────────────────────────────────────────────────────────
    /// 【基础 IO 速成】这一节是给第一次碰文件读写的人看的，只讲本文件用到的部分。
    ///
    /// 1) 路径从哪来
    ///    `Path.Combine(a, b)` 负责拼路径，**不要用字符串相加**：
    ///    自己拼 `dir + "\\" + file` 在 Windows 上碰巧能用，但会拼出双斜杠 / 漏斜杠，
    ///    换到 Linux（`/`）直接失效。Path.Combine 会自动处理分隔符。
    ///
    /// 2) 「用户目录」不要写死
    ///    写死 `C:\Users\你的名字\...` 换台机器就崩。标准做法是问系统要：
    ///    `System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)`
    ///    → Windows 上是 `%APPDATA%`（`C:\Users\<你>\AppData\Roaming`）。
    ///    常量给的是**枚举**而不是字符串，所以拼错不会静默失败。
    ///
    /// 3) 三个最常用的静态方法（都在 `System.IO.File` 上，不用 new 对象）
    ///    `File.Exists(path)`            → 文件在不在（**不要**用来判断后再读，见下面第 6 条）
    ///    `File.WriteAllText(path, txt)` → 整个文件覆盖写入（不存在就创建）
    ///    `File.ReadAllText(path)`       → 一次性读出全部文本
    ///    还有 `File.Copy` / `File.Move` / `File.Delete`。
    ///    目录用 `Directory.CreateDirectory(path)`：**已存在也不报错**（幂等），
    ///    所以不用先判断再创建。
    ///
    /// 4) 编码
    ///    默认的 `File.WriteAllText` 用 UTF-8（**不带 BOM**），`ReadAllText` 也按 UTF-8 读。
    ///    本项目所有中文都靠这个默认值活下来 —— 如果哪天用 `Encoding.Default`（中文系统上是 GBK），
    ///    存档里的「斗篷」会变成乱码，而且不报错。
    ///
    /// 5) 异常
    ///    文件 IO 是少数**正常使用也会失败**的 API：磁盘满、文件被占用、目录只读、
    ///    杀毒软件锁文件……所以每个读写点都要 try/catch，而不是「应该不会出错」。
    ///    存档失败**绝不能崩游戏**，最差也要退化成「这一局没存上」。
    ///    要抓的三种：
    ///      `IOException`              —— 磁盘/占用类问题（基类，能覆盖大部分）
    ///      `UnauthorizedAccessException` —— 没权限
    ///      `JsonException`            —— 文件内容不是合法 JSON（人手改坏了 / 写了一半）
    ///
    /// 6) 写入要「原子」
    ///    直接 `WriteAllText(save.json)` 有个致命窗口：写到一半进程被杀，
    ///    文件只剩半截 —— 下次读档 JSON 解析失败，**存档彻底报废**。
    ///    做法：先写 `save.json.tmp`，写成功了再 `File.Move(tmp, 目标, overwrite: true)`。
    ///    同目录内的改名在操作系统层面是原子的，所以目标文件要么是旧的完整内容、
    ///    要么是新的完整内容，不存在「半截」。
    /// ─────────────────────────────────────────────────────────────
    /// </summary>
    public static class SaveFile
    {
        /// <summary>
        /// 当前存档格式版本。
        /// 改了 `SaveData` 的字段含义才需要动它；纯加字段可以不升（旧档缺的字段用默认值）。
        /// </summary>
        public const int CurrentVersion = 2;

        /// <summary>存档文件名。</summary>
        private const string FileName = "save.json";

        /// <summary>临时文件名（原子写入用，见类注释第 6 条）。</summary>
        private const string TempFileName = "save.json.tmp";

        /// <summary>
        /// JSON 序列化选项：
        ///   · `WriteIndented` —— 缩进换行，存档可以人眼阅读 / 手改（调试时很有用）
        ///   · `UnsafeRelaxedJsonEscaping` —— 让中文原样写进文件，而不是变成 `\u6597\u7BF7`。
        ///     （名字里的 Unsafe 指的是「不转义 &lt; &gt; &amp; 等字符对注入不安全」，
        ///       我们是写本地文件、不嵌网页，所以安全。）
        /// </summary>
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        /// <summary>存档所在目录（`%APPDATA%\LostCompound`）。</summary>
        public static string DirectoryPath => Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "LostCompound");

        /// <summary>存档文件的完整路径。</summary>
        public static string FilePath => Path.Combine(DirectoryPath, FileName);

        /// <summary>存档是否存在（主菜单据此决定显不显示「继续游戏」）。</summary>
        public static bool Exists => File.Exists(FilePath);

        /// <summary>
        /// 写存档。成功返回 true；失败返回 false 并打印原因，**不抛异常**。
        /// </summary>
        public static bool Save(SaveData data)
        {
            if (data == null)
            {
                Console.WriteLine("[存档] 内部错误：要保存的数据是空的，本次没有存档。");
                return false;
            }

            string tempPath = Path.Combine(DirectoryPath, TempFileName);
            try
            {
                data.Version = CurrentVersion;
                data.SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                // 目录不存在时 CreateDirectory 会建出来；已存在则什么也不做（幂等）
                Directory.CreateDirectory(DirectoryPath);

                string json = JsonSerializer.Serialize(data, JsonOptions);

                // 先写临时文件，再原子改名覆盖 —— 见类注释第 6 条
                File.WriteAllText(tempPath, json, new UTF8Encoding(false));
                File.Move(tempPath, FilePath, overwrite: true);

                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                Console.WriteLine("[存档] 保存失败：" + ex.Message);
                // 清掉可能残留的半截临时文件（删不掉也没关系，下次会被覆盖）
                TryDelete(tempPath);
                return false;
            }
        }

        /// <summary>
        /// 读存档。返回 <c>null</c> 表示「没有存档 / 存档坏了 / 版本不对」，
        /// 三种情况都会打印一句人能看懂的话，调用方只需判断 null。
        /// </summary>
        public static SaveData Load()
        {
            if (!Exists)
            {
                return null;   // 没有存档不是错误，静默返回
            }

            string json;
            try
            {
                json = File.ReadAllText(FilePath, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine("[存档] 读取失败：" + ex.Message);
                return null;
            }

            SaveData data;
            try
            {
                data = JsonSerializer.Deserialize<SaveData>(json, JsonOptions);
            }
            catch (JsonException)
            {
                // 文件在、但不是合法 JSON：写了一半 / 被人手改坏了
                Console.WriteLine("[存档] 存档文件已损坏，无法读取。");
                return null;
            }

            if (data == null)
            {
                Console.WriteLine("[存档] 存档文件是空的。");
                return null;
            }

            if (data.Version != CurrentVersion)
            {
                // 版本不同就**直接拒绝**，不做「尽力而为」的部分读取：
                // 半懂不懂地读进来，往往比明确拒绝更糟（玩家会带着错乱的状态继续玩）。
                Console.WriteLine($"[存档] 存档版本是 {data.Version}，游戏需要 {CurrentVersion}，无法读取。");
                return null;
            }

            return data;
        }

        /// <summary>删除存档（玩家死亡时调用）。删除失败不影响游戏。</summary>
        public static void Delete()
        {
            TryDelete(FilePath);
        }

        /// <summary>尽力删除一个文件：不存在、没权限、被占用都**不抛异常**。</summary>
        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 删不掉就算了：留着一个旧档最多是「继续游戏」读到上一局，
                // 比在这里崩掉进程好得多。
            }
        }
    }
}
