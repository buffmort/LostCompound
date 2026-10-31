using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace LostCompound.Save
{
    /// <summary>
    /// profile 里存的东西：**跨对局**都要留着的玩家档案。
    ///
    /// 与 <see cref="SaveData"/> 的区别（两者是**两个文件**，不要合并）：
    ///   · <see cref="SaveData"/>（`save.json`）= 这一局打到哪了，死了就删；
    ///   · ProfileData（`profile.json`） = 玩家是谁、死过几次、转化仓里攒了什么，
    ///     死了**不删**，而且新开局要读它。
    ///
    /// 现在的字段：
    ///   · <see cref="Name"/> —— 玩家名。镜子(45) 与电子云(`dsp²`) 的提示语要用它；
    ///   · <see cref="Deaths"/> —— 累计死亡次数（设计文档第十章「第 1 次死亡解锁…」要用）；
    ///   · <see cref="ConversionChamber"/> —— 转化仓（设计文档 3.4）。**转化仓本身还没实现**，
    ///     这里只留一个空列表占位，免得以后加字段又要升版本。
    /// </summary>
    public sealed class ProfileData
    {
        /// <summary>档案格式版本。改字段含义才升；纯加字段可以不升（旧档案缺的字段用默认值）。</summary>
        public int Version { get; set; } = Profile.CurrentVersion;

        /// <summary>玩家名。空字符串 = 还没起过名字。</summary>
        public string Name { get; set; } = "";

        /// <summary>累计死亡次数（只增不减）。</summary>
        public int Deaths { get; set; }

        /// <summary>累计通关次数（打通第 10 层）。设计文档第十章的解锁就靠它。</summary>
        public int Clears { get; set; }

        /// <summary>转化仓内容（未实现，占位）。</summary>
        public List<ItemData> ConversionChamber { get; set; } = [];
    }

    /// <summary>
    /// 玩家档案的读写与当前值。
    ///
    /// 为什么单独一个文件：`save.json` 会在死亡时被删除（单存档槽），
    /// 而名字 / 死亡次数 / 转化仓**不能跟着那一局一起消失** —— 否则每次开新局都要重新起名，
    /// 死亡次数也永远回到 0。所以档案独立存在同一个目录下（`%APPDATA%\LostCompound\profile.json`）。
    ///
    /// 读写细节与 <see cref="SaveFile"/> 完全同一套（原子写、异常吃掉不崩、损坏就重建），
    /// 这里不再重复那份长篇注释。
    /// </summary>
    public static class Profile
    {
        /// <summary>没有起名时显示的名字（镜子的提示语里用它，而不是显示空白）。</summary>
        public const string DefaultName = "玩家";

        /// <summary>档案格式版本。</summary>
        public const int CurrentVersion = 1;

        private const string FileName = "profile.json";
        private const string TempFileName = "profile.json.tmp";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private static ProfileData _current;

        /// <summary>当前档案。第一次访问时自动从磁盘读（读不到就建一份默认的，不落盘）。</summary>
        public static ProfileData Current => _current ??= LoadOrCreate();

        /// <summary>显示用的名字：没起名 / 空白时回退成 <see cref="DefaultName"/>。</summary>
        public static string Name =>
            string.IsNullOrWhiteSpace(Current.Name) ? DefaultName : Current.Name;

        /// <summary>是不是还没起过名字（开局据此决定要不要问）。</summary>
        public static bool HasNoName => string.IsNullOrWhiteSpace(Current.Name);

        /// <summary>累计通关次数（供 UI / 解锁判断读取）。</summary>
        public static int Clears => Current.Clears;

        /// <summary>档案文件路径。</summary>
        public static string FilePath => Path.Combine(SaveFile.DirectoryPath, FileName);

        /// <summary>改名并立刻落盘（名字是要长期留着的，改动就该存）。</summary>
        public static void Rename(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }
            Current.Name = name.Trim();
            Save();
        }

        /// <summary>死亡一次。**只记账，不落盘** —— 由调用方决定什么时候存（通常和删存档一起）。</summary>
        public static void AddDeath()
        {
            Current.Deaths++;
        }

        /// <summary>通关一次。**只记账，不落盘** —— 由调用方决定什么时候存。</summary>
        public static void AddClear()
        {
            Current.Clears++;
        }

        /// <summary>把当前档案写到磁盘。失败返回 false 并打印原因，不抛异常。</summary>
        public static bool Save()
        {
            string tempPath = Path.Combine(SaveFile.DirectoryPath, TempFileName);
            try
            {
                Directory.CreateDirectory(SaveFile.DirectoryPath);
                Current.Version = CurrentVersion;

                string json = JsonSerializer.Serialize(Current, JsonOptions);
                File.WriteAllText(tempPath, json, new UTF8Encoding(false));
                File.Move(tempPath, FilePath, overwrite: true);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                Console.WriteLine("[档案] 保存失败：" + ex.Message);
                return false;
            }
        }

        /// <summary>读档案；没有 / 坏了 / 版本不认，都返回一份全新的默认档案（不报错、不阻塞开局）。</summary>
        private static ProfileData LoadOrCreate()
        {
            if (!File.Exists(FilePath))
            {
                return new ProfileData();
            }

            try
            {
                string json = File.ReadAllText(FilePath, Encoding.UTF8);
                ProfileData data = JsonSerializer.Deserialize<ProfileData>(json, JsonOptions);
                if (data == null || data.Version != CurrentVersion)
                {
                    return new ProfileData();
                }
                data.ConversionChamber ??= [];
                return data;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // 档案坏了不拦着玩家玩：当成新档案，下次存档时覆盖掉
                Console.WriteLine("[档案] 读取失败（按新档案处理）：" + ex.Message);
                return new ProfileData();
            }
        }
    }
}
