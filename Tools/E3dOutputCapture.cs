using System;
using System.Collections.Generic;
using System.Text;

namespace E3DMcpServer.Tools
{
    /// <summary>
    /// 把 E3D **打印到命令窗口的输出**捕获下来，让 agent 读得到。
    ///
    /// ═══ 为什么要有它 ═══════════════════════════════════════════════════════
    /// 今天插件给 agent 的反馈只有两种：
    ///   · 失败 → <c>Command.Error</c>（E3D 的真实报错文本）✅
    ///   · 成功 → **零内容**，回执是我们自己拼的固定串
    /// 而 <c>Q SPEC</c> / <c>$Q</c> / <c>LIST</c> 这类**打印型**命令的结果全进命令窗口，
    /// 插件唯一的取值通道 <c>GetStringFromPML</c> 只能读 PML 变量 —— 于是：
    ///   · <c>SpecQuery</c> 只能无条件回一句 "Queried spec 'X'."（既不证明存在也不证明可达）
    ///   · <c>$Q</c>（官方语法提示查询，手册反复写「真机 $Q 为准」）agent 完全拿不到
    ///
    /// ═══ 依据 ═══════════════════════════════════════════════════════════════
    /// AVEVA 自带的 .NET XML 文档（<c>Aveva.Core.Utilities.xml</c>）：
    ///   <c>Aveva.Core.Utilities.Messaging.PdmsOutputEvents</c>  "PDMS Message events"
    ///     · <c>OutputListener</c>  (delegate)  "Delegate taking a message and message type"
    ///     · <c>AddOutputListener(listener)</c>  "Add message listener"
    ///     · <c>RemoveOutputListener(listener)</c>
    ///
    /// ═══ ⚠ 未编译验证（读这段代码的人务必先看这里）═══════════════════════════
    /// 本文件是**在没有 AVEVA 编译环境的机器上**写的。已确认的是「API 存在 + 文档怎么说」；
    /// **没确认**的是委托的**参数类型**（XML 文档只写了 "a message and message type"，
    /// 没给签名）。所以：
    ///   1. 第一次编译时 IntelliSense 会直接告诉你 <c>OutputListener</c> 的真实签名 ——
    ///      按它改 <see cref="OnPdmsOutput"/> 的参数即可，其余逻辑不用动。
    ///   2. 万一这条路根本走不通（反射拿不到、委托签名对不上），
    ///      <see cref="TryAttach"/> 会**吞掉异常并返回 false**，
    ///      调用方一律退回旧行为 —— **绝不因为捕获不到就让主路径变坏**。
    ///
    /// ═══ 设计红线 ═══════════════════════════════════════════════════════════
    /// · **捕获失败 ≠ 命令失败**。两者必须分开报，否则就是拿"我没听见"当"它没说"。
    /// · 监听器是**进程级**的，必须配对摘除（<c>using</c> 作用域），否则会一直累积文本。
    /// · 非重入：同一时刻只允许一个捕获作用域（E3D 命令是单线程执行的）。
    /// </summary>
    internal sealed class E3dOutputCapture : IDisposable
    {
        private static readonly object _gate = new object();
        private static E3dOutputCapture _active;

        private readonly List<string> _lines = new List<string>();
        private Delegate _listener;
        private bool _attached;

        /// <summary>捕获到的行（未附着成功时为空）。</summary>
        public IReadOnlyList<string> Lines { get { return _lines; } }

        /// <summary>真的挂上监听器了吗。false = 这一轮拿不到输出（**不代表命令失败**）。</summary>
        public bool Attached { get { return _attached; } }

        /// <summary>附着失败的原因（Attached=true 时为 null）。如实报，不吞。</summary>
        public string AttachError { get; private set; }

        private E3dOutputCapture() { }

        /// <summary>
        /// 开一个捕获作用域。**永不抛异常** —— 挂不上就返回一个 Attached=false 的实例，
        /// 调用方照常执行命令、只是拿不到输出。
        /// </summary>
        public static E3dOutputCapture Begin()
        {
            var cap = new E3dOutputCapture();
            lock (_gate)
            {
                if (_active != null)
                {
                    // 非重入：已有作用域在跑（不该发生，E3D 命令单线程）。不抢，如实说明。
                    cap.AttachError = "已有一个输出捕获作用域在运行（非重入），本次跳过捕获";
                    return cap;
                }
                cap.TryAttach();
                if (cap._attached) _active = cap;
            }
            return cap;
        }

        /// <summary>
        /// 挂监听器。**用反射**而不是直接写 <c>PdmsOutputEvents.AddOutputListener(...)</c>：
        /// 委托签名本机确认不了，反射能让「签名对不上」变成一条运行期的诚实错误，
        /// 而不是一个**编译不过的插件**（那会让整个 MCP server 起不来 —— 代价完全不对等）。
        /// ★真机确认签名后，可以换成直接调用（更快、编译期就查得出错），
        ///   但**在那之前别换** —— 换了就等于拿整个插件赌一个没验过的签名。
        /// </summary>
        private void TryAttach()
        {
            try
            {
                var evType = Type.GetType(
                    "Aveva.Core.Utilities.Messaging.PdmsOutputEvents, Aveva.Core.Utilities", false);
                if (evType == null)
                {
                    AttachError = "找不到类型 Aveva.Core.Utilities.Messaging.PdmsOutputEvents";
                    return;
                }
                var delType = evType.GetNestedType("OutputListener");
                if (delType == null)
                {
                    AttachError = "找不到委托 PdmsOutputEvents.OutputListener";
                    return;
                }
                var add = evType.GetMethod("AddOutputListener",
                    new[] { delType });
                if (add == null)
                {
                    AttachError = "找不到方法 AddOutputListener(OutputListener)";
                    return;
                }

                // 按委托的**真实签名**绑我们的处理方法。参数个数/类型都从 delType 反推，
                // 所以文档没写签名也不影响 —— 对不上会在这里抛，被下面 catch 成一条诚实错误。
                var invoke = delType.GetMethod("Invoke");
                var ps = invoke != null ? invoke.GetParameters() : null;
                if (ps == null)
                {
                    AttachError = "委托 OutputListener 取不到 Invoke 签名";
                    return;
                }

                var handler = GetType().GetMethod(
                    ps.Length == 1 ? "OnPdmsOutput1" : "OnPdmsOutput",
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic);
                if (handler == null)
                {
                    AttachError = "本类没有与委托匹配的处理方法（参数个数 " + ps.Length + "）";
                    return;
                }

                _listener = Delegate.CreateDelegate(delType, this, handler, false);
                if (_listener == null)
                {
                    // 参数类型对不上（例如第一个参数不是 string 而是 PdmsMessage）。
                    // 如实报出真实签名，好照着改 —— 比"挂不上"三个字有用得多。
                    var sig = new StringBuilder();
                    foreach (var p in ps) { if (sig.Length > 0) sig.Append(", "); sig.Append(p.ParameterType.FullName); }
                    AttachError = "委托签名对不上，真实签名是 OutputListener(" + sig + ")"
                        + " —— 按它改本类的 OnPdmsOutput 参数即可，其余逻辑不用动";
                    return;
                }

                add.Invoke(null, new object[] { _listener });
                _attached = true;
            }
            catch (Exception ex)
            {
                AttachError = ex.GetBaseException().Message;
                _attached = false;
            }
        }

        // ── 两个候选处理方法：委托是几个参数就绑哪个（见 TryAttach）──────────────
        // ⚠ 参数类型按文档 "a message and message type" 猜成 (string, int)。
        //    真机第一次编译/运行会告诉你真实类型，照 AttachError 里报的签名改这里。
        private void OnPdmsOutput(object message, object messageType)
        {
            Add(message);
        }

        private void OnPdmsOutput1(object message)
        {
            Add(message);
        }

        /// <summary>
        /// 从 AVEVA 的消息对象里取**文本**。
        ///
        /// ★2026-07-29 真机第七跑抓到的 bug：这里原来直接 <c>message.ToString()</c>，
        ///   结果回执里全是 <c>Aveva.Core.Utilities.Messaging.Implementation.PdmsOutputImpl</c>
        ///   —— 那些实现类**没重写 ToString()**，打出来的是类型全名。
        ///   于是监听器明明挂上了、内容却全是垃圾，而且**把真相吞了**
        ///   （同一跑里四条 COLLECT 的真实报错也是这么丢的）。
        ///
        /// 正路：官方 XML 文档 <c>PdmsMessage.MessageText</c>
        ///   "Obtains message text, filling in any substitutions"。
        /// 用反射按 方法 → 属性 → ToString 三级降级：传进来的对象类型不确定
        ///（PdmsMessage / PdmsOutput 都可能），写死一种会再摔一次。
        /// </summary>
        private static string TextOf(object o)
        {
            if (o == null) return null;
            if (o is string s0) return s0;
            try
            {
                var t = o.GetType();
                var m = t.GetMethod("MessageText", Type.EmptyTypes);
                if (m != null)
                {
                    var v = m.Invoke(o, null);
                    if (v != null) return v.ToString();
                }
                var p = t.GetProperty("MessageText");
                if (p != null)
                {
                    var v = p.GetValue(o, null);
                    if (v != null) return v.ToString();
                }
                // ★PdmsOutput 一族没有 MessageText —— 文本在 Description / Details
                //  （官方 XML：Description "Message description" · Details "Message details"）。
                //   第八跑实证：监听器收到的正是 PdmsOutputImpl，上一轮只按 PdmsMessage 修，
                //   所以还是回"只拿到类型名"。两个类不一样，取法也不一样。
                string desc = null, det = null;
                var pd = t.GetProperty("Description");
                if (pd != null) { var v = pd.GetValue(o, null); if (v != null) desc = v.ToString(); }
                var pt = t.GetProperty("Details");
                if (pt != null) { var v = pt.GetValue(o, null); if (v != null) det = v.ToString(); }
                if (!string.IsNullOrWhiteSpace(desc) || !string.IsNullOrWhiteSpace(det))
                {
                    if (string.IsNullOrWhiteSpace(det)) return desc;
                    if (string.IsNullOrWhiteSpace(desc)) return det;
                    return desc + " | " + det;
                }
            }
            catch { /* 取文本失败就退回 ToString，不抛 */ }
            var fallback = o.ToString();
            // ★退到 ToString 时如实标注：类型全名不是消息文本，别让它冒充内容。
            if (!string.IsNullOrEmpty(fallback) && fallback.StartsWith("Aveva."))
                return "(取不到消息文本，只拿到类型名 " + fallback + ")";
            return fallback;
        }

        private void Add(object message)
        {
            try
            {
                if (message == null) return;
                var s = TextOf(message);
                if (string.IsNullOrEmpty(s)) return;
                lock (_gate)
                {
                    // 上限保护：E3D 有些命令会刷屏，别把内存吃穿。超了丢最旧的。
                    if (_lines.Count >= 2000) _lines.RemoveAt(0);
                    _lines.Add(s);
                }
            }
            catch { /* 捕获通道自身绝不能抛 */ }
        }

        /// <summary>捕获到的文本（无则空串）。</summary>
        public string Text()
        {
            lock (_gate) { return string.Join("\n", _lines.ToArray()); }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_attached && _listener != null)
                {
                    try
                    {
                        var evType = Type.GetType(
                            "Aveva.Core.Utilities.Messaging.PdmsOutputEvents, Aveva.Core.Utilities", false);
                        var delType = evType != null ? evType.GetNestedType("OutputListener") : null;
                        var rem = evType != null && delType != null
                            ? evType.GetMethod("RemoveOutputListener", new[] { delType })
                            : null;
                        if (rem != null) rem.Invoke(null, new object[] { _listener });
                    }
                    catch { /* 摘不掉也不能抛；最坏是多留一个监听器 */ }
                }
                if (ReferenceEquals(_active, this)) _active = null;
                _attached = false;
                _listener = null;
            }
        }
    }
}
