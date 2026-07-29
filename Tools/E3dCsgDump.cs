using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Aveva.Core.Database;
using Aveva.Core3D.CSG;

namespace E3DMcpServer.Tools
{
    /// <summary>
    /// 把一个元素的**真实几何图元**（含尺寸与变换矩阵）读出来，交给 agent。
    ///
    /// ═══ 为什么要有它 ═══════════════════════════════════════════════════════
    /// 现有 <c>e3d_collect_geometry</c> 只回 <c>@ (X,Y,Z)</c> 位置 + 几个字符串属性，
    /// **没有包围盒、没有变换、没有真尺寸**。于是几个问题一直答不了：
    ///   · DISH 封头的**局部原点在底面还是拱顶**（挂了六跑的门控）
    ///   · 保温包络 / 障碍体到底多大（SP-2 现在是我们自己按经验算的）
    ///   · 元素的真实几何与我们发的命令是否一致（写后验收只能比属性）
    /// 而 <c>e3d_export_rvm</c> 那条路要导文件、拷回来、再解析 —— 重且绕。
    ///
    /// ═══ 依据（AVEVA 自带 .NET XML 文档，签名逐字）═══════════════════════════
    ///   <c>CSGTreeBuilder.ReadGeometry(DbElement, CSGTreeBuilderOptions, CSGPrimitivesVisitor)</c>
    ///       "Reads through all element primitives"
    ///   <c>CSGPrimitivesVisitor.VisitDish(DbElement, CSGType, double radius, double height,
    ///       CSGDishType dishType, double[] transformation, int translucency)</c>
    ///       —— <b>transformation 就是「局部原点在哪」的答案</b>
    ///   <c>CSGTreeBuilderOptions</c>: CenterLine · Tube · Insulation · Obstruction · Holes · SolidOnly · Pline
    ///
    /// ═══ ✅ 已编译通过 · ⚠ 真机未跑过 ═══════════════════════════════════════
    /// 本文件在**没有 AVEVA 编译环境**的机器上写成。方法签名逐字取自官方 XML 文档，
    /// 但**没编译过、没跑过**。第一次编译若报错，多半是：
    ///   · <c>CSGTreeBuilder.Instance</c> 的取法（文档只写 "Instance of Abstract class"）
    ///   · <c>CSGTreeBuilderOptions</c> 的构造（文档有 <c>CreateCSGTreeBuilderOptions</c>）
    /// 这两处已单独包 try 并如实报错，**不会把整个插件带崩**。
    ///
    /// ═══ 诚实红线 ═══════════════════════════════════════════════════════════
    /// 变换矩阵**原样回传，不做解释**。我不知道 AVEVA 用的是行主序还是列主序、
    /// 12 个数还是 16 个数 —— 猜一个说法比不说更坏。
    /// 09 侧拿到真实数组后再定判据（那时有真数据，不用猜）。
    /// </summary>
    internal sealed class E3dCsgDump : CSGPrimitivesVisitor
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private int _count;
        private readonly int _max;

        private E3dCsgDump(int max) { _max = max > 0 ? max : 200; }

        /// <summary>读一个元素的全部几何图元。永不抛 —— 失败回一条说明串。</summary>
        public static string Dump(string path, int max, bool insulation, bool obstruction, bool centerLine)
        {
            if (string.IsNullOrWhiteSpace(path)) return "Error: 请提供 name（元素路径）";
            DbElement el;
            try
            {
                el = DbElement.GetElement(path);
                if (el == null || !el.IsValid) return $"Error: 元素 '{path}' 不存在或无效";
            }
            catch (Exception ex) { return "Error: 取元素失败: " + ex.GetBaseException().Message; }

            var v = new E3dCsgDump(max);
            try
            {
                var builder = CSGTreeBuilder.Instance;
                if (builder == null) return "Error: CSGTreeBuilder.Instance 为 null（本 E3D 版本可能不同）";

                var opts = builder.CreateCSGTreeBuilderOptions();
                try
                {
                    opts.Tube = true;                 // 实体几何
                    opts.SolidOnly = false;
                    opts.Insulation = insulation;     // 保温包络（SP-2 现在自己算的那个）
                    opts.Obstruction = obstruction;   // 障碍体
                    opts.CenterLine = centerLine;     // 管道中心线
                    opts.Holes = false;
                }
                catch (Exception ex)
                {
                    // 选项设不上不影响主流程，如实记一行。
                    v._sb.AppendLine("  ⚠ CSGTreeBuilderOptions 部分选项设置失败: " + ex.GetBaseException().Message);
                }

                builder.ReadGeometry(el, opts, v);
            }
            catch (Exception ex)
            {
                return "Error: ReadGeometry 失败: " + ex.GetBaseException().Message
                     + "\n（这条路按官方 XML 文档签名写、已编译通过，但**真机未跑过** —— 把这行原文贴回来即可定位）";
            }

            // ── ★第二段：未变换包围盒（visitor 给不出，只能走 CSGTree.Items）──────
            // 病历（2026-07-29 真机第七跑）：visitor 回的是**定义参数**
            //（Radius/Height/Transform），而 DISH「局部原点在底面还是拱顶」
            // 靠参数**问不出来** —— 同一组 Radius+Height，原点在底或在顶都成立。
            // 要定它必须拿**几何的实际范围**：CSGItem.Limits = "untransformed limits box"。
            //   原点在底面 → 未变换 bbox 的 Z 约为 [0, +Height]
            //   原点在拱顶 → 约为 [-Height, 0]
            // ★Limits 的类型官方 XML 没写 —— **不猜**，反射把它的属性原样 dump 出来，
            //   拿到真数据再定判据（猜一个字段名比不说更坏）。
            try
            {
                var builder2 = CSGTreeBuilder.Instance;
                var opts2 = builder2.CreateCSGTreeBuilderOptions();
                try { opts2.Tube = true; } catch { }
                // ★GetGeometry 回的是 CSGTree[] **数组**，不是单个 —— 编译器纠正的
                //  （CS1061: CSGTree[] 未包含 Items 的定义）。一个元素可以有多棵树。
                var trees = builder2.GetGeometry(el, opts2);
                if (trees == null || trees.Length == 0)
                {
                    v._sb.AppendLine("  -- 未变换包围盒: GetGeometry 回空 --");
                }
                else
                {
                    v._sb.AppendLine($"  -- 未变换包围盒 (CSGTree×{trees.Length} 的 CSGItem.Limits, 原样 dump) --");
                    int n = 0;
                    foreach (var tree in trees)
                    {
                        if (tree == null) continue;
                        foreach (var item in tree.Items)
                        {
                            if (item == null || ++n > 20) break;
                            object lim = null, tf = null;
                            try { lim = item.Limits; } catch { }
                            try { tf = item.Transform; } catch { }
                            v._sb.Append("    #").Append(n)
                                 .Append(" TYPE=").Append(SafeStr(item.Type))
                                 .Append(" LIMITS=").Append(DumpProps(lim))
                                 .Append(" XFORM=").Append(DumpProps(tf))
                                 .AppendLine();
                        }
                    }
                    if (n == 0) v._sb.AppendLine("    (所有 tree.Items 都为空)");
                }
            }
            catch (Exception ex)
            {
                v._sb.AppendLine("  -- 未变换包围盒: 取不到 (" + ex.GetBaseException().Message + ") --");
            }

            if (v._count == 0)
                return $"csg: 0 primitives (root: {path})\n"
                     + "  读到 0 个图元。可能是：该元素本身不带几何（纯容器）/ 选项把它过滤掉了。\n"
                     + "  **不代表元素不存在** —— 存在性请用 e3d_element_exists。";

            return $"csg: {v._count} primitives (root: {path})\n" + v._sb;
        }

        // ── 输出格式：一行一个图元，`|KEY=VAL` 后缀（对齐 collect_geometry 的既有约定，
        //    09 侧解析器可复用）。变换矩阵原样回，不解释。────────────────────────
        private void Emit(DbElement element, CSGType type, string kind, string dims, double[] tr)
        {
            if (_count >= _max) return;
            _count++;
            string nm = "";
            try { nm = element != null && element.IsValid ? element.GetAsString(DbAttributeInstance.NAME) : ""; }
            catch { }
            _sb.Append("  [").Append(kind).Append("] ").Append(string.IsNullOrEmpty(nm) ? "(无名)" : nm);
            _sb.Append("|CSGTYPE=").Append(type);
            if (!string.IsNullOrEmpty(dims)) _sb.Append('|').Append(dims);
            _sb.Append("|TRANSFORM=").Append(Fmt(tr));
            _sb.AppendLine();
        }

        private static string N(double d)
        {
            return Math.Round(d, 3).ToString(CultureInfo.InvariantCulture);
        }

        private static string SafeStr(object o)
        {
            try { return o == null ? "(null)" : o.ToString(); } catch { return "(?)"; }
        }

        /// <summary>
        /// 把一个类型未知的对象的**公开属性**原样打出来。
        /// 用在 <c>CSGItem.Limits</c> / <c>Transform</c> 上 —— 官方 XML 没写它们的类型，
        /// **猜字段名不如把真实字段全摆出来**，拿到真数据再定判据。
        /// 只取无参属性、只打数值/字符串，遇到复杂对象再下一层（最多两层，防爆量）。
        /// </summary>
        private static string DumpProps(object o, int depth = 0)
        {
            if (o == null) return "(null)";
            var t = o.GetType();
            if (o is double || o is float || o is int || o is long || o is bool || o is string)
                return o is double d ? N(d) : o.ToString();
            if (depth >= 2) return SafeStr(o);
            var sb = new StringBuilder();
            sb.Append(t.Name).Append('{');
            bool first = true;
            try
            {
                foreach (var p in t.GetProperties())
                {
                    if (p.GetIndexParameters().Length != 0 || !p.CanRead) continue;
                    object v;
                    try { v = p.GetValue(o, null); } catch { continue; }
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append(p.Name).Append('=').Append(DumpProps(v, depth + 1));
                }
            }
            catch { }
            if (first) sb.Append(SafeStr(o));   // 一个属性都取不到 → 至少给个 ToString
            sb.Append('}');
            return sb.ToString();
        }

        private static string Fmt(double[] a)
        {
            if (a == null) return "(null)";
            var parts = new List<string>(a.Length);
            foreach (var d in a) parts.Add(N(d));
            // 长度也带上 —— 12 还是 16 决定了它是什么矩阵约定，别让 09 侧去数。
            return "[" + a.Length + "]{" + string.Join(",", parts.ToArray()) + "}";
        }

        // ── CSGPrimitivesVisitor 实现（12 个方法，签名逐字取自官方 XML 文档）─────
        public void VisitBox(DbElement e, CSGType t, double x, double y, double z, double[] tr, int tl)
            => Emit(e, t, "BOX", $"XLEN={N(x)}|YLEN={N(y)}|ZLEN={N(z)}", tr);

        public void VisitCylinder(DbElement e, CSGType t, double r, double h, double[] tr, int tl)
            => Emit(e, t, "CYLINDER", $"RADIUS={N(r)}|HEIGHT={N(h)}", tr);

        /// <summary>★ 封头 —— 这条就是 DISH 局部原点门控要的数据。</summary>
        public void VisitDish(DbElement e, CSGType t, double r, double h, CSGDishType dt, double[] tr, int tl)
            => Emit(e, t, "DISH", $"RADIUS={N(r)}|HEIGHT={N(h)}|DISHTYPE={dt}", tr);

        public void VisitSphere(DbElement e, CSGType t, double r, double[] tr, int tl)
            => Emit(e, t, "SPHERE", $"RADIUS={N(r)}", tr);

        public void VisitSnout(DbElement e, CSGType t, double rt, double rb, double h,
            double xo, double yo, double xt, double yt, double xb, double yb, double[] tr, int tl)
            => Emit(e, t, "SNOUT", $"RTOP={N(rt)}|RBOT={N(rb)}|HEIGHT={N(h)}|XOFF={N(xo)}|YOFF={N(yo)}", tr);

        // ★7 个 double，不是 8 —— 我第一版按 CSGPyramid 的文字描述数成了 8 个，
        //   编译器当场纠正（CS0535）。真实签名逐字：
        //   VisitPyramid(DbElement, CSGType, double×7, double[], int)
        //   命名按 CSGPyramid 文档「底/顶各一个矩形 + 高 + 剪切偏移」推，
        //   ⚠ 参数**顺序**未经真机核实 —— 所以下面把 7 个值原样全打出来，
        //     不按名字下结论（顺序猜错就是给一堆看着对的错标签）。
        public void VisitPyramid(DbElement e, CSGType t,
            double d1, double d2, double d3, double d4, double d5, double d6, double d7,
            double[] tr, int tl)
            => Emit(e, t, "PYRAMID",
                $"D1={N(d1)}|D2={N(d2)}|D3={N(d3)}|D4={N(d4)}|D5={N(d5)}|D6={N(d6)}|D7={N(d7)}"
                + "|NOTE=(7个尺寸原样回传:顺序未经真机核实)", tr);

        public void VisitRTorus(DbElement e, CSGType t, double ri, double ro, double h, double a, double[] tr, int tl)
            => Emit(e, t, "RTORUS", $"RINSIDE={N(ri)}|ROUTSIDE={N(ro)}|HEIGHT={N(h)}|ANGLE={N(a)}", tr);

        public void VisitCTorus(DbElement e, CSGType t, double ar, double sr, double a, double[] tr, int tl)
            => Emit(e, t, "CTORUS", $"ARCRADIUS={N(ar)}|SECTIONRADIUS={N(sr)}|ANGLE={N(a)}", tr);

        public void VisitLine(DbElement e, CSGType t, double x1, double y1, double z1,
            double x2, double y2, double z2, double[] tr, int tl)
            => Emit(e, t, "LINE", $"FROM={N(x1)},{N(y1)},{N(z1)}|TO={N(x2)},{N(y2)},{N(z2)}", tr);

        public void VisitArc(DbElement e, CSGType t, double r, double a, double[] tr, int tl)
            => Emit(e, t, "ARC", $"RADIUS={N(r)}|ANGLE={N(a)}", tr);

        // 面片组 / 拉伸：只报存在与规模，不展开顶点（一个网格几千个点，灌进回执没意义）。
        public void VisitFacetGroup(DbElement e, CSGType t, CSGFacetGroup fg, double[] tr, int tl)
            => Emit(e, t, "FACETGROUP", "DETAIL=(未展开:顶点数据不进回执)", tr);

        public void VisitExtrusion(DbElement e, CSGType t, CSGExtrusion ex, double[] tr, int tl)
            => Emit(e, t, "EXTRUSION", "DETAIL=(未展开:轮廓数据不进回执)", tr);
    }
}
