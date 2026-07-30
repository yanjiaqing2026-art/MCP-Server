using System.Collections.Generic;
using E3DMcpServer.Models;

namespace E3DMcpServer.Tools
{
    public static class E3dToolDefinitions
    {
        public static List<ToolDefinition> All => _all.Value;
        private static readonly System.Lazy<List<ToolDefinition>> _all =
            new System.Lazy<List<ToolDefinition>>(() => Build());

        static List<ToolDefinition> Build()
        {
            return new List<ToolDefinition>
            {
                // ══ 查询 ═══════════════════════════════════
                T("e3d_ce_get", "获取 E3D 中当前选中元素(CE)的名称、类型和坐标。返回格式:  '名称: /PIPE-001\\n类型: PIPE\\n坐标: X=8128637 Y=6205447 Z=41727'。这是查看'当前元素是什么'的首选工具。通常与 e3d_element_dump 配合使用：先 CE_GET 确认元素，再 DUMP 看全部属性。", NoParams()),

                T("e3d_element_get", "按名称/路径获取元素信息。返回格式同 e3d_ce_get: '名称: X\\n类型: Y\\n坐标: ...'。参数 name 必须用完整路径如 /PIPE-001 或 /ZONE-01/BRAN-01。若元素不存在返回错误。",
                   P("name:string:元素完整路径,如 /PIPE-001 或 /*。必填。"), R("name")),

                T("e3d_element_children", "列出元素的直接子元素(members)。返回格式: 'N children:\\n  [TYPE] NAME @ (X,Y,Z)'。可选 type_filter 按类型过滤(如 ELBO,VALV,PIPE)。用于探索 E3D 层级结构。",
                   P("name:string:父元素路径。留空使用 CE。可选。"),
                   P("type_filter:string:按类型过滤,可用: PIPE,BRAN,ELBO,VALV,NOZZ,TEE,ZONE,SITE,EQUI。可选。")),

                T("e3d_element_owner", "获取元素的直接父级(Owner)。返回格式同 e3d_ce_get。用于向上导航: 从 PIPE 找 BRAN, 从 BRAN 找 ZONE。",
                   P("name:string:元素路径。留空使用 CE。可选。")),

                T("e3d_attr_read", "读取元素的属性值。返回格式: 每行 'KEY: VALUE'。常用属性: ODIA(外径mm), WALL(壁厚mm), MTYP(材料), SPREF(管等级), SCHD(Schedule), PRES(设计压力), TEMP(设计温度℃), SBOR(公称口径mm), PURP(用途), FUNC(功能)。若指定 attrs 只返回匹配项, 留空返回 NAME,TYPE,DESC,PURP,POS,OWNER。用于查询管道设计参数。",
                   P("name:string:元素路径。留空使用 CE。可选。"),
                   P("attrs:string:属性名列表,英文逗号分隔。例: 'ODIA,WALL,TEMP'。可选。")),

                T("e3d_search", "按类型搜索 E3D 数据库中的元素。返回格式: 'PIPE search: N results\\n  [PIPE] /NAME @ (X,Y,Z)'。filter 语法: 'ATTR OP VALUE', OP 可用: GT(大于), LT(小于), GE(≥), LE(≤), EQ(等于), NE(不等于)。例: filter='ODIA GT 200' 搜外径>200mm 的管道。max 默认50, 最大200。用于查找特定条件的管道/设备。",
                   P("type:string:元素类型。可用: PIPE,BRAN,ELBO,VALV,NOZZ,TEE,ZONE,SITE,EQUI,STRU。必填。"),
                   P("filter:string:过滤表达式,如 'ODIA GT 150' 或 'NAME EQ PIPE-101'。可选。"),
                   P("max:integer:最大返回数,默认50。可选。"),
                   R("type")),

                T("e3d_collect_geometry", "递归收集模型里**有真实世界坐标的几何组件**(TUBE/ELBO/TEE/法兰/阀门/喷嘴/设备等),供实时 3D 镜像 E3D 真实显示。区别于 e3d_search 只返回管线头(容器节点,坐标退化→前端会把多根管叠成一点)。从 root 递归下钻,纯容器(SITE/ZONE/PIPE/BRAN,坐标退化)只下钻不收集。返回格式: 'geometry: N components\\n  [TYPE] /NAME @ (X,Y,Z)'。",
                   P("root:string:起始路径,留空或 /* 表示整库。可选。"),
                   P("max:integer:最大返回组件数,默认2000。可选。")),

                T("e3d_export_rvm", "导出真实 RVM 网格几何(AVEVA 原生 EXPORT)到本机临时文件,供实时 3D 用与「模型审查」同款加载器显示**真 CAD 形状**(非 e3d_collect_geometry 的示意基本体)。返回 JSON: {ok:true,path,name} 或 {ok:false,error}。root 留空=导出当前选中(CE)。比 e3d_collect_geometry 重(几MB/几秒),按需调用。",
                   P("root:string:起始路径,留空=当前选中元素(CE)。可选。")),

                T("e3d_project_info", "获取 E3D 项目信息。返回: 项目名(project), 数据库(mdb), 当前用户(user), 当前模块(module)。用于确认当前工作环境。", NoParams()),

                T("e3d_measure", "测量两个元素之间的直线距离。返回格式: 'Distance: 2500.000 mm (dX=... dY=... dZ=...)'. 用于检查管道间距、碰撞评估。",
                   P("name1:string:第一个元素的完整路径。必填。"),
                   P("name2:string:第二个元素的完整路径。必填。"),
                   R("name1", "name2")),

                T("e3d_element_type", "获取元素的 E3D 内部类型名。返回格式: 'Type: PIPE' 或 'Type: ELBO'。用于确认元素的准确类型分类。",
                   P("name:string:元素路径。留空使用 CE。可选。")),

                T("e3d_element_path", "获取元素在 E3D 数据库中的完整路径(FQN)。返回类似 '/SITE-1/ZONE-A/PIPE-101'。用于了解元素在层级中的位置, 或为其他工具准备路径参数。",
                   P("name:string:元素路径。留空使用 CE。可选。")),

                // ══ 修改 ═══════════════════════════════════
                T("e3d_attr_set", "设置 E3D 元素的属性值。修改后需调用 e3d_db_save 持久化。常用属性: ODIA(外径), WALL(壁厚), MTYP(材料), SPREF(管等级), SCHD, PRES(压力), TEMP(温度)。例: 修改设计温度 → attr='TEMP', value='250'。返回格式: 'OK: set ATTR=VALUE on NAME'。",
                   P("name:string:元素完整路径。必填。"),
                   P("attr:string:属性名,如 ODIA,WALL,MTYP,SPREF,TEMP,PRES。必填。"),
                   P("value:string:新值。数字属性如 ODIA=219.1, 字符串属性如 MTYP='A106 Gr.B'。必填。"),
                   R("name", "attr", "value")),

                T("e3d_element_create", "在 E3D 中创建新元素。type 可用: PIPE, BRAN, ELBO, VALV, NOZZ, TEE, ZONE, SITE, EQUI, STRU, BOX 等。创建后必须设置属性(ODIA,WALL,MTYP,SPREF)并 SAVEWORK。owner 指定父级路径, 如 '/*' (根) 或 '/ZONE-01'。返回格式: 'OK: created TYPE NAME'。",
                   P("type:string:元素类型(PIPE,BRAN,ELBO,VALV,NOZZ,TEE,ZONE,SITE,EQUI,STRU)。必填。"),
                   P("name:string:新元素名称。必填。"),
                   P("owner:string:父元素路径,如 '/*' 或 '/ZONE-01'。可选, 默认当前 CE 的 owner。"),
                   R("type", "name")),

                T("e3d_element_delete", "删除 E3D 中的元素。⚠ 不可逆! 删除前建议先 SAVEWORK。name 必须用完整路径。危险操作：必须先向用户确认，再带 confirm='true' 调用，否则被拒。返回格式: 'OK: deleted NAME'。",
                   P("name:string:要删除的元素完整路径。必填。"),
                   P("confirm:string:危险操作确认，必须显式传 'true'（先向用户确认）。必填。"),
                   R("name", "confirm")),

                T("e3d_element_rename", "重命名 E3D 元素。例: 将 /PIPE-OLD 改为 /PIPE-NEW。返回格式: 'OK: renamed OLD to NEW'。",
                   P("name:string:当前完整路径。必填。"),
                   P("new_name:string:新名称。必填。"),
                   R("name", "new_name")),

                T("e3d_element_copy", "复制 E3D 元素到目标位置。source 为源元素路径, dest 为目标父级路径。返回格式: 'OK: copied SOURCE to DEST'。",
                   P("source:string:源元素完整路径。必填。"),
                   P("dest:string:目标父级路径。必填。"),
                   R("source", "dest")),

                T("e3d_element_move", "移动元素到新的父元素下。name 为要移动的元素, new_owner 为新父级路径。返回格式: 'OK: moved NAME to OWNER'。",
                   P("name:string:要移动的元素路径。必填。"),
                   P("new_owner:string:新父元素路径。必填。"),
                   R("name", "new_owner")),

                // ══ PML ══════════════════════════════════
                T("e3d_pml_exec", "在 E3D 中执行任意 PML 命令(真写库)。🚨 完全数据库访问权限, 请谨慎! 支持所有 PML 语法: NEW/DELETE/MOVE/BY/POS/SAVEWORK/UNDO 等。PML 语法: 元素操作如 'NEW PIPE /NAME', 属性设置如 '!!CE.ODIA = 219.1', 查询如 'Q VAR !!CE.ODIA'。返回 'Command executed.' 或错误信息。⚠ 建模/定位/连接类动词(ROTATE/CONNECT/POS…)作用于 CE(当前元素)、不接前导元素名——先 '!!CE = /目标' 导航再发裸命令。⚠ e3d_pml_eval 只求值表达式、【不能】对建模命令做语法 dry-run。",
                   P("command:string:完整 PML 命令字符串。必填。"), R("command")),

                // ★2026-07-29 新增两个（✅ 已编译通过 · ⚠ **真机未跑过**，见各自 .cs 头注的确认步骤）
                T("e3d_pml_exec_verbose", "执行 PML 命令并**把 E3D 打印到命令窗口的输出一起带回来**。与 e3d_pml_exec 的区别: 后者只回'成/败 + 失败时的报错', 成功时零内容, 而 Q SPEC / $Q / LIST 这类【打印型】命令的结果全进命令窗口拿不到。用它可以真正读到: $Q 语法提示(官方的下一个合法命令词, 手册说'真机 $Q 为准')、Q SPEC 的规格清单、Q ATT 的属性表。回执分两段: 命令成败 + E3D 输出; ★捕获失败会单独说明 —— 那只说明【我们没听见】, 不说明命令没输出、更不说明命令失败。",
                   P("command:string:完整 PML 命令字符串。必填。"), R("command")),

                // ★2026-07-30 —— 等级驱动选型，走 AVEVA 原生 Aveva.E3D.Select（不是发 SELECT 命令）。
                T("e3d_spec_select", "★**等级驱动选型**（配管 + 暖通）。走 AVEVA 原生 Aveva.E3D.Select: Select.InSpecification/With/WithPurpose → ComponentSelection.AvailableComponents/Create/**GetMessages**。取代'发一条 SELECT 再读 SPREF 看空不空'那种试错 —— **AvailableComponents 直接回答'这个等级里到底有没有这类件', GetMessages 直接回答'为什么选不到'**。mode: **probe**(★第一次必跑, 验 PMLNetAny 拿不拿得到 —— 那是整条链的门槛) / available(列可选构件) / hvac(暖通有哪些类型与尺寸)。★结构型钢**不走这条**(它是问答式 StructuralSpec, 另一个程序集) —— SCTN 发 SELECT 回 (61,28) 就是走错范式。",
                   P("mode:string:probe / available / hvac。默认 probe。可选。"),
                   P("spec:string:等级名(★现查现用, 不要写死)。available 必填。"),
                   P("type:string:构件类型码, 如 'ELBO' / 'GASK'。可选(空=列全部)。"),
                   P("attr:string:过滤属性名, 如 'NPD'。可选。"),
                   P("value:string:过滤属性值, 如 '150'。可选。"),
                   P("purpose:string:用途码。可选。")),

                // ★2026-07-31 分析批 —— 全量索引钻完后筛的四块。
                T("e3d_analysis", "★**分析批**（四个动作）: `clash`=**原生碰撞检查**(走 AVEVA Clasher, 结果分**四类**: 撞上/接触/净距不足/**证不了**, 每条带**碰撞点坐标** —— 比自研空间索引多了'碰撞点'与'证不了'两项) · `inbox`=**空间盒内有哪些元素**(给两个对角点) · `rule`=**表达式规则判定**(E3D 内建规则引擎, 如 `HBORE GT 100`, 逐个元素判过/不过/**判不了**) · `assoc`=**P&ID↔3D 关联**(这根管子对应流程图上哪条线; **只读**, 建/断关联属工程决策不由 agent 做)。★全部只读。",
                   P("action:string:clash | inbox | rule | assoc。必填。"),
                   P("names:string:元素路径逗号分隔(clash/rule/assoc) 或 盒的第一个角点 `x,y,z`(inbox)。必填。"),
                   P("arg1:string:clash=障碍体路径(可选) · inbox=第二个角点 `x,y,z` · rule=**PML 表达式** · assoc=不用。"),
                   P("arg2:string:inbox=类型过滤(逗号分隔, 可选)。"),
                   P("max:integer:每类最多列多少条, 默认100。★超了会**明说不是没有**。可选。"),
                   R("action"), R("names")),

                // ★2026-07-31 —— 回答「MCP 能不能完全拿到 E3D 反馈」的那一半。
                T("e3d_datal_dump", "★**把元素的全部数据导出来**（走 AVEVA 官方 DatalListing，即 E3D 里 DATAL 命令背后的引擎）。用于: 一次拿到一批元素的完整属性/结构, 比逐个 attr_read 快一个数量级; 也用于把工程师建好的真实构件**整份读出来照抄**。★与 e3d_attr_read 的分工: attr_read 点名读几个属性, 本工具**全量导**。⚠ **命令窗口的正常打印(Q/LIST 那类)MCP 拿不到** —— PdmsOutputEvents 只流错误, 那是能力边界; 但**数据本身**走这条能全量拿到。",
                   P("names:string:要导出的元素路径, 逗号分隔。必填。"),
                   P("brief:boolean:简表(true)还是完整(false), 默认 false。可选。"),
                   P("comments:boolean:是否含注释, 默认 false。可选。"),
                   P("max_chars:integer:回执最多多少字符, 默认60000。★超了会**明说已截断**。可选。"),
                   R("names")),

                // ★2026-07-31 原生 API 批（全量索引挖完后按「签名清楚+对着已知痛点」筛的五个）。
                T("e3d_native_ops", "★**AVEVA 原生 API 杂项**（一个工具五个动作，全部离线可答、不发 PML）: `bore`=官方通径换算(我们一直自己算, 栽过'小1000倍') · `members`=成员遍历(解掉 !!ce.MEM 拿不回数组那个缺口, 可按类型过滤) · `copytree`=**整棵子树复制**(typical 复用: 把建好的一台泵/一榀框架复用到另一处) · `attrvalid`=**这个类型有没有这个属性**(此前要发一条看回不回 (2,201)) · `namecheck`=名字格式预检(此前要真机回 (41,12) 才知道)。★copytree 是**写操作且不可逆**。",
                   P("action:string:bore | members | copytree | attrvalid | namecheck。必填。"),
                   P("name:string:元素路径(members/copytree源) 或 类型码(attrvalid) 或 类型码(namecheck, 可选)。"),
                   P("arg1:string:members=类型过滤(可选) · copytree=复制到谁后面 · attrvalid=属性名 · bore=通径值 · namecheck=候选名字。"),
                   P("arg2:string:bore 的第二个参数(Int32, 官方没说明含义, 原样透传)。可选。"),
                   P("max:integer:members 最多列多少个, 默认200。可选。"),
                   R("action")),

                // ★2026-07-30 —— 用户点破「答案其实都在 dll 和 xml 里」之后加的。
                // DbElementType 上每一条都是我们试了三四跑的问题的现成答案。
                T("e3d_type_schema", "★**查元素类型的完整 schema**: 合法 owner 类型 / 可包含的成员类型 / 真实属性名单 / 可连接数 / 短名与全名 / 所在数据库类型。走 AVEVA 原生 DbElementType(OwnerTypes/MemberTypes/SystemAttributes/ValidConnections/ShortName)。**不带 type 参数 = 列出全部类型**(含短名, 直接解掉 SPEC vs SPECIFICATION 那类别名坑)。用于: 建元素前确认该建在什么下面(不用真机试错)、判某类型有没有某属性(不用发一条看回不回 (2,201))、判某类型能不能 CONNECT。★这份是**权威的禁止面** —— ATT 统计只能证明'允许', 证明不了'禁止'。",
                   P("type:string:元素类型码, 如 'NOZZ' / 'SPECIFICATION'。**留空 = 列出全部类型**。可选。"),
                   P("max:integer:列全部时最多返回多少个, 默认400。可选。")),

                // ★2026-07-30 第十三跑之后新增 —— 让 agent 能**按条件批量查 E3D**。
                // 依据: AVEVA 自带 XML 文档 DbCollection.Parse/Evaluate（逐字，不是猜的）。
                // 走过的两条弯路见 E3dCollectQuery.cs 头注（多行 PML 的块结构 · 临时宏）。
                T("e3d_collect_query", "★**按条件批量查元素**（COLLECT）。传 PML1 **选择准则**, 用 AVEVA 原生 DbCollection.Parse+Evaluate —— 不跑 PML 文本、不写临时文件、错误从 PdmsMessage 出真文本。用于: 列全部管等级/设备/某类型元素、按条件筛(WHERE)、限定作用域(FOR)。★criteria 只传准则本身, **不要**带 'VAR !x COLLECT' 前缀(官方例子: 'ALL BOX WHERE (XLEN + 10) FOR /MYSITE')。返回: 'collect: N elements (criteria: X)\n  名字|属性=值'。★超过 max 会**明说另有多少个未列出**, 不悄悄截断。",
                   P("criteria:string:PML1 选择准则, 如 'ALL SPEC' / 'ALL EQUI FOR /SITE-A' / 'ALL BOX WHERE (XLEN GT 1000)'。必填。"),
                   P("max:integer:最多返回多少个, 默认200。可选。"),
                   P("attrs:string:每个元素额外读回的属性名, 逗号分隔, 如 'PSPEC,HBORE'。可选。"),
                   R("criteria")),

                T("e3d_csg_dump", "读一个元素的**真实几何图元**: 类型(BOX/CYLINDER/DISH/SNOUT/CTORUS…)、真实尺寸、以及**变换矩阵**。区别于 e3d_collect_geometry(只回 @ (X,Y,Z) 位置 + 几个字符串属性, 没有包围盒/变换/真尺寸)。用于: 判断图元的局部原点在哪(如 DISH 封头原点在底面还是拱顶)、取保温包络与障碍体的真实尺寸、写后验收比几何而不只比属性。返回格式: 'csg: N primitives (root: X)\\n  [DISH] 名|CSGTYPE=..|RADIUS=..|HEIGHT=..|TRANSFORM=[n]{..}'。★变换矩阵原样回传不做解释(行/列主序未经证实, 猜一个说法比不说更坏)。",
                   P("name:string:元素路径。必填。"),
                   P("max:integer:最多返回多少个图元, 默认200。可选。"),
                   P("insulation:boolean:是否包含保温包络几何, 默认 false。可选。"),
                   P("obstruction:boolean:是否包含障碍体几何, 默认 false。可选。"),
                   P("centerline:boolean:是否包含管道中心线, 默认 false。可选。"),
                   R("name")),

                T("e3d_pml_eval", "求值一个 PML【表达式】并返回其值(内部 '!!x = <expr>' 真执行求值, 不改库)。用于取属性/算术, 例: expression='!!CE.ODIA' 返回外径, expression='2 * 3.14159 * 100' 返回算术结果。⚠ 这【不是】命令语法 dry-run——不能拿它'试运行' NEW/CONNECT/POS 等建模命令看语法对不对(它只跑表达式求值)。要验建模命令是否正确: 离线用 09 侧 PML lint(发送前静态校验), 真机看 e3d_pml_exec 的真实回执 + %TEMP%\\pipingclaw_e3d_pml.log。",
                   P("expression:string:PML 表达式(不是命令)。必填。"), R("expression")),

                // ══ 批量 ═══════════════════════════════════
                T("e3d_batch_read", "批量读取多个元素的属性。elements 用逗号分隔路径列表。attrs 用逗号分隔属性名列表。返回格式: '--- NAME ---\\n  KEY: VALUE' 每组一段。用于同时查看多个管道的参数对比。",
                   P("elements:string:元素路径列表,英文逗号分隔。必填。"),
                   P("attrs:string:属性名列表,英文逗号分隔。必填。"),
                   R("elements", "attrs")),

                T("e3d_batch_set", "批量设置多个元素的同一属性为相同值。⚠ 会修改所有指定元素! elements 用逗号分隔。返回每元素的操作结果。",
                   P("elements:string:元素路径列表,英文逗号分隔。必填。"),
                   P("attr:string:属性名。必填。"),
                   P("value:string:要设置的值。必填。"),
                   R("elements", "attr", "value")),

                T("e3d_collect", "收集指定类型的所有元素, 可选过滤条件和返回属性。类似 e3d_search + e3d_attr_read 的组合。返回格式: 'Found N TYPE elements:\\n  [TYPE] NAME\\n    KEY: VALUE'。",
                   P("type:string:元素类型(PIPE,BRAN,ELBO,VALV等)。必填。"),
                   P("filter:string:过滤表达式,如 'ODIA GT 150'。可选。"),
                   P("attrs:string:要返回的属性列表,逗号分隔。可选, 留空只返回名称和类型。"),
                   R("type")),

                T("e3d_pipeline_export", "导出当前选中管道的完整层级树: PIPE→BRAN→ELBO/VALV 等子组件, 每级带属性。返回格式: 缩进树形 '[TYPE] NAME\\n  KEY: VALUE'。用于理解管道结构和发现子组件。",
                   P("pipe_name:string:管道完整路径。留空使用 CE 或向上查找最近 PIPE。可选。")),

                // ══ 导航 ═══════════════════════════════════
                T("e3d_navigate", "将 E3D 当前元素(CE)导航到指定路径。path 支持: 完整路径如 '/ZONE-01/BRAN-01', 相对路径如 'OWNER'(父级), 特殊值 '/*'(世界根)。导航后, 后续不带 name 参数的工具调用都针对新 CE。",
                   P("path:string:E3D 路径或特殊标识(OWNER/MEMBERS/CEPARENT/*)。必填。"),
                   R("path")),

                T("e3d_select", "在 E3D 3D 视图中选中并高亮指定元素。name 必须用完整路径。返回: 'Navigated to: [TYPE] NAME'。用于在视图中定位和可视化元素。",
                   P("name:string:元素完整路径。必填。"),
                   R("name")),

                // ══ 数据库 ═══════════════════════════════════
                T("e3d_db_save", "保存所有修改到 E3D 数据库(SAVEWORK)。在执行任何修改操作(attr_set, create, delete 等)后, 必须调用此工具才能持久化。危险操作（落库不可逆）：必须先向用户确认，再带 confirm='true' 调用。返回: 'Database saved.'。",
                   P("confirm:string:危险操作确认，必须显式传 'true'（先向用户确认）。必填。"),
                   R("confirm")),

                T("e3d_db_changes", "查看自上次 SAVEWORK 以来所有变更的元素列表(GETCHANGES)。返回格式: 'GETCHANGES executed.' (输出到 E3D 控制台)。用于确认修改了哪些元素。", NoParams()),

                T("e3d_db_undo", "撤销上一步操作(UNDO)。只能撤销未保存的修改。返回: 'Undo executed.'。", NoParams()),

                T("e3d_db_extract", "从 E3D 数据库提取指定类型的所有元素到当前会话(EXTRACT ALL)。type 如 PIPE, ZONE, SITE 等。危险操作（整库范围动作）：必须带 confirm='true'。返回格式: 'Extracted all TYPE.'。",
                   P("type:string:元素类型。必填。"),
                   P("confirm:string:危险操作确认，必须显式传 'true'。必填。"),
                   R("type", "confirm")),

                // ══ 元素生命周期 ═══════════════════════════
                T("e3d_element_exists", "检查元素是否存在于数据库中。返回: 'TRUE: NAME exists.' 或 'FALSE: NAME does not exist.'。用于在操作前确认元素存在。",
                   P("name:string:元素完整路径。必填。"), R("name")),

                T("e3d_element_equals", "判断两个路径是否指向同一个数据库对象。返回: 'TRUE: same element.' 或 'FALSE: different elements.'。用于比较不同路径是否指向同一元素。",
                   P("name1:string:第一个元素路径。必填。"),
                   P("name2:string:第二个元素路径。必填。"),
                   R("name1","name2")),

                T("e3d_element_revert", "撤销对指定元素的修改, 恢复到上次保存的状态(REVERT)。可选 attr 参数只恢复指定属性。返回格式: 'Reverted NAME'。",
                   P("name:string:元素路径。可选。"),
                   P("attr:string:指定要恢复的属性名。可选, 留空恢复全部。")),

                T("e3d_element_dump", "导出元素的所有已知属性值。返回格式: '=== [TYPE] NAME ===\\n  KEY: VALUE' 每行一个属性。涵盖 NAME,TYPE,OWNER,POS,ORI,DIR,ODIA,WALL,MTYP,PRES,TEMP,SBOR,SCHD,SPREF,BORE,RADIUS,ANGLE,LENGTH,CONN,ARRIVE,LEAVE,HEAD,TAIL,CTYPE,STYPE,TTYP 等。用于全面了解一个元素的所有设计数据。",
                   P("name:string:元素路径。留空使用 CE。可选。")),

                // ══ 层次导航 ═══════════════════════════════
                T("e3d_occurrence", "获取元素在设计/布置层次中的出现实例(Occurrence)。返回: 'Occurrence queried.'。用于理解设计层级中的实例关系。",
                   P("name:string:元素路径。可选。")),

                T("e3d_siblings", "列出与当前元素同级的所有兄弟元素。返回格式: 'Siblings of [TYPE] NAME:\\n  [TYPE] Name @ (X,Y,Z)'。用于浏览同一层级的所有元素。",
                   P("name:string:元素路径。可选。")),

                T("e3d_world", "获取 E3D 数据库的根元素(WORLD = /*)。返回格式同 e3d_ce_get。用途: 从根开始向下导航, 探索整个数据库树。", NoParams()),

                T("e3d_wrt", "获取元素的参考坐标系(WRT - With Respect To)。返回格式: 'WRT queried.'。用于了解元素的坐标系参考。",
                   P("name:string:元素路径。可选。")),

                // ══ 属性 ═══════════════════════════════════
                T("e3d_attr_list", "列出元素的所有可用属性名及其当前值。返回格式: 'Attributes of NAME:\\n  ATTR: VALUE'。如果只想看特定属性用 e3d_attr_read, 如果想知道'这个元素有哪些属性'用本工具。",
                   P("name:string:元素路径。可选。")),

                T("e3d_attr_info", "查询 E3D 属性的定义信息(类型、单位、有效值范围等)。返回: 'Queried attribute ATTR'。用于了解属性含义和约束。",
                   P("attr:string:属性名,如 ODIA,WALL,SPREF。必填。"),
                   P("type:string:元素类型,如 PIPE,BRAN。可选, 留空查通用定义。")),

                // ══ 几何 ═══════════════════════════════════
                T("e3d_pos_set", "设置元素的绝对位置。坐标单位为 mm。X=东(East), Y=北(North), Z=高(Up)。返回格式: 'Set NAME position to (X, Y, Z)'。",
                   P("name:string:元素完整路径。必填。"),
                   P("x:number:X 坐标(East, mm)。必填。"),
                   P("y:number:Y 坐标(North, mm)。必填。"),
                   P("z:number:Z 坐标(Up, mm)。必填。"),
                   R("name","x","y","z")),

                T("e3d_pos_move", "相对偏移元素位置(BY)。dx=X方向, dy=Y方向, dz=Z方向(mm)。正值向东/北/上。返回格式: 'Moved NAME by (dx, dy, dz)'。",
                   P("name:string:元素完整路径。必填。"),
                   P("dx:number:X 方向偏移(mm)。必填。"),
                   P("dy:number:Y 方向偏移(mm)。必填。"),
                   P("dz:number:Z 方向偏移(mm)。必填。"),
                   R("name","dx","dy","dz")),

                T("e3d_orientation_get", "获取元素的朝向(ORI)和方向(DIR)向量。返回格式: 'Orientation of NAME:\\n  ORI: ...\\n  DIR: ...'。用于了解管道走向。",
                   P("name:string:元素路径。可选。")),

                T("e3d_rotate", "旋转元素。axis 为旋转轴(X,Y,Z 或方向向量), angle 为旋转角度(度)。返回: 'Rotated NAME about AXIS by ANGLE deg.'。",
                   P("name:string:元素路径。必填。"),
                   P("axis:string:旋转轴(X/Y/Z 或向量)。必填。"),
                   P("angle:number:旋转角度(度)。必填。"),
                   R("name","axis","angle")),

                T("e3d_reverse", "反转元素的方向。返回格式: 'Reversed NAME.'。用于翻转管道方向(Flow Direction)。",
                   P("name:string:元素完整路径。必填。"), R("name")),

                // ══ 连接 ═══════════════════════════════════
                T("e3d_connect", "连接两个元素(如 HEAD 到 TAIL)。name1 连接到 name2。返回格式: 'Connected NAME1 to NAME2.'。用于管道组对连接。",
                   P("name1:string:第一个元素路径。必填。"),
                   P("name2:string:第二个元素路径。必填。"),
                   R("name1","name2")),

                T("e3d_disconnect", "断开元素的所有连接。返回格式: 'Disconnected NAME.'。",
                   P("name:string:元素完整路径。必填。"), R("name")),

                // ══ 阵列 ═══════════════════════════════════
                T("e3d_array_add", "向元素阵列添加成员。array 为目标阵列路径, member 为要添加的元素。返回格式: 'Added MEMBER to ARRAY.'。",
                   P("array:string:目标阵列元素路径。必填。"),
                   P("member:string:要添加的成员路径。必填。"),
                   R("array","member")),

                T("e3d_array_remove", "从元素阵列移除指定位置的成员。index 从 1 开始。返回格式: 'Removed index N from ARRAY.'。",
                   P("array:string:目标阵列元素路径。必填。"),
                   P("index:integer:要移除的索引(1-based)。必填。"),
                   R("array","index")),

                T("e3d_array_sort", "对元素阵列排序。返回格式: 'Sorted ARRAY.'。",
                   P("array:string:目标阵列元素路径。必填。"), R("array")),

                // ══ 规格/目录 ═════════════════════════════
                T("e3d_spec_query", "查询 E3D 规格(SPEC)或目录(CAT)的详细信息。spec 为规格名称(如 1C003, 3S001), 留空查全部。返回: 'Queried spec SPECNAME.'。用于了解可用规格及其参数。",
                   P("spec:string:规格名称,如 '1C003' 或 '3S001'。可选, 留空查全部。")),

                T("e3d_spec_list", "列出项目里所有可用的管道规格(SPEC)名——「选管等级」时用它枚举真实可选项(agent 不再瞎猜规格名)。返回: 'Spec list: N specs\\n  /规格名...'。枚举不到时诚实说明(规格可能在独立目录/spec库,需真机核实根)。",
                   P("max:integer:最多返回多少个规格,默认500。可选。")),

                T("e3d_bom", "查询元素的材料表(BOM - Bill of Materials)。返回: 'Bill of materials queried for NAME.'。用于导出材料清单。",
                   P("name:string:元素完整路径。必填。"), R("name")),

                T("e3d_component_info", "获取管件的类型信息(CTYPE 组件类型, STYPE 子类型, TTYP 温度类型, DTSE 数据设置, SPWL 规格壁厚, MTYS 材料屈服应力)。返回格式: 'Component info for NAME:\\n  ATTR: VALUE'。用于了解管件的具体规格参数。",
                   P("name:string:元素路径。可选。")),

                // ══ 视图 ═══════════════════════════════════
                T("e3d_view_zoom", "将 E3D 3D 视图缩放到指定元素。返回格式: 'Zoomed to NAME.'。用于在视图中定位元素。",
                   P("name:string:元素完整路径。必填。"), R("name")),

                T("e3d_view_fit", "将 E3D 3D 视图适配到所有可见元素(FIT)。返回: 'View fitted.'。", NoParams()),

                T("e3d_view_colour", "设置元素在 E3D 3D 视图中的显示颜色。colour 可用: RED, GREEN, BLUE, YELLOW, CYAN, MAGENTA, WHITE, ORANGE, PINK。返回格式: 'Set colour of NAME to COLOUR.'。用于标记/高亮特定元素。",
                   P("name:string:元素完整路径。必填。"),
                   P("colour:string:颜色名(RED/GREEN/BLUE/YELLOW/CYAN/MAGENTA/WHITE/ORANGE/PINK)。必填。"),
                   R("name","colour")),

                // ══ 碰撞/检查 ═════════════════════════════
                T("e3d_clash_check", "执行 E3D 碰撞检查并返回结构化报告(碰撞位置/元素对/间距)。type_or_name1 可以是元素类型(如 PIPE 检查所有管道碰撞)或单个元素路径。name2 可选(两元素间检查)。返回格式: '=== CLASH REPORT ===\\nTarget: PIPE\\nClash details: Clash 1: /PIPE-A vs /PIPE-B\\n...\\n=== END REPORT ==='。尝试程序化提取碰撞对和位置, 如果 PML 不支持则提示用 Q CLASH 查看 E3D 控制台。",
                   P("type_or_name1:string:元素类型(如 PIPE)或元素路径。必填。"),
                   P("name2:string:第二个元素路径。可选, 留空检查所有。"),
                   R("type_or_name1")),

                T("e3d_design_check", "对元素运行 E3D 设计规则检查(CHECK)。返回: 'Design check executed.'。检查结果输出到 E3D 控制台。",
                   P("name:string:元素完整路径。必填。"), R("name")),

                // ══ 管道操作 ═══════════════════════════════
                // P2-C 存疑名单（2026-07-02）：CUT/GAP/JOIN/ROUTE 走裸命令形态（"CUT {name} AT {at}"），
                // 未经 Choose 范式同等的真机验证，疑似伪 PML 残留 —— 命令可能被 E3D 拒绝。
                // 已如实标注；真机日志闭环验证通过前，建模主链请优先用 Choose 系列插入工具。
                T("e3d_pipe_cut", "⚠未经真机验证(裸命令形态,可能被 E3D 拒绝并返回带内错误)。意图: 在管道 at(mm) 处切割成两段。失败时请改用 New ... Choose 建模范式或由人在 E3D 内操作。返回: 'OK: cut ...' 或 'Error: ... 在 E3D 端被拒: <真实错误>'。",
                   P("name:string:管道完整路径。必填。"),
                   P("at:number:切割位置(mm)。必填。"),
                   R("name", "at")),

                T("e3d_pipe_gap", "⚠未经真机验证(裸命令形态,可能被 E3D 拒绝并返回带内错误)。意图: 在管道 at(mm) 处切出间隙,gap 默认10mm。返回: 'OK: gap ...' 或 'Error: ... 在 E3D 端被拒: <真实错误>'。",
                   P("name:string:管道完整路径。必填。"),
                   P("at:number:切割位置(mm)。必填。"),
                   P("gap:number:间隙距离(mm),默认10。可选。"),
                   R("name", "at")),

                T("e3d_pipe_join", "⚠未经真机验证(裸命令形态,可能被 E3D 拒绝并返回带内错误)。意图: 合并两根共线管道。返回: 'OK: joined ...' 或 'Error: ... 在 E3D 端被拒: <真实错误>'。",
                   P("name1:string:第一根管道路径。必填。"),
                   P("name2:string:第二根管道路径。必填。"),
                   R("name1", "name2")),

                // P2①（2026-07-02 说真话）：五个管件插入工具的实现是【CE→目标 + New <TYPE> Choose 从管等级选型】
                // 在分支当前位置建件 —— `at` 参数在实现里从未生效。旧描述"在 at(mm) 位置插入"会让 Agent 确信
                // "阀门已在 3500mm 处"而模型里不是（无法自愈的认知污染）。现 at 降为可选并如实标注；精确定位
                // 请建件后用 e3d_pos_dist / e3d_pos_thr / e3d_connect 微调。
                T("e3d_pipe_bend", "在目标分支/元件后按管等级选型插入弯头(New ELBO Choose)，建在分支当前位置(CE 流转)，⚠不按 at 定位。可选 stype 指定选型。精确定位请随后用 e3d_pos_thr(THRO PH/PT/PREV)/e3d_connect 微调。返回格式: 'OK: New ELBO Choose ...（CE=目标）'。",
                   P("name:string:目标分支/元件完整路径(CE 导航目标)。必填。"),
                   P("at:number:【当前版本不生效】保留参数,插入位置不受它控制。可选。"),
                   P("stype:string:弯头选型 Stype(如 90ELB,45ELB)。可选,缺省 Choose Default。"),
                   R("name")),

                T("e3d_pipe_tee", "在目标分支/元件后按管等级选型插入三通(New TEE Choose)，建在分支当前位置(CE 流转)，⚠不按 at 定位。精确定位请随后用 e3d_pos_thr(THRO PH/PT/PREV)/e3d_connect 微调。返回格式: 'OK: New TEE Choose ...（CE=目标）'。",
                   P("name:string:目标分支/元件完整路径(CE 导航目标)。必填。"),
                   P("at:number:【当前版本不生效】保留参数,插入位置不受它控制。可选。"),
                   P("stype:string:三通选型 Stype。可选,缺省 Choose Default。"),
                   R("name")),

                T("e3d_pipe_valve", "在目标分支/元件后按管等级选型插入阀门(New VALV Choose)，建在分支当前位置(CE 流转)，⚠不按 at 定位。精确定位请随后用 e3d_pos_thr(THRO PH/PT/PREV)/e3d_connect 微调。返回格式: 'OK: New VALV Choose ...（CE=目标）'。",
                   P("name:string:目标分支/元件完整路径(CE 导航目标)。必填。"),
                   P("at:number:【当前版本不生效】保留参数,插入位置不受它控制。可选。"),
                   P("stype:string:阀门选型 Stype(GATE/GLOB/BALL/CHECK等)。可选,缺省 Choose Default。"),
                   R("name")),

                T("e3d_pipe_flange", "在目标分支/元件后按管等级选型插入法兰(New FLAN Choose)，建在分支当前位置(CE 流转)，⚠不按 at 定位。精确定位请随后用 e3d_pos_thr(THRO PH/PT/PREV)/e3d_connect 微调。返回格式: 'OK: New FLAN Choose ...（CE=目标）'。",
                   P("name:string:目标分支/元件完整路径(CE 导航目标)。必填。"),
                   P("at:number:【当前版本不生效】保留参数,插入位置不受它控制。可选。"),
                   P("stype:string:法兰选型 Stype(WNRF/SORF/BLIND等)。可选,缺省 Choose Default。"),
                   R("name")),

                T("e3d_pipe_reducer", "在目标分支/元件后按管等级选型插入大小头(New REDU Choose)，建在分支当前位置(CE 流转)，⚠不按 at 定位。精确定位请随后用 e3d_pos_thr(THRO PH/PT/PREV)/e3d_connect 微调。返回格式: 'OK: New REDU Choose ...（CE=目标）'。",
                   P("name:string:目标分支/元件完整路径(CE 导航目标)。必填。"),
                   P("at:number:【当前版本不生效】保留参数,插入位置不受它控制。可选。"),
                   R("name")),

                T("e3d_pipe_route", "⚠未经真机验证(裸命令 'ROUTE {pipe}' 形态,可能被 E3D 拒绝并返回带内错误)。意图: 让 E3D 自动计算管道走向。智能布管请优先用应用侧 routePlan(Genesis)。返回: 'OK: auto-routed ...' 或 'Error: ... 在 E3D 端被拒: <真实错误>'。",
                   P("pipe:string:管道完整路径。必填。"),
                   R("pipe")),

                // ══ 元素创建补充 ═══════════════════════════
                T("e3d_support_create", "创建管道支架/管架(SUPPORT)元素。owner 指定所属管道/BRAN 路径。返回格式: 'OK: created support NAME'。",
                   P("name:string:支架名称。必填。"),
                   P("owner:string:所属父级路径。可选。"),
                   P("stype:string:支架类型(HANGER/SHOE/GUIDE/ANCHOR)。可选。"),
                   R("name")),

                T("e3d_weld_create", "创建焊缝(WELD)元素。返回格式: 'OK: created weld NAME'。用于标记现场焊和工厂焊。",
                   P("name:string:焊缝名称。必填。"),
                   P("owner:string:所属父级路径。可选。"),
                   R("name")),

                T("e3d_label_create", "创建标签(LABEL)元素，显示文本标注。返回格式: 'OK: created label NAME'。",
                   P("name:string:标签名称。必填。"),
                   P("owner:string:所属父级路径。必填。"),
                   P("text:string:标签显示文本。必填。"),
                   R("name", "owner", "text")),

                // ══ 属性补充 ═══════════════════════════════
                T("e3d_attr_clear", "清除元素的属性值(CLEARA)。返回格式: 'OK: cleared ATTR on NAME'。用于重置错误设置的属性。",
                   P("name:string:元素完整路径。必填。"),
                   P("attr:string:要清除的属性名。必填。"),
                   R("name", "attr")),

                T("e3d_attr_copy", "将属性值从源元素复制到目标元素(COPYA)。返回格式: 'OK: copied attributes from SRC to DST'。attrs 用逗号分隔属性名列表。",
                   P("source:string:源元素路径。必填。"),
                   P("target:string:目标元素路径。必填。"),
                   P("attrs:string:属性名列表,逗号分隔,如 'ODIA,WALL,MTYP'。必填。"),
                   R("source", "target", "attrs")),

                // ══ 可见性 ═══════════════════════════════════
                T("e3d_show", "在 E3D 3D 视图中显示元素。返回格式: 'OK: showing NAME'。",
                   P("name:string:元素完整路径。必填。"),
                   R("name")),

                T("e3d_hide", "在 E3D 3D 视图中隐藏元素。返回格式: 'OK: hiding NAME'。",
                   P("name:string:元素完整路径。必填。"),
                   R("name")),

                T("e3d_view_iso", "切换 E3D 3D 视图为等轴测视角(ISO)。返回: 'OK: switched to isometric view'。", NoParams()),

                T("e3d_view_plan", "切换 E3D 3D 视图为平面视角(PLAN)。返回: 'OK: switched to plan view'。", NoParams()),

                T("e3d_view_elevation", "切换 E3D 3D 视图为立面视角(ELEVATION)。返回: 'OK: switched to elevation view'。", NoParams()),

                // ══ 数据库补充 ═══════════════════════════════
                T("e3d_db_claim", "锁定元素(CLAIM)，在多人协作环境中获取独占编辑权。返回格式: 'OK: claimed NAME'。",
                   P("name:string:要锁定的元素路径。必填。"),
                   R("name")),

                T("e3d_db_release", "释放元素锁定(RELEASE)，允许他人编辑。返回格式: 'OK: released NAME'。",
                   P("name:string:要释放的元素路径。必填。"),
                   R("name")),

                // ══ 出图 ═══════════════════════════════════
                T("e3d_draft_iso", "生成管道等轴测图(ISODRAFT)并输出到指定文件。返回格式: 'OK: ISO draft of NAME -> PATH'。",
                   P("name:string:管道完整路径。必填。"),
                   P("output:string:输出文件路径,如 C:\\ISO\\PIPE-001.pdf。必填。"),
                   R("name", "output")),

                // ══ 定位补充 ═══════════════════════════════
                T("e3d_pos_at", "将元素定位到绝对坐标(AT)。x/y/z 为 East/North/Up 坐标(mm)。返回格式: 'OK: positioned NAME at Ex Ny Uz'。",
                   P("name:string:元素完整路径。必填。"),
                   P("x:number:东向坐标(mm)。必填。"),
                   P("y:number:北向坐标(mm)。必填。"),
                   P("z:number:标高(mm)。必填。"),
                   R("name", "x", "y", "z")),

                T("e3d_pos_dist", "⚠未经真机验证(PML命令语法核查报告 §6)：`{name} DIST {值}` 作为定位命令无任何官方/社区出处，高度疑与量距查询 DIST 混淆，可能被 E3D 拒收。精确定位优先改用 e3d_pos_thr(THRO PH/PT/PREV 穿分支头/尾/上一件点)或 e3d_connect(CONN 族)。失败时带回 E3D 真实错误。",
                   P("name:string:元素完整路径。必填。"),
                   P("dist:number:距离(mm)。必填。"),
                   P("from:string:参考元素路径。可选,默认当前参考点。"),
                   R("name", "dist")),

                T("e3d_pos_dir", "设置元素方向(DIR)。direction 可用: U(上)/D(下)/N(北)/S(南)/E(东)/W(西) 或组合如 U45E。返回格式: 'OK: set NAME direction to DIR'。",
                   P("name:string:元素完整路径。必填。"),
                   P("direction:string:方向标识(U/D/N/S/E/W 或组合)。必填。"),
                   R("name", "direction")),

                T("e3d_pos_ori", "绕指定 P-Point 旋转元素(ORI)。ppoint 为 P0/P1/P2/P3。返回格式: 'OK: oriented NAME around PPOINT'。",
                   P("name:string:元素完整路径。必填。"),
                   P("ppoint:string:P-Point 标识(P0/P1/P2/P3)。必填。"),
                   R("name", "ppoint")),

                T("e3d_pos_thr", "将元素对准参考点(THR)，使元素的 P-Point 通过目标点。返回格式: 'OK: through-point aligned NAME to TARGET'。",
                   P("name:string:元素完整路径。必填。"),
                   P("target:string:目标元素路径。必填。"),
                   R("name", "target")),

                // ══ 元素操作补充 ═══════════════════════════════
                T("e3d_element_flip", "翻转管件的进出口方向(FLIP)。返回格式: 'OK: flipped NAME'。用于纠正管件安装方向。",
                   P("name:string:元素完整路径。必填。"),
                   R("name")),

                T("e3d_force_connect", "强制连接两个元素(FCONN)，忽略对齐约束。危险操作（可产生几何错位的非标准连接）：必须带 confirm='true'。返回格式: 'OK: force-connected NAME to TARGET'。仅特殊情况使用。",
                   P("name:string:源元素路径。必填。"),
                   P("target:string:目标元素路径。必填。"),
                   P("confirm:string:危险操作确认，必须显式传 'true'（先向用户确认）。必填。"),
                   R("name", "target", "confirm")),

                // ══ 会话 ═══════════════════════════════════
                T("e3d_session_status", "获取当前 E3D 会话状态。返回: 'Session status queried.\\n  project: ...\\n  mdb: ...\\n  user: ...\\n  module: ...'。用于确认 E3D 连接正常和工作环境。", NoParams()),

                // ══ Phase 5 — 工程分析 ═════════════════════
                T("e3d_pipe_slope_check", "检查管道实际坡度是否符合规范要求。沿管道组件 (BRAN→component) 顺序读取每段 POS, 计算水平距离与高差, 输出每段坡度% 与超差状态 (OK / FLAT_REVERSED / STEEPER)。液体管道按规范要求 ≥0.5% 朝下。",
                   P("pipe:string:管道完整路径,如 /PIPE-101。必填。"),
                   P("min_slope:number:期望最小坡度 % (正值=朝下)。默认 0.5。可选。"),
                   P("tolerance:number:坡度容差 %。默认 0.1。可选。"),
                   R("pipe")),

                T("e3d_pipe_drain_holes", "基于坡度计算结果, 自动识别管道低点并建议排液孔位置。返回低点组件路径 + 建议规格 (默认 DN20)。仅做建议, 不修改 E3D。",
                   P("pipe:string:管道完整路径。必填。"),
                   R("pipe")),

                T("e3d_support_spacing_plan", "按管径 + 介质规则规划支架建议位置。默认按 ASME B31.3 Table 121.5 水服务间距表选择初始间距, 可通过 spacing(mm) 参数覆盖。沿组件累计长度, 每超过间距标记一个支架位置。",
                   P("pipe:string:管道完整路径。必填。"),
                   P("spacing:number:覆盖默认间距 mm (留空=查表)。可选。"),
                   R("pipe")),

                // ══ Phase 5 — 真批量 ═══════════════════════
                T("e3d_element_batch_create", "一次性创建 N 个元素。items 为 JSON 数组, 每项 { type, name, owner? }。逐项调用 E3D API, 返回每项 OK/FAIL 摘要 + 总体统计。失败时由 Agent 通过 SkillRuntime rollback 撤销。",
                   P("items:array:JSON 数组 [{type,name,owner?},...]。必填。"),
                   R("items")),

                T("e3d_attr_batch_set_multi", "一次性更新 N 个元素 × M 个属性。updates 为 JSON 数组, 每项 { name, attrs: { ATTR: VALUE, ... } }。返回每元素/属性级 OK/FAIL。",
                   P("updates:array:JSON 数组 [{name,attrs:{...}},...]。必填。"),
                   R("updates")),

                // ══ Phase 5 — 事件订阅 ═════════════════════
                T("e3d_subscribe", "订阅 E3D 事件流。events 为逗号分隔的事件类型 (current_element_changed/attr_changed/* 等)。返回 subscription_id, 后续用 e3d_poll_events 拉取。",
                   P("events:string:逗号分隔的事件类型, 'current_element_changed' 等。必填。"),
                   R("events")),

                T("e3d_unsubscribe", "释放订阅。",
                   P("subscription_id:string:e3d_subscribe 返回的 id。必填。"),
                   R("subscription_id")),

                T("e3d_poll_events", "拉取订阅缓冲区中累积的事件 (FIFO, 最多 max 条)。无新事件返回 '0 events'。",
                   P("subscription_id:string:e3d_subscribe 返回的 id。必填。"),
                   P("max:integer:本次最多拉取条数, 默认 50, 上限 500。可选。"),
                   R("subscription_id")),
            };
        }

        // ── helpers ──────────────────────────────────────

        static ToolDefinition T(string name, string desc, params object[] items)
        {
            var props = new Dictionary<string, ToolProperty>();
            List<string> req = null;

            foreach (var item in items)
            {
                if (item is ToolProperty tp)
                    props[tp.Description] = tp; // temp; will fix below
                else if (item is string[] r)
                    req = new List<string>(r);
            }

            // Build clean properties
            var cleanProps = new Dictionary<string, ToolProperty>();
            foreach (var raw in items)
            {
                if (raw is string s && s.Contains(":"))
                {
                    var parts = s.Split(new[] { ':' }, 3);
                    if (parts.Length >= 3)
                    {
                        var propName = parts[0].Trim();
                        var propType = parts[1].Trim();
                        var propDesc = parts[2].Trim();
                        cleanProps[propName] = new ToolProperty { Type = propType, Description = propDesc };
                    }
                }
            }

            return new ToolDefinition
            {
                Name = name,
                Description = desc,
                InputSchema = new ToolInputSchema
                {
                    Type = "object",
                    Properties = cleanProps,
                    Required = req
                }
            };
        }

        static object P(string spec) { return spec; }  // "name:type:description"
        static object NoParams() { return null; }
        static string[] R(params string[] names) { return names; }
    }
}
