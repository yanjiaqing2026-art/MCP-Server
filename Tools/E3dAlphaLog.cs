using System;
using System.IO;
using System.Text;

namespace E3DMcpServer.Tools
{
    /// <summary>
    /// <b>ALPHA LOG —— 真正能拿到命令窗口输出的那条通道。</b>
    ///
    /// ═══ ⛔⛔ 我把「拿不到」当成结论下早了（2026-07-31 用户点破）═══════════════
    /// 第十八跑之后我写下：「命令窗口的正常打印，MCP 两条通道都拿不到 ——
    /// 这是能力边界」。用户当场反问：**「照道理不会的，一定有一个输出。」**
    /// 他是对的。我证明的只是**我试过的那两条**拿不到：
    ///   ✗ <c>PdmsOutputEvents.AddOutputListener</c> —— 真机实测每条命令 0 个事件，
    ///     进程累计里 Type/Category 只有 <c>ERROR/</c> → 这条通道天然只流报错。
    ///   ✗ <c>Command.Result</c> —— 第十八跑第一次真正探到它（裸 <c>$Q</c> 确实走进了
    ///     <c>isReadOnly</c> 分支），而它本身就是空的。
    /// **两条都是 .NET 通道。而 PDMS 自己有一条命令级的输出重定向，我从没查过。**
    ///
    /// ═══ 官方形态（PDMS 老牌用法，不是我猜的）════════════════════════════════
    /// <code>
    ///   ALPHA LOG /C:/out.txt OVER     ← 开始把命令窗口输出写进文件（OVER = 覆盖同名）
    ///   LI MDB                          ← 任意命令，它打印什么都进文件
    ///   ALPHA LOG END                   ← 结束
    /// </code>
    /// 出处：PDMS Syntax Library「To log all command window informations」逐字例子
    /// 就是上面这段；社区教程（pdmsmacro / mehungryblog）用的也是同一形态。
    ///
    /// ★**为什么这条对我们成立，而"宏路"当年不成立**：宏那条卡在
    ///   「要在 E3D 那台机器上写文件」——**而插件本来就跑在那台机器上**。
    ///   `e3d_datal_dump` 已经用同一个模式跑通过真机（§30）：
    ///   写临时文件 → 读回来放进回执 → 删掉。这里照抄那条已验证的路。
    ///
    /// ═══ 它解掉什么 ═══════════════════════════════════════════════════════════
    /// · <c>$Q</c> —— 官方语法提示查询。手册反复写「真机 `$Q` 为准」，它是**权威裁决器**。
    ///   拿得到之后，「这条命令形态对不对」从此可以自问自答，不必再攒一批进内网试。
    /// · <c>Q ATT</c> / <c>Q SPEC</c> / <c>LIST</c> / <c>Q MEM</c> —— 整个 Q 家族。
    ///
    /// ⚠ <b>未上真机</b>。判据先写死（脚本 §39）：
    ///   回执里出现命令的**真实打印内容** = 通道成立；
    ///   文件没生成 / 是空的 → 如实说是哪一种，**不要报成"能力不行"**。
    /// </summary>
    internal static class E3dAlphaLog
    {
        /// <summary>
        /// 跑一条（或多行）命令，并把**命令窗口的打印**一起带回来。
        /// </summary>
        /// <param name="command">要跑的命令。多行用 \n 分隔。</param>
        /// <param name="maxChars">回执里最多带回多少字符（防止 LIST 之类刷屏）。</param>
        public static string Run(string command, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(command)) return "alphalog: 缺 command";
            if (maxChars <= 0) maxChars = 20000;

            // ★不用 Path.GetTempPath() 直接拼：ALPHA LOG 的路径参数在 PDMS 里以 `/` 开头，
            //   而 Windows 临时目录是 `C:\Users\...`。实测形态是 `ALPHA LOG /C:/out.txt OVER`
            //   —— **前导斜杠 + 正斜杠分隔**。这里按那个形态拼，并把结果原样记进回执，
            //   万一路径形态不对，下一跑一眼能看出是路径问题还是通道问题。
            string tmp = Path.Combine(Path.GetTempPath(), "pcmcp-alpha-" + Guid.NewGuid().ToString("N") + ".txt");
            string pdmsPath = "/" + tmp.Replace('\\', '/');

            var sb = new StringBuilder();
            sb.Append("alphalog（把命令窗口的打印一起带回来）\n");
            sb.Append("  日志文件: ").Append(pdmsPath).Append('\n');

            string startErr = null, endErr = null, cmdErr = null;
            bool started = false;
            try
            {
                // ① 开日志
                started = E3dApiWrapper.TryRunPmlPublic("ALPHA LOG " + pdmsPath + " OVER", out startErr);
                if (!started)
                {
                    sb.Append("  ⛔ **ALPHA LOG 没开起来**：").Append(startErr ?? "(E3D 未给出错误文本)").Append('\n');
                    sb.Append("  ★这说明的是「这条命令没被接受」，**不是**「命令窗口没有输出」——\n");
                    sb.Append("    两者的下一步完全不同。把上面这句原文贴回来。\n");
                    return sb.ToString();
                }

                // ② 跑真正的命令（逐行发 —— ALPHA LOG 已经在录，中途报错也照样录进去）
                foreach (var raw in command.Replace("\r\n", "\n").Split('\n'))
                {
                    var line = (raw ?? "").Trim();
                    if (line.Length == 0) continue;
                    string e;
                    if (!E3dApiWrapper.TryRunPmlPublic(line, out e) && cmdErr == null) cmdErr = e;
                }
            }
            finally
            {
                // ③ 无论如何都要关 —— 不关的话 E3D 会一直往这个文件里写，
                //    工程师后面每一条命令都进我们的临时文件（而且文件删不掉）。
                if (started) { try { E3dApiWrapper.TryRunPmlPublic("ALPHA LOG END", out endErr); } catch { } }
            }

            // ④ 读回来
            try
            {
                if (!File.Exists(tmp))
                {
                    sb.Append("  ⛔ ALPHA LOG 开了也关了，但**文件没生成**。\n");
                    sb.Append("  ★三种可能，别混：① 路径形态不对（上面那行就是我们发过去的原文）；\n");
                    sb.Append("    ② 这套 E3D 不认 ALPHA LOG；③ 权限写不进去。\n");
                    if (!string.IsNullOrWhiteSpace(endErr)) sb.Append("    ALPHA LOG END 的回话: ").Append(endErr).Append('\n');
                    return sb.ToString();
                }
                string text = File.ReadAllText(tmp);
                if (string.IsNullOrWhiteSpace(text))
                {
                    sb.Append("  ⚠ 文件生成了但**是空的** —— 通道通了、这条命令确实没打印任何东西。\n");
                    sb.Append("  ★这与「拿不到输出」是**两回事**，别混成一句。\n");
                    if (!string.IsNullOrWhiteSpace(cmdErr)) sb.Append("    命令本身的回话: ").Append(cmdErr).Append('\n');
                    return sb.ToString();
                }
                bool truncated = text.Length > maxChars;
                if (truncated) text = text.Substring(0, maxChars);
                sb.Append("  共 ").Append(truncated ? maxChars + "+（**已截断**，原文更长）" : text.Length.ToString()).Append(" 字符\n");
                sb.Append("--- 命令窗口输出 ---\n").Append(text).Append('\n');
                if (!string.IsNullOrWhiteSpace(cmdErr))
                    sb.Append("（命令本身报过错：").Append(cmdErr).Append(" —— 输出照录，两件事分开看）\n");
                return sb.ToString();
            }
            catch (Exception ex) { return sb.Append("  读日志文件失败: ").Append(ex.Message).Append('\n').ToString(); }
            finally { try { if (File.Exists(tmp)) File.Delete(tmp); } catch { } }
        }
    }
}
