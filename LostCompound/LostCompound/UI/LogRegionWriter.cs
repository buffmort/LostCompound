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

        /// <summary>
        /// **真实输出**（第一次创建重定向器时抓住的 Console.Out）。
        ///
        /// 为什么必须存成静态：ConsoleScreen 的构造函数里写的是 `_raw = Console.Out`，
        /// 那只在「构造时控制台还没被接管」的情况下成立。一旦某个界面**装着的时候**
        /// 又构造了第二个 ConsoleScreen（调试战斗从地图里开、或者嵌套界面），
        /// 新对象的 `_raw` 就会指向**旧的 LogRegionWriter** ——
        /// 于是所有光标定位序列（ESC[行;列H）都会被当成日志内容塞进日志框，
        /// 终端一执行就花屏（Hyberty 2026-11-21 报的「花屏」，日志里那串 `[36;5H` 就是它）。
        ///
        /// 存成静态之后，无论构造多少次、嵌套多少层，拿到的都是同一条真实输出流。
        /// </summary>
        public static TextWriter RealOut { get; private set; }

        public LogRegionWriter(ConsoleScreen screen)
        {
            _screen = screen;
            RealOut ??= Console.Out;      // 只记第一次（那时控制台一定还没被接管）
        }

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

            // 这里原先还有一句 `_screen.SetPartial(_pending.ToString())`，把未换行内容画进输入框。
            // 已删除：输入框现在只画 ">"（提示与回显叠在一个框里，输多了会错位，
            // 详见 ConsoleScreen.RedrawInputBox 的注释）。未完成行继续留在 _pending 里，
            // 等下一个换行一起提交成日志行 —— 内容不会丢。
        }
    }
}
