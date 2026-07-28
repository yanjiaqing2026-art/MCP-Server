using System;
using System.Collections.Generic;

namespace E3DMcpServer.Tools
{
    /// <summary>
    /// E3D PML 命令参考（写回 17 库，2026-07-03）——从 help.aveva.com 官方手册
    /// (DRMPGC / DBRM / QRM / SOFTCG / PDUV) 逐字整理。
    ///
    /// 完整逐字手册 + 置信度分级见插件仓 docs/E3D命令手册-官方逐字版.md；
    /// 机读数据集(113 条，含语法/用例/手册页码/置信度)见 docs/data/e3d-commands.json。
    ///
    /// 本类给插件【写路径】提供一个【命令黑名单守卫】：AVEVA 官方已裁决"命令行无此逐字语法"
    /// 的伪命令（CUT/SPLIT/GAP/JOIN）绝不发给 E3D——否则只会拿一句晦涩拒收；改回清晰的
    /// 诚实错误 + 正解提示，与 09 侧 prompt 黑名单同口径。
    ///
    /// 诚实边界：命令库来源主力是官方 1.1 分支(公开全文)，真机是 3.1；逐字缩写/Q 返回格式
    /// 以真机 `$Q` 为最终裁判。本守卫只拦【官方确认不存在】的 4 个伪命令(安全，绝不误伤有效命令)，
    /// 不做正向白名单(避免把没编目到的有效命令误拒)。
    /// </summary>
    public static class E3dCommandReference
    {
        /// <summary>
        /// 官方裁决【命令行无此逐字语法】的伪命令（作为独立首动词时）——绝不发给 E3D。
        /// 依据：PDUV "Pipe Splitting" 仅 GUI 窗口式操作；DRMPGC 全集无 CUT/GAP/JOIN 逐字命令。
        /// 切管的正解 = 在切点"插入组件 + 新建 BRAN"；留间隙用官方 CLEARANCE 命令。
        /// </summary>
        public static readonly HashSet<string> BlacklistedVerbs =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CUT", "SPLIT", "GAP", "JOIN" };

        /// <summary>取 PML 命令串的首动词（大写、去前导空白）。</summary>
        public static string LeadingVerb(string pml)
        {
            if (string.IsNullOrWhiteSpace(pml)) return "";
            string t = pml.TrimStart();
            int i = 0;
            while (i < t.Length && !char.IsWhiteSpace(t[i])) i++;
            return t.Substring(0, i).ToUpperInvariant();
        }

        /// <summary>命令首动词是否为官方确认不存在的伪命令。</summary>
        public static bool IsBlacklisted(string pml, out string verb)
        {
            verb = LeadingVerb(pml);
            return BlacklistedVerbs.Contains(verb);
        }

        /// <summary>黑名单命令的诚实错误信息（"Error:" 前缀符合 MCP 诚实化约定）+ 正解提示。</summary>
        public static string BlacklistError(string verb)
        {
            return "Error: '" + verb + "' 不是有效的 E3D 命令行命令（AVEVA 官方裁决无此逐字语法：" +
                   "PDUV Pipe Splitting 仅 GUI，DRMPGC 无此命令）。切管请走「在切点插入组件 + 新建 BRAN」，" +
                   "间隙用 CLEARANCE。详见 docs/E3D命令手册-官方逐字版.md。";
        }
    }
}
