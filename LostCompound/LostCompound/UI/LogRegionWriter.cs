using System.Text;

namespace LostCompound.UI
{
    /// <summary>
    /// 把控制台输出重定向到「日志区」的 TextWriter。
    ///
    /// 原理：游戏中所有 Console.WriteLine 都会经过 <see cref="Console.Out"/>。
    /// 用 Console.SetOut 把 Console.Out 换成这个对象之后，
    /// 那 100 多处已有的打印代码**一行都不用改**，输出就自动进了日志区。
    ///
    /// 它按「行」为单位工作：
    ///   - 收到 '\n' 时，把攒好的这一行提交成一条日志；
    ///   - 收到 Console.Write（不带换行，例如 "请选择: "）时，作为「当前未完成行」暂存。
    /// </summary>
    public sealed class LogRegionWriter : TextWriter
    {
        private readonly ConsoleScreen _screen;
        private readonly StringBuilder _pending = new();

        public LogRegionWriter(ConsoleScreen screen) => _screen = screen;

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            switch (value)
            {
                case '\n':
                    _screen.AppendLog(_pending.ToString());
                    _pending.Clear();
                    break;
                case '\r':
                    break;      // 忽略回车，只认 \n
                default:
                    _pending.Append(value);
                    _screen.SetPartial(_pending.ToString());
                    break;
            }
        }

        /// <summary>
        /// 整串一次性处理，而不是逐字符调用 <see cref="Write(char)"/>。
        /// 逐字符会导致「每个字符重画一次日志区」，在真终端里会剧烈闪烁、输出量爆炸。
        /// </summary>
        public override void Write(string value)
        {
            if (value == null) return;

            foreach (char c in value)
            {
                if (c == '\n')
                {
                    _screen.AppendLog(_pending.ToString());
                    _pending.Clear();
                }
                else if (c != '\r')
                {
                    _pending.Append(c);
                }
            }

            // 整串处理完只更新一次未完成行
            _screen.SetPartial(_pending.ToString());
        }
    }
}
