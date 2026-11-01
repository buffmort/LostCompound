using System.Security.Cryptography;
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
    ///
    /// 7) 文件长什么样（2026-11-01 起：带校验，用户要求「验证过不去就删档」）
    ///    外层是一个**信封**，存档本体被 base64 包在里面：
    ///    <code>
    ///    {
    ///      "Version": 4,
    ///      "Hash": "……base64 的 二次哈希( 一次哈希(载荷) + 盐 )……",
    ///      "Payload": "……base64 的存档 JSON……"
    ///    }
    ///    </code>
    ///    为什么要包一层、而不是在存档 JSON 里加个 hash 字段：
    ///      hash 必须覆盖「存档本体」，而存档本体里如果含 hash 字段，就等于让它自我引用。
    ///      分成「信封 + 载荷」之后，签名对象就是那段字节，干净。
    ///    代价：**存档不再是人类可读的 JSON**（base64 看着像乱码）。
    ///
    ///    签名算法（用户 2026-11-01 定：「字节码 → 一次哈希 → 加盐 → 二次哈希」）：
    ///      first  = SHA256(载荷字节)
    ///      salted = first ‖ 盐（<see cref="HashSuffix"/>）
    ///      Hash   = base64( SHA256(salted) )
    ///      为什么加盐：裸 SHA256 谁都能重算 —— 改完血量再算一遍填回去，校验照样通过，等于没锁。
    ///      盐只在程序里，不知道它就算不出能对上的签名，所以二次解码再编码会失效。
    ///      （盐在二进制里，够挡住手改，但不是密码学级别的 secrecy。）
    ///    这是**完整性校验**，不是加密：没有隐藏内容的意思，只是「内容被动过」。
    /// ─────────────────────────────────────────────────────────────
    /// </summary>
    public static class SaveFile
    {
        /// <summary>
        /// 当前存档格式版本。
        /// **3 = 加了签名信封（单轮 SHA256 + 盐）**；
        /// **4 = 签名改成两轮 SHA256（一次哈希 → 加盐 → 二次哈希）** —— 3 版存档读不出来，会被删掉。
        /// </summary>
        public const int CurrentVersion = 4;

        /// <summary>
        /// 签名用的**盐**（用户 2026-11-01 定：用「HYB」）。
        ///
        /// 它夹在两次哈希之间：`SHA256( SHA256(载荷) ‖ "HYB" )`。
        /// 作用是：别人拿到存档、解开 base64、改完内容再自己算一遍 SHA256 填回去 ——
        /// **不知道这串盐就算不出能对上的签名**，所以二次解码再编码会失效。
        ///
        /// ⚠️ 改了它 = 所有旧存档校验失败 → 全部删档，所以不要随手改。
        /// </summary>
        private const string HashSuffix = "HYB";

        private static readonly byte[] HashSuffixBytes = Encoding.UTF8.GetBytes(HashSuffix);

        /// <summary>存档文件名。</summary>
        private const string FileName = "save.json";

        /// <summary>临时文件名（原子写入用，见类注释第 6 条）。</summary>
        private const string TempFileName = "save.json.tmp";

        /// <summary>
        /// JSON 序列化选项：
        ///   · `WriteIndented` —— 缩进换行，外层信封可以人眼阅读（调试时很有用）
        ///   · `UnsafeRelaxedJsonEscaping` —— 让中文原样写进文件，而不是变成 `\u6597\u7BF7`。
        ///     （名字里的 Unsafe 指的是「不转义 &lt; &gt; &amp; 等字符对注入不安全」，
        ///       我们是写本地文件、不嵌网页，所以安全。）
        /// </summary>
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        /// <summary>
        /// 存档文件的信封：版本 + 签名 + 载荷（base64 的存档 JSON）。
        /// 加字段不会破坏旧档（缺的字段是默认值），但改**语义**要升 <see cref="CurrentVersion"/>。
        /// </summary>
        private sealed class SaveEnvelope
        {
            public int Version { get; set; }
            public string Hash { get; set; } = "";
            public string Payload { get; set; } = "";
        }

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

                // ① 存档本体 → 字节 → base64；② 对**那段字节**签名；③ 一起塞进信封
                byte[] payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, JsonOptions));
                SaveEnvelope envelope = new()
                {
                    Version = CurrentVersion,
                    Payload = Convert.ToBase64String(payload),
                    Hash = ComputeHash(payload),
                };
                string json = JsonSerializer.Serialize(envelope, JsonOptions);

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
        /// 读存档。返回 <c>null</c> 表示「没有存档 / 存档坏了 / 版本不对 / **校验没过**」。
        ///
        /// ⚠️ 除了「没有存档」，其它情况都会**把这张存档删掉**（用户裁定 2026-11-01）：
        ///    文件被改过、或版本不认，都是「这份档不可信」，留着只会在下一次「继续游戏」时
        ///    又失败一次。宁可让人重开一局，也不要把一份坏档反复塞给玩家。
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

            // ---- ① 拆信封 ----
            SaveEnvelope envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<SaveEnvelope>(json, JsonOptions);
            }
            catch (JsonException)
            {
                return RejectSave("存档文件已损坏（不是合法 JSON）。");
            }

            if (envelope == null || string.IsNullOrEmpty(envelope.Payload) || string.IsNullOrEmpty(envelope.Hash))
            {
                // 旧版（裸 JSON 的存档）也会走到这里：没有 Payload / Hash 字段
                return RejectSave("存档格式不认识或缺少校验信息。");
            }

            if (envelope.Version != CurrentVersion)
            {
                // 版本不同就**直接拒绝并删档**，不做「尽力而为」的部分读取：
                // 半懂不懂地读进来，往往比明确拒绝更糟（玩家会带着错乱的状态继续玩）。
                Console.WriteLine($"[存档] 存档版本是 {envelope.Version}，游戏需要 {CurrentVersion}。");
                return RejectSave("存档版本不兼容。");
            }

            // ---- ② 验签：重算 SHA256(载荷 + 后缀)，和文件里写的比 ----
            byte[] payload;
            try
            {
                payload = Convert.FromBase64String(envelope.Payload);
            }
            catch (FormatException)
            {
                return RejectSave("存档载荷不是合法的 base64。");
            }

            string expected = ComputeHash(payload);
            // FixedTimeEquals：逐字节比较时**不提前返回**，避免按「比较耗时」反推正确签名的长度/前缀。
            // 这里其实没有攻击者能反复试，但这是密码学代码的默认写法，照做不吃亏。
            bool ok = CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(envelope.Hash));
            if (!ok)
            {
                return RejectSave("存档校验失败（文件被改动过）。");
            }

            // ---- ③ 载荷 → 存档对象 ----
            SaveData data;
            try
            {
                data = JsonSerializer.Deserialize<SaveData>(Encoding.UTF8.GetString(payload), JsonOptions);
            }
            catch (JsonException)
            {
                // 签名对但解析不了：只可能是程序自己写坏了（例如改结构时忘了升版本）
                return RejectSave("存档内容无法解析。");
            }

            if (data == null)
            {
                return RejectSave("存档内容是空的。");
            }

            if (data.Version != CurrentVersion)
            {
                Console.WriteLine($"[存档] 存档版本是 {data.Version}，游戏需要 {CurrentVersion}。");
                return RejectSave("存档版本不兼容。");
            }

            return data;
        }

        /// <summary>
        /// 校验失败的统一出口：**先删档，再返回 null**。
        ///
        /// 为什么集中成一处：校验失败的入口有五六条（JSON 坏、缺字段、版本不对、base64 坏、
        /// 签名对不上……），每条都写一遍「打印 + 删除」必然有一天漏掉一条 ——
        /// 漏掉的那条就会留下一个永远读不出来的档，每次「继续游戏」都失败一次。
        /// </summary>
        private static SaveData RejectSave(string reason)
        {
            Console.WriteLine("[存档] " + reason + "这张存档已删除。");
            Delete();
            return null;
        }

        /// <summary>
        /// 签名：**两轮 SHA256**，盐夹在中间。用户 2026-11-01 定的口径：
        ///   「先变成一段彻底的字节码 → 一次哈希 → 加盐 → 二次哈希」。
        ///
        /// <code>
        /// first  = SHA256(载荷字节)                 // 第一轮：把原文压成 32 字节
        /// salted = first ‖ "HYB"                    // 加盐：拼在**哈希值**末尾（不是拼在原文上）
        /// Hash   = base64( SHA256(salted) )         // 第二轮
        /// </code>
        ///
        /// 为什么要两轮而不是「原文 + 盐」一轮：
        ///   第一轮之后得到的是长度固定的 32 字节摘要，盐拼在它后面 —— 原文多长都一样，
        ///   而且就算有人拿到摘要也还原不出原文。两轮也让「改存档」这条路更难走：
        ///   改原文 → 第一轮变 → 第二轮变 → 签名对不上。
        /// 为什么要盐：不加盐的话，任何人解开 base64、改完血量、重新算一遍 SHA256 填回去，
        ///   校验照样通过，等于没锁。盐只在程序里，不知道它就算不出能对上的签名。
        ///
        /// ⚠️ 换盐 = 所有旧存档校验失败 → 全部删档。
        /// 这是**完整性校验**，不是加密：内容没被隐藏（base64 解一下就能看）。
        /// </summary>
        private static string ComputeHash(byte[] payload)
        {
            // 第一轮：原文 → 32 字节摘要
            byte[] firstPass = SHA256.HashData(payload);

            // 加盐：摘要 ‖ 盐
            byte[] salted = new byte[firstPass.Length + HashSuffixBytes.Length];
            Buffer.BlockCopy(firstPass, 0, salted, 0, firstPass.Length);
            Buffer.BlockCopy(HashSuffixBytes, 0, salted, firstPass.Length, HashSuffixBytes.Length);

            // 第二轮：对「摘要 + 盐」再哈希
            return Convert.ToBase64String(SHA256.HashData(salted));
        }

        /// <summary>删除存档（玩家死亡时调用；校验失败时也走它）。删除失败不影响游戏。</summary>
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
