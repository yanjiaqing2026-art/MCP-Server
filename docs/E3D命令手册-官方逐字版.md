# E3D 命令与用法权威汇总手册（Design 模块，供 AI Agent 作 ground truth 使用）

> **来源与版本说明**：本手册所有逐字语法（syntax graph，`>---` 铁路图）与用例均直接转录自 help.aveva.com 官方在线手册。除个别页面在 3.1/2.1 目录中确认存在（如 DRMPCM24.09.22 标注为 3.1）外，Design 命令语法在 1.1 / 2.1 / 3.1 三个版本间高度一致，本手册主体转录自 **AVEVA_Everything3D/1.1** 分支（该分支全文公开可及且逐页语法图完整）。**每条命令的出处均标注手册代号 + 页码 URL。** 语法图中 `<expr>/<uval>/<bpos>/<bdir>/<pos>/<axes>/<marke>/<gid>` 为官方子语法（见第 0 章）。命令关键字大写部分为最小可缩写形式（官方约定：`CONnect` 可写作 `CON/CONN/.../CONNECT`）。
>
> **置信度分级**：【官方-已抓取】= 本轮已逐字抓取官方正文；【官方-目录确认】= 官方目录页确认该命令页存在但本轮未抓正文；【社区来源】= 仅社区文档佐证，须真机复核；【未找到，未验证】= 未获任何官方出处。

---

## TL;DR

- **配管建模第一优先级命令集已从官方 DRMPGC（Model Reference Manual: Pipework General Commands）逐字抓取**：HPOS/TPOS/HDIR/TDIR/HBORE/TBORE/HCONN/TCONN、HREF/TREF、CONNECT(含 CONN PH TO / CONN PT TO LAST MEM / FCONN)、DISTANCE/THROUGH/CLEARANCE 定位、DIRECTION/ORIENTATE 定向、CHOOSE/SELECT 选型、ROTATE、BY/MOVE 平移，全部附官方铁路图语法。
- **COLLECT 遍历族出自 DBRM（Database Management Reference Manual）第 11 章，VAR/COLLECT/EVALUATE/WITHIN/FROM TABLE 语法完整**；PML 变量作用域（`!`局部/`!!`全局，≤64 Unicode 字符）出自 SOFTCG。
- **关键裁决**：官方 Pipe **Split（切分）无命令行语法** — 只有 GUI 的 *Split Pipe* 窗口（PDUV 手册），故 AI Agent 拟命令行"切管"须走"插入组件/新建 BRAN"而非臆造 `CUT/SPLIT` 命令；`GAP/JOIN/ROUTE`（作为拆分/合并动词）在 DRMPGC 中同样无逐字命令语法记载 — 列入黑名单待真机验证。

> ⚠️ **流程声明（须知）**：本任务因达到最大交互轮次限制而提前收束。原计划的 `run_blocking_subagent`（定向补齐 QRM Q 查询族逐条返回值 与 SOFTCG 的 VAR 命令逐字铁路图）与 `enrich_draft`（对含"社区来源/未找到"标注段落做定向核验）**尚未执行**。因此第 9、11、12、13、14、15 章中标注为【社区来源】或【官方-目录确认】的条目**尚未经二次核验**，AI Agent 使用这些条目前须在真机 Command Window 复核。第 0–8 章及第 10 章（COLLECT）为本轮已逐字抓取的官方内容，可作 ground truth。

---

## Key Findings（关键结论）

1. **官方语法图记号约定**（QRM4.4.01 / DRMPGC13.03.01 逐字确认）：`+` = 选项分叉（任选其一或回车取默认）；`*` = 循环回退（选项可重复）；`<小写尖括号>` = 子语法图；大写字母 = 最小缩写。回车隐含，仅在有意义处标 `nl`。
2. **头尾属性可显式逐条设置**：`HPOS/TPOS <bpos>`、`HDIR/TDIR <bdir>`、`HBORE/TBORE <uval>`、`HCONN/TCONN word`。自由端 HCONN/TCONN 必须设为 `OPEN/CLOS/VENT/DRAN/NULL` 之一，否则数据一致性报错（DRMPCM24.09.06 官方注释）。
3. **CONNECT 在 Branch 与 Component 两个层级语义不同**：对 Branch/Hanger 用 `CONN PH/PT TO <目标>`（设 HBOR/HCON/HPOS/HDIR 并互设 CREF/HREF）；对 Component 用 `CONN [TO NEXT] [AND <marke> IS <bdir>]`（P 点贴面，bore/conn 不兼容时自动 FLIP）。`FCONN` = 强制连接，忽略兼容性。
4. **方向表达式 `<dir>` 官方定义**：`N/S/E/W/U/D` 及 `Y=North, X=East, Z=Up`；复合方向如 `E45N`、`N45E33D`、`W-33D`（负号=Up 方向），源自 DRMPGC13.03.12 语法图。`<bdir>` 额外支持 `FROM <bpos> TOWARDS <bpos>`、`TOWARDS <bpos>` 与设计点方向。
5. **Pipe Split 无命令行 API**（PDUV/2.1 PDUV7.4.4 官方"Pipe Splitting"章确认为窗口式操作，通过"插入 pipe assemblies"实现），这是对上一轮"CUT/GAP/JOIN/ROUTE 疑似臆造"结论的官方侧证。`ROUTE` 作为**自动布管**动词在 DRMPCM25.10 确有其功能（"using the route command"），但 autoroute 语法受限、不允许创建元素；其逐字命令铁路图本轮未抓到，标【官方-目录确认，语法未验证】。

---

## Details（分章逐条汇总）

### 第 0 章 标准子语法图（所有命令的构件，DRMPGC 第 13 章）

**`<pos>` / `<axes>` 轴向位置**（DRMPGC13.03.10，官方-已抓取）
- `<pos>` = `<nsy> <udz> <ewx>` 任意顺序组合；`<nsy>`=`North/Y/South <uval>`，`<udz>`=`Up/Z/Down <uval>`，`<ewx>`=`East/X/West <uval>`。
- `<axes>` = `WRT <gid>` 或 `IN <gid>`。
- 用例：`E1000`、`Z10`、`E30 D10 S20`、`E0 IN SITE`。

**`<bpos>` 3D 位置**（DRMPGC13.03.11，官方-已抓取）
- 语法：`<pos> [<axes>]` | `<marke>`（设计点）| `<gid>`（元素身份，取其原点）| `@`（光标）。
- 用例：`E300 N1000 U2500`、`PIN6`、`/VESSEL10`、`@`。注：`@` 光标定位仅在正交视图有效。

**`<dir>` 轴向方向**（DRMPGC13.03.12，官方-已抓取）
- 由 `<nsy>/<ewx>/<udz>` + `<exp_val>` 复合。用例：`E`、`E45N`、`W-33D`(=West 33 Up)、`Y`(=North)、`N45E33D`。

**`<bdir>` 3D 方向**（DRMPGC13.03.13，官方-已抓取）
- `<dir> [<axes>]` | `<marke>`（设计点方向）| `FROM <bpos> TOWARDS <bpos>` | `TOWARDS <bpos>`。
- 用例：`N45E`、`PL`、`TOW E0 WRT SITE`、`FROM PIN6 TO PIN7`。

**`<uval>`** = 物理量（DRMPGC13.03.07，目录确认）；**`<gid>`** = 设计元素身份（DRMPGC13.03.08）；**`<marke>`** = D 设计点 P 点（DRMPGC13.03.09）；**`<expr>`** = 通用表达式（DRMPGC13.03.05）。

---

### 第 1 章 元素创建（NEW / COPY / CHOOSE·SELECT 内联）

**NEW `<type>` [/名字] [COPY PREV] [BY <pos>] [<选型子句>]**
- 【官方-已抓取（内联用例）+ 社区补全】官方 DRMPGC24.09.13 给出内联形态：`NEW ELBO SEL WITH STYP LR`、`NEW TEE SEL WI PBOR 3 150`、`NEW FLAN SEL WI STYP WN`、`NEW REDU SEL WI STYP ECC LBOR 100`；DRMPGC24.09.12 给出 `NEW REDU CHOOSE WITH ABOR 100 LBOR 80`、`NEW ELBO CHOOSE WITH STYP LR`。
- `NEW ... COPY PREV [BY <方向> <距离>]`：社区高频用例 `NEW BRA COPY PREV BY E/W/S/N/U/D 100`、`NEW CYLI COPY PREV BY N 1000`【社区来源，NEW 独立铁路图本轮未从官方抓到，标待验证】。
- 说明：`NEW` 在当前 CE 的成员表下创建指定类型元素并令其成为新 CE；可选 `/名字` 命名，`COPY PREV` 复制上一同类元素属性。**NEW 的完整独立语法图（含全部子句）本轮未定位到官方页面** → 见附录②。

**CHOOSE**（DRMPGC24.09.12，官方-已抓取）——从规格显示列表选型（仅 DEV GRAPHICS 模式）
```
>- CHOOse -+- AUTOConnect -+- ON / OFF
           |- FORCEConnect -+- ON / OFF
           |- SPec <gid>
           +- DEFault
           +- (RTEX|STEX|TTEX|XTEX|YTEX|ZTEX|TEXT|ALL)
           +- WITH *( <wivl> | <wiwor> )
  <wivl>  = PBOre integer | ANgle | RAdius | ABOre | LBOre | PREssure | TEMperature | RATing  <uval>
  <wiwor> = STYpe|TYpe|ACOnn|LCOnn word | PCOnn integer word | word value | word word
```
- 关键子命令：`CHOOSE DEFAULT`（选规格默认项）、`CHOOSE ALL`（合并 CHOOSE+TEXT）、`CHOOSE SPEC /RF150`（从指定规格选）、`CHOOSE FORCECONNECT OFF`、`CHOOSE AUTOCONNECT OFF`。默认态为 `CHOOSE FORCECONNECT ON`。规格层级须：第一级含 TYPE 问题、第二级含 PBOR 或 BORE，否则报错。

**SELECT**（DRMPGC24.09.14 / 24.09.15，官方-已抓取）——从 Branch 规格选默认组件及其 Leave Tube
```
>- SElect WIth *( SPec <gid> | <wivl> | <wiwor> )
```
- `SELECT` 单独 = 取默认；`SEL WI STYPE BALL`、`SEL WI STYPE ECC PBOR 2 50`、`SEL WI ANGLE 45`、`SEL WI LBOR 50`。SELECT 后自动更新 ANGLE/RADIUS/SHOP（及指定的 HEIGHT）。默认信息来源：SPEC←Branch.PSPE，ARRIVE BORE←上一元素 Leave bore，TEMP/PRES←Branch。

---

### 第 2 章 分支头尾设置（HPOS/TPOS/HDIR/TDIR/HBORE/TBORE/HCONN/TCONN/HREF/TREF）

**自由空间头尾属性**（DRMPGC24.09.06，官方-已抓取）
```
>-+- HPos / TPos -+- <bpos>
>-+- HDir / TDir -+- <bdir>
>-+- HBOre / TBore -+- <uval>
>-+- HCOnn / TCOnn -+- word
```
- 用例：`HPOS E10 N5 U5`（owner 坐标）、`HDIR N WRT WORLD`、`HBOR 80`、`HCON OPEN`。
- **官方注释**：自由端 HCONN/TCONN 必须为 `OPEN / CLOS / VENT / DRAN / NULL` 之一，否则数据一致性报错。

**HREF / TREF 头尾连接引用属性**（DRMPGC24.09.05，官方-已抓取）
```
>-+- HRef / TRef -+- <gid> -+- HEAD / TAIL / (空)
                  +- NULREF
```
- 用例：`TREF /PIPE2 HEAD`（设本元素 TREF 指向 /PIPE2 头，并回设 /PIPE2 的 HREF 指回本元素）；`HREF NULREF`（断开头连接）。通常由 CONNECT 自动设置。

**移动头尾**（DRMPGC24.09.09，官方-已抓取）
```
>- MOVe -+- PHead|HHead|PTail|HTail -+- BY <pos> [WRT|IN <gid>]
                                     +- DISTance <uval>
```
- 用例：`MOVE PT DIST -2000`（沿 PT 反向移 2000）、`MOVE PT BY E2000 S500`。

**按 BOP/TOP 定位头尾**（DRMPGC24.09.08，官方-已抓取）
```
>-+- BOP / TOP -+- <uval> -+- FROm|TO <bpos>
                          |- INFront|BEHind|ONTop|UNDer -+- <gid>|<marke>|<bpos>
```
- 用例：`BOP ONTO /BEAM`（管底压梁顶，间隙 0）、`TOP UNDER U3000`。

**查询头尾**：`Q PHead / HHead / PTail / HTail`；`Q HPosition/TPosition [WRT|IN <gid>]`（DRMPGC24.09.04/06）。

---

### 第 3 章 元件定位（DISTANCE / THROUGH / POSITION / CONNECT）

**相对上一元件定位**（DRMPGC24.09.44，官方-已抓取）
```
>-+- POSition <marke> -+- DISTance <uval>
```
- 用例：`DIST 1000`（沿约束中心线距上一元件原点 1000）、`POS PA DIST 1000`（用本元件 Arrive 点）。

**穿越交点定位**（DRMPGC24.09.45，官方-已抓取）
```
>-+- POSition <marke> -+- THRough <bpos>
```
- 用例：`POS THR /TANK5`、`POS PA THR E3000`、`THR @`。参考平面垂直于约束中心线。

**Branch 头尾连接 CONNECT**（DRMPGC24.09.04，官方-已抓取）
```
>-- CONnect <marke> TO -+- <marke>
                        +- <gid>
```
- 用例：`CONN PH TO /1205-N5`（接 Nozzle）、`CONN PT TO LAST MEM`（接最后组件 Leave 点，忽略 Attachment）、`CONN PT TO /100-A8/T2`（接 TEE 自由 P 点）、`CONN PT TO P4 OF /VF205`、`CONN PH TO PT OF /100-A8/1`（接另一 Branch 尾）、`CONN PH TO ID NOZZ@`（光标选）。
- **前提**：给 CONNECT 前必须先设 Branch 规格（PSPE）；连接只读库元素会自动生成 Inter-DB 连接宏。

**元件连接 CONNECT**（DRMPGC24.09.38，官方-已抓取）
```
>- CONnect -+- <marke> -+- TO <marke> -+- AND <bdir> IS <bdir>
```
- 用例：`CONNECT`（本元件 Arrive 接上一元件 Leave）、`CONNECT TO NEXT`、`CONNECT AND P3 IS U`。bore/conn 不兼容时自动 FLIP 重试；相邻若为 ATTA 点则跳过。

**强制连接 FCONN**（DRMPGC24.09.39，官方-已抓取）
```
>- FCONnect -+- <marke> -+- TO <marke> -+- AND <bdir> IS <bdir>
```
- 用例：`FCONN`、`FCONN TO TAIL`、`FCONN AND P3 IS U`。忽略 bore/conn 兼容性（数据检查仍会报不兼容）。

---

### 第 4 章 方向与定向（DIRECTION / ORIENTATE）

**DIRECTION 变向组件**（DRMPGC24.09.37，官方-已抓取）
```
>-- DIRection -+- AND <marke> IS -+- <bdir>
```
- 用例：`DIR E`（把 Leave 点指向 East，变角组件自动调 ANGLE）、`DIR AND P3 IS U45E`。与 ORI 区别：DIR 会自动调整变角组件的 ANGLE（受 Catalogue 控制）。社区常见 `DIR TOW NEXT`、`DIR N 45 E`【社区来源；`TOW NEXT` 未在此官方页出现，待复核】。

**ORIENTATE 组件定向**（DRMPGC24.09.36，官方-已抓取）
```
>- ORIentate -+- <bdir> IS <bdir>
              +- AND <bdir> IS <bdir>
```
- 用例：`ORI`（Arrive 反向对准上一元件 Leave）、`ORI AND P3 IS U`（非同心件的离线 P 点朝上）。ORI **不会**为迁就斜向离线方向而改变变角组件的 ANGLE/RADIUS。

**查询方向**：`Q <marke> DIRection [WRT|IN <gid>]`（如 `Q PL`、`Q P3`）。

> **FLIP / BANG（绕轴转角）**：本轮未定位到官方逐字铁路图页面。FLIP 在 CONNECT/data-check 语境中被官方多次提及为"自动反转 Arrive/Leave"的行为动词（DRMPGC24.09.38），但独立 `FLIP` 命令语法【官方-目录确认，语法未验证】。`BANG` 未获官方出处 → 附录②。

---

### 第 5 章 建模方向模式（FORWARDS / BACKWARDS）

- 【官方-目录确认 + 社区/训练手册佐证】DRMPGC24.09.40（官方-已抓取）明确："All the examples assume Forwards routing mode ... if Backwards is being used, then the effect of each command will be logically reversed."（Backwards 下各命令效果逻辑反转）。
- 命令行形态：`FORWARDS`（缩写 `FORW`）/ `BACKWARDS`（缩写 `BACK`）【社区来源：TM-1810 训练手册与社区命令表；官方独立命令铁路图本轮未抓到 → 待真机复核】。
- 官方 TM-1810 描述：Forwards = 顺流向，修改轴在组件 Arrive；Backwards = 逆流向，修改轴在 Leave，Next↔Previous 互换。

---

### 第 6 章 管等级与选型（PSPEC / ISPEC / SPREF / STYPE / PURPOSE）

**PSPEC / HSPE 分支规格**（DRMPGC24.09.03，官方-已抓取）
```
>-- PSPEcification name
```
- 用例：`PSPEC /A35B8`（Pipe 级，级联给所有后建 Branch）、`PSPEC /A15A2`（Branch 级）。PSPE 控制后续所有组件选型。规格名必须当前可用。
- **查询**：`Q SPECification <qspci>`，其中 `<qspci>` = `PBOre integer | ANgle | RAdius | ABOre | LBOre | PREssure | TEMperature | RATing | STYpe | TYpe | PCOnn integer | ACOnn | LCOnn | word`（DRMPGC24.09.15，官方-已抓取）。`Q SPRef`、`Q TUbe` 查规格引用与 Leave Tube。
- **ISPEC**（绝缘规格）/ **TSPEC**（伴热规格）/ **SPREF**（组件规格引用）：官方在 DRMPCM24.09.28（绝缘规格属性）、24.09.29（伴热规格属性）有属性说明页【官方-目录确认】；社区用例 `Q ISPEC`、`ISPEC NULREF`（移除绝缘）、`Q SPREF`、`Q PSPEC`【社区来源，待复核逐字语法】。
- STYPE/PURPOSE 作为选型过滤：见第 1 章 CHOOSE/SELECT 的 `WITH STYP ...`（官方-已抓取）；PURP（Purpose）属性主要用于 routing rules（PDUV14.5.2 官方："Rules are identified by their Purpose (PURP) attribute"）。

---

### 第 7 章 元素编辑（DELETE / RENAME / COPY / ROTATE / MOVE·BY / POS AT / INCLUDE·EXCLUDE / MIRROR）

**ROTATE 重定向**（DRMPGC23.08.13，官方-已抓取，含完整四组铁路图）
```
关键字：ROTATE BY ABOUT THROUGH AND
绕给定轴：  >- ROTate ABOut <bdir> [THRough <bpos>] BY -+- <uval>
                                                       +- <bdir> TOwards <bdir>
                        或 ... AND <bdir> IS <bdir>
过给定点 / 指定量 / 指定朝向 三种变体见官方页
```
- 用例：`ROTATE BY -45`（绕 Z 轴，负角=逆时针）、`ROTATE BY 45 ABOUT E`、`ROTATE ABOUT E BY 45`、`ROT THRO P3 ABOUT S BY -25`、`ROTATE AND Y IS N45W25D`。社区常见 `ROT BY 90 ABOUT Z THRO ID@`【与官方语法一致】。

**POSITION AT 坐标定位**（DRMPGC23.08.08，官方-已抓取）
```
>-+- POSition <marke> -+- AT <bpos>
```
- 用例：`AT E3' N4'6 U1'`、`AT IDP@`、`AT@`、`POS PIN5 AT E3000`。查询：`Q POS [<bpos>] [WRT|IN <gid>]`、`Q POS IN SITE`、`Q POS IDP@`。

**BY 沿轴平移**（DRMPGC23.08.16，官方-已抓取）
```
>-- BY <pos> [<axes>]
```
- 用例：`BY E300 N400`、`BY E3000 WRT SITE`。社区高频 `BY U 500`/`BY D 500`/`BY E 500`/`BY N 500` 等【与官方 `<pos>` 语法一致】。

- **DELETE**：社区用例 `DELETE ZONE MEMBERS`、`DELETE VERT`、`DELETE $!ref`【社区来源；官方独立 DELETE 铁路图本轮未抓 → 附录②】。
- **RENAME**：社区用例 `REN /NAME COPY PREV`（复制并改名）、`NAME /NewName`【社区来源，待复核】。
- **COPY**：见 NEW ... COPY PREV（第 1 章）。
- **INCLUDE / EXCLUDE**（元素挪动归属）：社区在建模中用 `IN`/`INCLUDE`、`EXCLUDE` 调整成员关系【社区来源；官方语法未定位 → 附录②】。
- **MIRROR / 平面反射**：官方有 DRMPCM23.08.32 "Reflecting a Position in a Plane (Mirroring)"页【官方-目录确认，语法未抓】。

---

### 第 8 章 隐含管段与间隙（CLEARANCE / TUBE）

**CLEARANCE 相对上一元件留间隙**（DRMPGC24.09.53，官方-已抓取）
```
>-- CLEArance <uval>
```
- 用例：`CLEA 500`（考虑双方全几何，沿约束中心线留 500 间隙）。查询：`Q <marke> POSition|BOP|TOP [WRT|IN <gid>]`。
- 官方另有 24.09.54 组件两侧间隙、24.09.55 竖向间隙、24.09.56 Tube(BOP)间隙、24.09.57 综合间隙【官方-目录确认】。
- **隐含管段（Leave Tube / implied TUBI）**：由 SELECT/CHOOSE 自动选取（第 1、6 章）；其长度查询见第 9 章 `Q ITLE/ATLE`。可变长 Tube 属性见 DRMPCM24.09.27【官方-目录确认】。

---

### 第 9 章 查询族（Q ...）【混合置信度 — 多数条目待 QRM 逐条核验】

> **重要**：本轮**未能抓取 QRM 的逐条 Q 命令返回值页正文**（仅确认 QRM 目录与语法约定页 QRM4.4.01）。以下 Q 命令的语义多来自官方 DRMPGC 各页的"Querying:"小节（已抓取，可信）与社区命令表（待复核）。**Agent 使用 Q 命令解析返回值前，强烈建议真机核对格式。**

| 命令 | 返回内容 | 出处/置信度 |
|---|---|---|
| `Q POSition [<bpos>] [WRT|IN <gid>]` | CE 原点位置（owner/指定坐标系） | DRMPGC23.08.08【官方-已抓取】|
| `Q PHead / HHead / PTail / HTail` | 头尾位置/方向属性 | DRMPGC24.09.04/06【官方-已抓取】|
| `Q HPosition / TPosition [WRT|IN <gid>]` | 头/尾位置 | DRMPGC24.09.04【官方-已抓取】|
| `Q <marke> DIRection [WRT|IN <gid>]`（如 `Q PL`,`Q P3`） | 指定 P 点方向 | DRMPGC24.09.36/37【官方-已抓取】|
| `Q <marke> POSition [WRT|IN <gid>]`（如 `Q PA POS`） | 指定 P 点位置 | DRMPGC24.09.38/44【官方-已抓取】|
| `Q SPECification <qspci>` | 规格问题项当前取值 | DRMPGC24.09.15【官方-已抓取】|
| `Q SPRef` / `Q TUbe` | 规格引用 / Leave Tube | DRMPGC24.09.15【官方-已抓取】|
| `Q HREF / TREF` | 头/尾连接引用 | DRMPGC24.09.05【官方-已抓取】|
| `Q CE / HEAd / BRANch / TAIl` | 当前元素/头/分支/尾 | DRMPGC24.09.05【官方-已抓取】|
| `Q <marke> BOP / TOP [WRT|IN <gid>]` | P 点管底/管顶标高 | DRMPGC24.09.53【官方-已抓取】|
| `Q ATT` / `Q ATTRIBUTES` | 列出 CE 全部属性 | 【社区来源】|
| `Q NAME / TYPE / OWN / MEM / REF` | 名/类型/属主/成员/引用号 | 【社区来源，待 QRM 复核】|
| `Q ORI [WRT ...]` | CE 朝向 | 【社区来源】|
| `Q HBORE / TBORE / ABORE / LBORE / BORE` | 头/尾/Arrive/Leave/公称孔径 | 【社区来源，待复核】|
| `Q P1/P2/P3 ...` | P 点 bore/方向/连接类型/位置 | 【社区来源】|
| `Q ITLE / ATLE`（隐含管长；负值=重叠/无效） | 隐含 Leave/Arrive Tube 长 | 【未找到官方页，未验证 — 负值含义须真机确认】|
| `Q TULE / LTLE` | 分支管长 | 【社区来源，待复核】|
| `Q SPRE / CATREF / STYP / PSPEC / ISPEC` | 规格/目录/子类型/管等级/绝缘 | 【社区来源】|
| `Q CONNECTIONS / HCON / TCON / ACON / LCON` | 连接类型 | 【社区来源，待复核】|
| `Q ANGLE / RADIUS / HEIGHT` | 变角/半径/高度 | 【社区来源】|
| `Q MTXX / DTXR / RTEX` | 材料/明细描述文本 | 【社区来源】|
| `Q PARA / NVOL / NWEI` | 参数/净体积/净重 | 【社区来源】|

---

### 第 10 章 集合与遍历（VAR ... COLLECT / EVALUATE）【官方-已抓取，DBRM 第 11 章】

**COLLECT 语法与选择准则**（DBRM11，官方-已抓取全文）
```
VAR !Array COLLECT <selection criteria>
选择准则顺序：class → WITH/WHERE(表达式) → WITHIN(体积) → FOR(层级) [EXCLUDE ...]
```
- **class 形态**：`ALL`、`ALL FRMW`、`ALL BRANCH MEMBERS`（所有配管组件）、`ITEMS OF EQUI /VESS1`、`( /PIPE1 /PIPE2 )`。
- **逻辑表达式**：`WITH`/`WHERE`，如 `VAR !LENGTHS COLLECT ALL WITH ( XLEN * YLEN * ZLEN GT 1000 )`。
- **空间盒**：`WITHIN <bpos> TO <bpos>` 或 `WITHIN VOLUME <元素> <间隙>`，如 `VAR !P COLLECT ALL PIPE EXCLUSIVE WITHIN VOLUME /PUMP1 1500`（EXCLUSIVE=仅完全在盒内）。
- **层级限定**：`FOR /PIPE1 /PIPE2 EXCLUDE BRAN 1 OF /PIPE2`。
- **追加/覆盖**：`VAR !BENDS APPEND COLLECT ALL ELBOWS`；`VAR !BENDS[99] COLLECT ALL ELBOWS`（从索引 99 起覆盖）。
- **索引加速**：`FROM TABLE NAME PREFIX '/B'`、`FROM TABLE SPRE VALUE /RF300/100G`、`FROM TABLE :MYATT VALUE 100 TO 110`（UDA）、`FOR DB CTBATEST/DESI`。索引属性含 CATR/SPRE/LSTU/ISPE/DOCREF 等（官方给全表）。
- **性能建议**（官方原文）：表达式最慢，务必先用 class/FOR/WITHIN 缩小范围，如 `ENHANCE ALL BOX WITH (...) FOR /*`。

**EVALUATE**（DBRM11，官方-已抓取）
```
VAR !variable EVALUATE <expression> FOR <selection>
```
- 用例：`var !cln collect all EQUI` 然后 `var !name evaluate (description) for all from !cln`，或直接 `var !name evaluate (description) for all EQUI`。

**MATCHWILD / 通配**：官方 DBRM 用 `NAME PREFIX`（前缀）实现名称通配；社区常见 `MATCH WITH (NAME,'*ABC*')`、`MATCHWILD`【社区来源，`MATCHWILD` 逐字语法待复核】。

**表达式运算符**（PML1 表达式，SOFTCG5.05.8 官方-已抓取指路 + DBRM）：比较 `GT/LT/EQ/GE/LE/NE`；算术 `+ - * /`；PML1 表达式用于命令参数/规则/报告模板，PML2 表达式（`=` 赋值、`if`/`do`）用于赋值与控制流。

---

### 第 11 章 PML 变量与 VAR 命令【混合置信度】

**VAR 命令**（词捕获形态 vs 表达式赋值形态）
- 【官方指路 + 训练手册佐证】`VAR !x <词序列>` = 捕获形态（结果为字符串），如 `VAR !FRED NAME`（取 CE name）、`VAR !POS POS IN WORLD`、`VAR !list COLL ALL ELBO FOR CE`。**注意（官方 TM-1401）**："PML1 style variables using the VAR syntax are only stored as strings."（VAR 语法变量仅存为字符串）。
- `!x = <PML2 表达式>` = 赋值形态（保留类型 REAL/BOOLEAN/ARRAY），如 `!temp = 23 * 1.8 + 32`。SOFTCG5.05.8（官方-已抓取）明确：PML2 表达式用于 `if`/`do`/`=`；其他场合（命令参数）用 PML1。
- **RAW 关键字 / 变量删除**：【未找到官方逐字页，未验证 → 附录②】。社区：删除变量常见 `VAR !x` 无值或专用清除；须真机确认。

**变量作用域**（SOFTCG3.03.07，官方-已抓取）
- `!` 开头 = 局部（仅本 Function/macro 内有效）；`!!` 开头 = 全局（整个 session 或直到删除）。名称：字母开头，字母+数字组合，**≤64 Unicode 字符**（不含 `!`/`!!`）；不建议含 `.`（PML2 中 `.` 为对象/方法分隔符）。

**`$` 替换族**：`$M/文件` 加载宏（社区高频）、`$Q` 打印/help、`$P`、`$!var`【社区来源；官方逐字定义页本轮未抓 → 待复核】。

---

### 第 12 章 控制流（do / if / handle）【官方-目录确认，语法未逐字抓取】

- `if (...) ... elseif ... else ... endif`、`do ... enddo`（含 `do !x values !arr`、`do !x from 1 to N`）、`handle (n) ... elsehandle ... endhandle`（错误处理）。均为 PML2 结构，SOFTCG/SCRM 有专章【官方-目录确认（SCRM Introduction 已抓取指路），逐条铁路图待补】。社区宏示例：`do !sVal values !s ... handle any elsehandle NONE ... endhandle enddo`【社区来源】。

---

### 第 13 章 数据库操作（SAVEWORK / GETWORK / CLAIM / UNCLAIM / RELEASE / EXTRACT / UNDO·REDO / LOCK）【官方-目录确认 + 社区，语法待复核】

- DBRM 目录（官方-已抓取）确认存在章节：**SAVEWORK/GETWORK 多写环境冲突、Extracts（Create/Claim/Release/Merge/Flush）、Global Working、Undo Capabilities、Backtrack/Rewind**。
- 命令行形态（【社区来源，待真机复核】）：`SAVEWORK`（存盘）、`GETWORK`（取回他人改动）、`CLAIM <元素>` / `UNCLAIM ALL` / `RELEASE`、`Q CLAIMLIST EXTRACTS`、`LOCK ALL` / `UNLOCK`、`SESSION COMMENT '...'`、`UNDO` / `REDO` / `MARK`。
- **建议**：CLAIM/RELEASE/EXTRACT 的精确命令行语法对多用户 DB 写操作至关重要 → 列入附录②优先真机验证。

---

### 第 14 章 CE 导航（/名字 / OWNER / MEMBER / FIRST·LAST·NEXT·PREVIOUS / GOTO / END）【官方-目录确认 + 社区】

- DBRM 目录（官方-已抓取）确认："Database Navigation and Query Syntax / Current Element / Current Position / Specify the Standard Name / Specify the Constructed Name / Specify the World / Specify the Refno / Use the BACKREF attribute"。
- 命令行形态（【社区来源，待复核逐字语法】）：`/名字`（跳转到命名元素并设为 CE）、`OWNER`（上移一层）、`MEMBER n` / `MEM`、`FIRST` / `LAST` / `NEXT` / `PREVIOUS`（含带类型形态如 `FIRST FLAN`、`NEXT AXES AT CE`）、`GOTO`、`END` / `RETURN`（退出层级）、`CE <元素>`。
- 官方概念（TM-1801，已抓取）：CE=Current Element，Owner=属主，Member=成员，World=顶层；"Navigating to"=选 CE。

---

### 第 15 章 显示与视图（ADD / REMOVE / ENHANCE / AID / UPDATE / AXES / MARK）【官方-目录确认 + 社区】

- DRMPGC 第 15 章"Display"（官方-已抓取目录）确认存在：Adding Elements to the Display、Removing Elements、Defining Colours、Element Representation、Setting Tube Representation、Using Design Aids、Highlighting Components、Refreshing the Graphical View、Specifying Axes、Graphical Labelling 等页【官方-目录确认，多数正文本轮未抓】。
- 命令行形态（【社区来源，与官方章节吻合，逐字语法待复核】）：`ADD CE`、`ADD ALL WITHIN VOL CE 1000 ALL`、`REM ALL` / `REMOVE CE`、`REM ALL ADD CE`、`ENHANCE CE COLOUR RED` / `ENHANCE CE TRANSLUCENCY 99` / `UNENHANCE ALL`、`REPR HOLES ON/OFF UPDATE` / `REPR OBST ON UPDATE`、`AXES ON/OFF` / `AXES AT PH|PT|P1|IDP@`、`MARK CE` / `UNMARK ALL` / `MARK WITH (NAM) ...`、`UPDATE ALL`、`AID*`（AIDLIN/AIDPOI 等 in-canvas）。
- **注意**：`ADD/REMOVE/ENHANCE` 只改显示，不改数据库，对建模正确性无风险；但仍应真机确认逐字缩写。

---

## Recommendations（给 AI Agent 集成的分阶段建议）

**阶段一（可立即作 ground truth，零风险）**：将本手册**第 0–8 章 + 第 10 章**（均为官方 DRMPGC/DBRM 逐字抓取）直接编入 Claude Code 的命令白名单与语法校验层。这些覆盖了配管建模全链路：`NEW`（内联形态）→ `PSPEC` → `HPOS/HDIR/HBORE/HCONN`+`CONN PH TO` → `SELECT/CHOOSE` → `DIST/THRO/CONNECT/DIR/ORI/CLEA` → `CONN PT TO LAST MEM`。生成命令时严格套用官方铁路图的关键字顺序与 `<pos>/<bdir>` 复合方向文法。

**阶段二（须真机验证后再启用）**：第 9 章除"官方-已抓取"行外的 Q 查询返回格式、第 11–15 章标【社区来源/官方-目录确认】的条目。建议在 E3D Command Window 用小样本逐条执行、对照 `Q ATT` 输出核验，通过后升级为白名单。**优先验证顺序**：CLAIM/RELEASE/SAVEWORK/GETWORK（第 13 章，多用户写风险最高）> NEW/DELETE/RENAME 独立语法（第 1/7 章）> Q ITLE/ATLE 负值含义（第 9 章）> FORWARDS/BACKWARDS（第 5 章）。

**阶段三（补齐官方出处）**：用一次定向抓取补 QRM 逐条 Q 页正文、SOFTCG 的 VAR 命令与 do/if/handle 铁路图、DBRM 的 CLAIM/EXTRACT 命令行页、DRMPGC 第 14/15 章 Display 正文——将阶段二条目从【社区来源】升级为【官方-已抓取】。

**触发阈值**：任何命令若在真机执行返回语法错误或产生非预期模型 → 立即从白名单降级并标"未验证"，回到官方手册核对；**绝不允许 Agent 对查不到出处的命令自行拼写**。

---

## Caveats（重要限制与未决项）

1. **本报告未完成既定的 subagent + enrich 校验流程**（因轮次上限提前收束）。第 9、11–15 章的【社区来源】条目**未经二次核验**，存在拼写/缩写偏差风险，须真机复核后方可作 ground truth。
2. **版本**：主体转录自 1.1 分支；已抽样确认 DRMPCM24.09.22 等页在 3.1 目录中同样存在且内容一致，但**未逐页比对 3.1 全量差异**。若 3.1 引入新子句，本手册可能滞后 → 关键命令建议对照 `https://help.aveva.com/AVEVA_E3D_Design/3.1/DRMPGC/` 同名页复核。
3. **NEW 完整独立铁路图未定位到官方页**：本手册的 NEW 语法由官方内联用例 + 社区用例拼合，`COPY PREV BY ...` 组合形态属社区佐证。
4. **FLIP / BANG / RAW / INCLUDE·EXCLUDE / MATCHWILD** 未获官方逐字语法。

### 附录①：官方已裁决"不存在/勿用"命令黑名单
- **管道切分 `CUT` / `SPLIT`（作为命令行动词）**：官方无命令行语法。Pipe Split 仅存在于 GUI *Split Pipe* 窗口（PDUV/2.1 PDUV7.4.4"Pipe Splitting"），底层通过"插入 pipe assemblies / 新建 BRAN/PIPE"实现。**Agent 若需程序化切管，须走"在切点插入组件 + 重设头尾 + 新建分支"，不得发 `CUT/SPLIT` 命令。**
- **`GAP` / `JOIN`（作为切分/合并动词）**：DRMPGC 无逐字命令记载，疑似臆造 → 勿用；间隙用官方 `CLEARANCE`（第 8 章），合并须走 GUI/PML 宏。
- **`ROUTE`**：作为**自动布管**功能官方确有（DRMPCM25.10"using the route command"），但语法受限、不允许创建元素，且其逐字命令铁路图本轮未取得 → **不列黑名单，但列"语法未验证、暂缓程序化调用"**。

### 附录②：官方文档未覆盖 / 仍须真机验证的命令清单
1. `NEW` 完整独立语法图（含全部 CHOOSE/SELECT/COPY PREV/BY 子句组合）
2. `DELETE` / `RENAME`(`NAME`) / `INCLUDE` / `EXCLUDE` 独立铁路图
3. `FLIP` / `BANG`（绕轴转角）独立命令语法
4. `FORWARDS` / `BACKWARDS` 命令行独立语法（行为已官方确认，命令语法待补）
5. `VAR` 命令 RAW 关键字、变量删除语法；`$M/$P/$Q/$!var` 替换族逐字定义
6. `SAVEWORK/GETWORK/CLAIM/UNCLAIM/RELEASE/EXTRACT/LOCK/UNLOCK/UNDO/REDO/MARK/SESSION COMMENT` 命令行逐字语法（DBRM 目录已确认存在，正文待抓）
7. CE 导航 `FIRST/LAST/NEXT/PREVIOUS [<类型>]`、`OWNER/MEMBER/GOTO/END/RETURN` 逐字语法
8. Display 族 `ADD/REMOVE/ENHANCE/AXES/MARK/UPDATE/REPR/AID*` 逐字语法（DRMPGC 第 15 章正文）
9. QRM 各 `Q` 命令逐条返回值定义；尤其 `Q ITLE/ATLE` 负值含义、`Q CONNECTIONS` 系列
10. `MATCHWILD`、`ROUTE`（autoroute）命令行语法

---

## 附录③：help.aveva.com 补抓命令（官方逐字，2026-07-03）

> 本附录为针对附录②「未覆盖 / 待验证」清单的**二次补抓**结果，来源为 help.aveva.com（AVEVA Everything3D 1.1 / 3.1 / E3D Design 2.1 静态全文页）。
> **铁路记号原样保留**：`>-`/`+`/`*`/`<小写>`/大写=最小缩写；**不重排、不翻译语法本体**。
> 重要更正（相对附录②）：`NEW`/`DELETE`/`CLAIM` 等「元素编辑/数据库」命令的权威语法**在 DBRM（Database Management Reference Manual）**，不在 DRMPGC；撤销/重做真名是 `UNDODB`/`REDODB`/`MARKDB`（非 `UNDO`/`REDO`）。

### ③.1 元素创建 NEW 组（DBRM 第 4.5 章 / DRMPCM24.09，官方逐字）

**NEW（通用元素创建）** — DBRM4.05.3
```
NEW  <element_type>  [ <element_name> ]  [ +- BEFore <list_position> -+ ]  [ DB <team>/<database> ]
                                             '- AFTer  <list_position> -'
```
- `<list_position>` 可为：名字/refno（`/ELBO3`）、裸序号（`8`）、类型+序号（`FLAN 2`=第 2 个 Flange）、`FIRST/LAST <type>`、`NEXT n`（相对当前列表位）。
- 不给列表位时，新元素插在**当前列表位之后**。CE 必须处于能合法拥有目标类型的层级。
- 例：`NEW TEE /TEE2`、`NEW TEE AFTER /ELBO3`、`NEW TEE BEFORE 8`、`NEW TEE BEFORE FLAN 2`、`NEW TEE AFTER LAST ELBO`、`NEW TEE AFTER NEXT 3`、`NEW SITE /MYSITE DB MYTEAM/MYDB`。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/DBRM/DBRM4.05.3.html （另 DBRM1.02.28 元素创建）。

**NEW … COPY（先建后克隆）** — DBRM4.05.6
```
Stage 1: NEW <element_type> [<element_name>]      (创建并成为 CE)
Stage 2 (对新 CE 应用以下 COPY 之一):
  COPY <element_id>
  COPY MEMbers OF <element_id>
  COPY MEMbers <integer> TO <integer> OF <element_id>
  COPY ATTributes OF <element_id>
  COPY LIKE <element_id>
  COPY ADJ/ACENT <select>
  <any COPY form> ... REName <old_name> <new_name>
```
- `COPY <id>`：克隆源的**全部属性 + 全部子件**，除 NAME（须唯一）与 LOCK（目标恒解锁）；CE 须无成员。`COPY ATT OF` 除 DESI 引用与 OWNER 外全复制；`COPY LIKE` 再额外跳过位置/方向/朝向/角度。
- 例：`NEW EQUI /EQUIPB` + `COPY /EQUIPA`；`NEW BRAN /SIDEARM` + `COPY MEMBERS 12 TO 20 OF /MAINLINE`；`COPY /FRACT1/PIPE RENAME /FRACT1 /FRACT2`。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/DBRM/DBRM4.05.6.html

**NEW … COPY PREV BY / AT / `<attr>`（建 + 定位一行完成）** — DRMPCM26.11.02
```
NEW <element_type> [<element_name>]  [ + AT <bpos>              + ]
                                     | COPY PREV BY <dir> <dist>   |
                                     '- <positioning_attr> <value> -'
```
- `COPY PREV BY <dir><dist>`：相对上一元素按方向+距离位移（如 `E1000`=East 1000），是**定位式**复制（≠ DBRM4.05.6 的属性克隆）。
- 例：`NEW PNOD /PNOD1 AT E1000 Y500 Z500`、`NEW PNOD /PNOD2 COPY PREV BY E1000`、`NEW PNOD /PNOD3 NPOS E2000 N1000 D500`。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/DRMPGC/DRMPCM26.11.02.html

**NEW `<component>` … CHOOSE（建管件 + 从规格图形选型）** — DRMPCM24.09.12（仅 DEV GRAPHICS 模式）
```
>- CHOOse -+- AUTOConnect --.
           |                |
           |- FORCEConnect -+- ON --.
           |                |       |
           |                '- OFF -+->
           |
           |- SPec <gid> -.
           |              |
           '--------------+- DEFault -.
                          |           |
                          '-----------+- RTEX -.
                                      |- STEX -|
                                      |- TTEX -|
                                      |- XTEX -|
                                      |- YTEX -|
                                      |- ZTEX -|
                                      |- TEXT -|
                                      |- ALL --|          .----<----.
                                      |        |         /          |
                                      '--------+- WITH -*- <wivl> --|
                                               |        '- <wiwor> -+->
                                               '->

where <wivl> is:
 >--+-- PBOre integer --.
    |-- ANgle ----------|
    |-- RAdius ---------|
    |-- ABOre ----------|
    |-- LBOre ----------|
    |-- PREssure -------|
    |-- TEMperature ----|
    '-- RATing ---------+-- <uval> -->

and <wiwor> is:
 >--+-- STYpe --.
    |-- TYpe ---|
    |-- ACOnn --|
    |-- LCOnn --+-- word -->
    |-- PCOnn integer word -->
    '-- word --+-- value --.
               '-- word ---+-->
```
- `CHOOSE` 可与 `NEW` 同行；默认态 `CHOOSE FORCECONNECT ON`。例：`NEW REDU` + `CHOOSE WITH ABOR 100 LBOR 80`、`NEW ELBO CHOOSE WITH STYP LR`、`CHOOSE SPEC /RF150`、`CHOOSE DEFAULT`。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/DRMPGC/DRMPCM24.09.12.html

**NEW `<component>` SELect（建管件 + 从当前规格自动选型）** — DRMPCM24.09.13
```
NEW <component_type>  SELect  [ WIth <spec_question> <answer> ]*
```
- 文本命令版 CHOOSE，选择答案用 `WITH` 键入。例：`NEW ELBO SEL WITH STYP LR`、`NEW TEE SEL WI PBOR 3 150`、`NEW FLAN SEL WI STYP WN`、`NEW REDU SEL WI STYP ECC LBOR 100`。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/DRMPGC/DRMPCM24.09.13.html

**诚实点**：独立的「NEW PIPE / NEW BRAN」专属铁路页在本套手册中不存在——PIPE/BRAN 走通用 `NEW` + GUI 表单；primitive 尺寸创建（`NEW BOX/CYLI`）语法官方指向 Data Model Reference Manual（未展开）。

### ③.2 元素编辑 DELETE / RENAME / INCLUDE / EXCLUDE（DBRM 第 4.5 / 11 章，官方逐字）

**DELETE** — DBRM4.05.4
```
DELETE element_type
DELETE element_type MEMbers
DELETE element_type MEMbers integer
DELETE element_type MEMbers integer TO integer
```
- 删除 CE 及其全部子件（不可逆）。命令词须**输全**、须确认 CE 的通用类型作安全校验；删后 Owner 变为新 CE。例：`DELETE NOZZ`、`DELETE ZONE`、`DELETE BRAN MEMBERS`、`DELETE BRAN MEMBER 6`、`DELETE BRAN MEMBERS 5 TO 7`。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/DBRM/DBRM4.05.4.html

**RENAME** — DBRM4.05.6（无独立命令页，仅作 COPY 限定词）
```
<COPY-command> REName old_name new_name
```
- RENAME **只**作为 COPY 命令的可选尾缀存在，复制时把 `old_name` 子串替换为 `new_name`（解决 NAME 唯一性冲突）。要直接改名须设 NAME 属性。例：`COPY /FRACT1/PIPE RENAME /FRACT1 /FRACT2`。

**INCLUDE** — DBRM4.05.5（层级重组）
```
INCLude element_id
INCLude element_id BEFore list_position
INCLude element_id AFTer list_position
```
- 把一个**已存在**元素并入 CE 的成员表，归属权转移到 CE（如 `/PIPE2` 归属从 `/ZONE2` 转到 `/ZONE1`）。同页兄弟命令 `REOrder element_id [BEFore|AFTer list_position]`（例 `REORDER /ELBO3 BEFORE FIRST ELBO`）。例：`INCLUDE /PIPE2`。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/DBRM/DBRM4.05.5.html

**EXCLUDE** — DBRM 第 11 章（COLLECT 查询限定词，非元素编辑命令）
```
<COLLECT-query> EXCLUDE element_type gid OF element_id
```
- EXCLUDE **不是**独立元素编辑命令，只在 COLLECT 收集查询里把指定元素**排除**出收集集，对 DB/CE **无写入**。例：`VAR !BRANCH COLLECT ALL BRANCH MEMBERS FOR /PIPE1 /PIPE2 EXCLUDE BRAN 1 OF /PIPE2`。

### ③.3 定向 ORIENTATE / FLIP（DRMPGC / DRMPCM，官方逐字）

**ORIENTATE（ORI）** — DRMPCM23.08.12/.13
```
>- ORIentate -+- <bdir> IS <bdir> -+- AND <bdir> IS <bdir> -+-->
```
- 两条 `<bdir> IS <bdir>` 子句，第二条 `AND …` 可选（对称件可只给一条；3D 完全定向须给两条）。右手系 E(X)/N(Y)/U(Z)，默认 owner 坐标系（可 WRT 改参照）。ORI 只转不移。
- 例：`ORI Y IS N AND Z IS UP`、`ORI P1 IS E`、`ORI Y IS PPLIN TOS OF /SCTN1 X DIR AND Z IS U`。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/DRMPGC/DRMPCM23.08.12.html

**FLIP（钢结构 Section 起止互换）** — DRMPCM26.11.29（AVEVA E3D Design 2.1 同页）
```
>--- FLIP --->
```
- 无参数。对 CE（steelwork Section）互换 POSS↔POSE、DRNS↔DRNE、JOIS↔JOIE 三对起/止属性，等效反转朝向。核对用 `Q ATT` / `Q DER POS`。
- 来源：https://help.aveva.com/AVEVA_E3D_Design/2.1/DRMPGC/DRMPCM26.11.29.html
- **低置信/待真机**：`BANG` 仅确认为可查询属性（`Q BANG`=Beta Angle），官方正文**无**独立 BANG 定向写命令 → 不作定向命令采信。

### ③.4 建模方向搭档 DIRECTION / FLIP（DRMPGC 3.1，官方逐字）

**DIRECTION** — DRMPCM24.09.37
```
>-- DIRection --+-- AND <marke> IS --.
                |                     |
                '---------------------+-- <bdir> -->
```
- 沿约束中心线定向 Component，把指定 p-point 指向新方向；与 ORI 不同，**自动调整变角组件的 ANGLE**。例：`DIR E`、`DIR AND P3 IS U45E`。

**FLIP（互换 Arrive/Leave p-point）** — DRMPCM24.09.25
```
>-- FLIP -->

Query:
>-- Query --+-- ARrive --.
            |            |
            '-- LEave ---+-->
```
- 与 FORWARDS/BACKWARDS 模式直接耦合：互换某 Component 的 ARRIVE/LEAVE p-point 号。例：`NEW REDU FLIP SELECT WITH LBORE 100`。
- **低置信/待真机**：`FORWARDS`/`BACKWARDS` 官方**确认为真实工作模式概念**（DRMPCM24.09.25 决定 REDU 是否需 FLIP），但**未找到独立 railroad 语法页**；命令拼写 + `FORW`/`BACK` 缩写为社区来源（community），未升级为官方逐字。

### ③.5 PML 变量 / $ 替换族（SOFTCG，官方逐字）

**$ 特殊字符族** — SOFTCG4.04.06
```
$P <text-to-end-of-line>          输出文本到屏幕
$M/<filename>                     执行文本文件（宏）
$!<PmlVariableName>               把 PML 变量按 .STRING() 展开为字符串（$!! 全局）
$$                                字面量 $（须双写）
<command text> $ <newline>        行尾 $ = 续行
$newline                          续行且不自动闭合
```
- `$Q`（SOFTCG13.13.14）是**语法提示查询**（列出下一个合法命令词/参数类型），**不是**变量替换：`NEW BOX $Q`。
- 例：`$P This text will be output to the screen`、`$M/filename`、`$P Area is $!Area`。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/SOFTCG/SOFTCG4.04.06.html

**VAR 命令族** — SOFTCG7.07.10/.11 / 9.09.09
```
VAR !VarName RAW ...                             抑制自动编辑，赋未经处理的字符串
VAR !Index SORT [ UNIQUE | NOUNSET | NOEMPTY ]* !Array1 [<sortopt>]* [ !Array2 [<sortopt>]* ]* [ LASTINGROUP !Group | FIRSTINGROUP !Group ]
   <sortopt> = CIASCII + DESCENDING + NUMERIC + (NUMERIC DESCENDING) + (CIASCII DESCENDING)
VAR !Totals SUBTOTAL !Values !Index !Group        分组求小计（单位限定词被忽略）
VAR !variable EVALUATE <expression>               求值，返回带单位限定的字符串/数组（min EVAL）
VAR !variable <ATTRIBUTE>                          如 VAR !X XLEN → "46cm"（含当前单位）
```
- 例：`VAR !Index SORT !Car CIASCII !Colour !Year NUMERIC`、`VAR !D EVAL (DIAM) for all CYLI`。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/SOFTCG/SOFTCG7.07.10.html / SOFTCG7.07.11.html / SOFTCG9.09.09.html

**变量删除（PML2）** — SOFTCG3.03.26
```
!VarName.Delete()        或      !!GlobalVar.Delete()
```
- `.Delete()` 使变量 UNDEFINED（1.1 SOFTCG **无** `VAR EXPUNGE` 关键字）。**警告（官方加粗）**：不得删除对象/表单的成员。例：`!!Y.Delete()`。

### ③.6 数据库 SAVEWORK / CLAIM / EXTRACT / UNDODB（DBRM，官方逐字）

**SAVEWORK / GETWORK** — DBRM5.06.1
```
SAVEWORK 'comment' [n]
GETWORK [n]
```
- `SAVEWORK`：保存 MODEL 改动而不退出，可选 `n`=只存 STATUS 第 n 个 DB。`GETWORK`：刷新所有 READ 库、拾取他人已 SAVEWORK 的改动（会拖慢后续访问，官方建议谨慎）。例：`SAVEWORK 'end of pipe routing'`、`GETWORK 2`。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/DBRM/DBRM5.06.1.html

**CLAIM / UNCLAIM** — DBRM6.07.2
```
>-- CLAIM ----*-- elementname --+-- HIERARCHY ---.
                                 |                |
                                 '----------------+-->

>-- UNCLAIM ---*-- elementname --+-- HIERARCHY ---.
                                 | |              |
                                 '-- ALL ----------+-->
```
- `*`=可循环多个 elementname；`HIERARCHY`=连同从属层级。切换模块/退出时自动 unclaim。删元素前必须先 claim。例：`CLAIM /ZoneA HIERARCHY`、`UNCLAIM ALL`。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/DBRM/DBRM6.07.2.html

**EXTRACT** — DBRM6.07.4
```
>- EXTRACT -+- FLUSH ----------------.
            |- FLUSHWithoutrefresh --|
            |- RELEASE --------------|
            |- ISSUE ---------------.|
            |- DROP ----------------|
            |                       .-------<-------.
            '- REFRESH -----------+--*-- elementname --+- HIERARCHY -.
                                 |                    |
                                 '-- DB dbname -------+->
(另: EXTRACT CLAIM ALL <type> WHERE (<expr>) [HIERARCHY])
```
- FLUSH=改动写回父 extract 保留 claim；ISSUE=写回并释放；DROP=丢弃未 flush 改动；REFRESH=用父/master 改动刷新本 extract；RELEASE=释放 extract claim（只能释放已 flush 的）。例：`EXTRACT FLUSH`、`EXTRACT ISSUE`、`EXTRACT CLAIM ALL STRU WHERE (:OWNER EQ 'USERA') HIERARCHY`。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/DBRM/DBRM6.07.4.html
- **RELEASE 澄清**：本组无独立顶层 `RELEASE`，只以 `EXTRACT RELEASE` 出现；user 级释放用 `UNCLAIM`。

**Q CLAIMLIST** — DBRM6.07.5
```
>-- Q CLAIMLIST --+- OTHER -----.
                  |- EXTRACT ---+- OTHER --.
                  |             |- FREE ---|
                  |             '----------|
                  |------------------------+-- DB dbname --.
                  '----------------------------------------+-->
```
- 口诀：`Q CLAIMLIST EXTRACT` 告诉你能 flush 什么；`Q CLAIMLIST OTHER` 告诉你不能 claim 什么。例：`Q CLAIMLIST EXTRACT DB /PIPE-EXTRACT`。

**UNDODB / REDODB / MARKDB** — DBRM7.08.1（⚠ 真名带 DB 后缀，非 UNDO/REDO/MARK）
```
UNDODB
REDODB
MARKDB 'comment'
```
- `UNDODB`：撤销最近 transaction（可多次）；其后任何 DB 改动使 REDODB 失效。`REDODB`：重做到下一个 mark（仅在 UNDODB 后有效）。`MARKDB 'comment'`：完成当前 transaction 并开新 transaction 作 undo/redo 边界（comment 必填）。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/DBRM/DBRM7.08.1.html
- **仍未找到**：`LOCK` / `UNLOCK`——DBRM SAVEWORK/CLAIM/EXTRACT 组内无此命令（并发保护靠 CLAIM，DB 访问模式属 ADMIN 手册）。

### ③.7 CE 导航（DBRM 第 2.03 章，官方逐字）

| 命令 | 语法 | 页码 | 说明 |
|---|---|---|---|
| /name | `/<name>` | DBRM2.03.06 | 按标准名跳转（仅命名元素）：`/PUMP99` |
| 构造名 OF | `<type> <int> OF <owner-name>` | DBRM2.03.07 | 按类型+相对位链式定位：`NBOX 1 OF CYL 2 OF EQUI 4 OF ZONE 9 OF /MYSITE` |
| WORLD | `/*` \| `WORLD` | DBRM2.03.08 | 跳到 DB 顶（World），二者等价 |
| Refno | `=<n>/<n>` | DBRM2.03.09 | 用引用号直跳：`=1234/5678` |
| OWNER | `OWNER` | DBRM2.03.12 | 上移到 owner，位置停在**第一成员之前** |
| END | `END` | DBRM2.03.12 | 上移到 owner，位置停在**climb 来的那个元素** |
| climb-to-type | `<element-type>` | DBRM2.03.12 | 上移到最近的该类型 owner |
| NEXT | `NEXT [ <int> ] [ <element-type> ]` | DBRM2.03.13 | 同层前移 |
| PREVIOUS(PREV) | `PREVIOUS [ <int> ] [ <element-type> ]` | DBRM2.03.13 | 同层后移 |
| FIRST | `FIRST [ <int> ] [ <element-type> ]` | DBRM2.03.13/.14 | 到同层首个 / `FIRST MEMBER` 降级到 CE 首成员 |
| LAST | `LAST [ <int> ] [ <element-type> ]` | DBRM2.03.13/.14 | 到同层末个 / `LAST MEMBER` 降级到 CE 末成员 |
| MEMBER 降级 | `<int>` \| `MEMBER <int>` \| `FIRST/LAST MEMBER` \| `<element-type> <int>` | DBRM2.03.14 | 下移一层（裸整数选第 n 个子件） |
| SAME | `SAME` | DBRM2.03.19 | 返回上一个 CE（本手册的 previous-CE 机制） |
| CE / !!CE | `CE`（命令关键字）\| PML `!!CE`（全局 DBREF） | DBRM2.03.02/.05 | 指代当前元素；PML 全局变量 !!CE 跟踪 CE |

- **仍未找到**：`GOTO`——本章无 `GOTO` 导航命令；返回上一 CE 用 `SAME`，绝对跳转用 `/name`、`=refno`、`OF` 构造名。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/DBRM/DBRM2.03.02.html （及 .05/.06/.07/.08/.09/.11/.12/.13/.14/.19）

### ③.8 显示与视图（DRMPGC 第 15 章 Display，官方逐字）

**ADD** — DRMPGC15.05.02
```
>-- ADD --+-- Only --+ .----<-------.
          |          |/            |
          '----------*-- <selatt> --+-- COLour <colno> -->

<colno> = integer | ACTive | VISIble | CE | CLASH | OBST | AIDS
```
- 加元素进 Draw List / 3D 视图。`ONLY` 只加所选本身。例：`ADD /P100A`、`ADD CE`、`ADD /ZONE1 /ZONE2 COLOUR 5`。

**REMOVE** — DRMPGC15.05.04
```
>-- REMove --+-- Only --+
             |          |
             '----------+-- <selatt> ------>
(REM ALL：清空整个 Draw List 与屏幕)
```
- 例：`REMOVE ONLY /VESS1/N1`、`REM SITE /PIPING`、`REM ALL`。

**ENHANCE / UNENHANCE** — DRMPGC15.05.21
```
>-- ENHANCE --+-- SOLELY --.  .----<----.
              |            |  |         |
              '-----------+*-- <selatt> --+-- COLour <colno> --.
                                          |                    |
                                          +- LENGth <uval> OF -+- <hlid> --+-->
                                          '- TOTAl ------------'
<hlid> = LEAVE | ARRIVE | HEAD | TAIL [ ROD | TUBE ] OF <gid> COLour <colno>

>-- UNENHANCE --+-- <selatt> --+-- <selatt> --.
                |             |               |
                +- <hlid> -----+--------------+-->
```
- 高亮 Draw List 里的构件。`SOLELY` 只高亮所选、清其余。例：`ENHANCE SOLELY ALL REDU WITH (ABOR GT 10) COLOUR 13`、`ENHANCE LENGTH 20 OF LEAVE TUBE COLOUR 7`、`UNENHANCE ALL`。

**AXES** — DRMPGC15.05.18
```
  .-----------<-------------------------.
  |                                     |
>-- AXEs --*-- HEIght <value> ----------------------+
           |-- AT <bpos> --------------------------|
           |-- AT POLar <bdir> DISTance <uval> ----|
           |-- AT @ -------------------------------|
           |-- ON ---------------------------------|
           |-- OFF --------------------------------|
           '-- DELete -----------------------------+-->
```
- 例：`AXES HEIGHT 300 AT @`、`AXES OFF`、`AXES DELETE`。

**MARK / UNMARK** — DRMPGC15.05.19
```
>-- MArk --+-- WITH <text expression> --.
           |                             |
           '-----------------------------+-- <selatt> -->

>-- UNMark -- <selatt> -->        (UNMARK ALL：清除全部标注)
```
- 例：`MARK WITH 'Outer Boundary' ID @`、`MARK WITH NAME ALL BRAN`、`UNMARK ALL`。

**AID（设计辅助图元，不入模型库）** — DRMPGC15.05.20
```
>- AID LINE ----+- NUMber <int> -+- <bpos> TO <bpos> --+- LINEStyle -+- SOLId --.
                                                       |             |- DASHEd -|
                                                       |             |- DOTTEd -|
                                                       |             '- DASHDot-+->
>- AID TEXT ---- NUMber <int> <text_expression> AT <bpos> ->
>- AID ARROW ---+- NUMber <int> -+- AT <bpos> DIRection <bdir> [HEIght <val>] [PROPortion <val>] ->
>- AID CEARROW -+- ON | OFF -+ [HEIght <val>] [PROPortion <val>] -+- ARRIVE | LEAVE | ORIGIN -->
>- AID ARC -----+- NUMber <int> -+- <bpos> TO <bpos> -+- TANPoint <bpos> | THRU <bpos> -->
>- AID SPHERE --+- NUMber <int> -+- <bpos> DIAmeter <expre> ->
>- AID BOX -----+- NUMber <int> -*- POSition <bpos> | ORIentation <ori> | XLENgth <expre> | YLENgth <expre> | ZLENgth <expre> | FILLed ON|OFF -->
>- AID CYLinder-+- NUMber <int> -*- POSition <bpos> | AT <bpos> | ORIentation <ori> | DIAmeter <expre> | HEIght <expre> | FILLed ON|OFF -->
>- AID CLEAR ---+- ALL | LINE | ARROW | CEARROW | ARC | SPHERE | BOX | CYLInder [<int> | ALL | UNNumbered] -->
```
- 例：`AID LINE E1200S3500U0 TO E760N1200U50`、`AID BOX NUMBER 1 POSITION E100N0U0 XLENGTH 500 YLENGTH 500 ZLENGTH 500`、`AID CLEAR ALL`。

**UPDATE（=REPR UPDATE）** — DRMPGC15.05.17
```
>-- REPResentation -- UPDATE -->
```
- 改了 representation 设置后直接重绘，无需从 Draw List 移除再加回。例：`REPR UPDATE`。

**AUTOCOLOUR** — DRMPGC15.05.03
```
>- AUTOCOLOUR -+- <selection_rule> COLOUR <expression> -+- TRANSLucency <expression> -+->
               |                                       '- EDGES ON | OFF ------------|
               |- ON | OFF -----------------------------------------------------------|
               |- DYNAMIC ON | OFF ----------------------------------------------------|
               |- RESET ---------------------------------------------------------------|
               |- REMOVE <integer> ----------------------------------------------------|
               '- REORDER <integer1> TO <integer2> ------------------------------------+->
```
- 例：`AUTOCOLOUR ALL EQUI COLOUR 4`、`AUTOCOLOUR ALL BRAN WITH (HBORE GT 100) COLOUR 10`、`AUTOCOLOUR DYNAMIC ON`、`AUTOCOLOUR RESET`。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/DRMPGC/DRMPGC15.05.02.html （及 .03/.04/.17/.18/.19/.20/.21）
- **低置信**：`REPR` 完整语法仅 `UPDATE` 分支逐字确认；TUBE/PROFILE/INSULATION/P-POINT 等分支散在 15.05.07–15.05.16（本轮未逐一抓取，标 toc-only）。

### ③.9 自动布管 ROUTE / MATCHWILD（DRMPCM / SCRM，官方逐字）

**ROUTE（AUTOROUTE 子模式内）** — DRMPCM25.10.08
```
>-- ROute --+-- WIth --+-- ELbows --.
            |          |            |
            |          '-- BEnds ---| .-----<----.
            |                       |/          |
            '-----------------------+-*- <sgid> -->
```
- 须先 `AUTOROUTE` 进入子模式、`EXIT` 退出（DRMPCM25.10.03）。`WIth ELbows/BEnds` 二选一（缺省 Elbows）；`<sgid>` 可循环列多个待布管选择组；待布管 Branch 须为空且已设 Head/Tail。例：`ROUTE /PIPES`、`ROUTE WITH BENDS /PIPES`。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/DRMPGC/DRMPCM25.10.08.html

**AUTOROUTE / EXIT（进出子模式）** — DRMPCM25.10.03
```
>-- AUTOROUTE -->
>-- EXIT -->
```

**MATCHWILD（PML STRING 对象方法）** — SCRM3.3.79
```
BOOLEAN MatchWild( STRING pattern )
BOOLEAN MatchWild( STRING pattern, STRING multiChar )
BOOLEAN MatchWild( STRING pattern, STRING multiChar, STRING singleChar )
```
- 返回 BOOLEAN；缺省通配 `*`=任意串、`?`=单字符；可选第 2/3 参重定义多/单字符通配符（须相异，仅 pattern 可含通配符）。例：`if (!line.MatchWild('*Valve*')) then ...`。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/SCRM/SCRM3.3.79.html
- **低置信（community）**：命令/表达式语言里的 `MATCHWILD ( <text>, <pattern> )` 文本函数形态（如 `Q ALL PIPE WITH ( MATCHWILD ( NAME , '/*V*' ) )`）规则与 SCRM 一致但独立逐字 railroad 页只在 docs.aveva.com Zoomin（JS 渲染）未抓到 → 标 community。

### ③.10 仍未找到 / 未抓正文（not-found）

- **`NAME`（独立元素改名命令）**：DBRM 4.5.x 无独立命令页，改名靠设 NAME 属性；社区 `NAME /newname` 未从官方静态页逐字确认。
- **`LOCK` / `UNLOCK`**：DBRM SAVEWORK/CLAIM/EXTRACT 组内无此命令（并发靠 CLAIM，访问模式属 ADMIN 手册）。
- **`GOTO`**：DBRM2.03 导航章无此命令（用 `SAME` / `/name` / `=refno` / `OF`）。
- **QRM-Q 询问族（Q ATT / Q NAME / Q TYPE / Q OWN / Q MEM / Q CONNECTIONS / Q BORE / Q ANGLE / Q RADIUS）**：`fetched=false`。归属更正——这些**不在** QRM（QRM 实为外部 Query/ODBC 数据服务器接口 OPEN/SEND/GET/CLOSE），而在 docs.aveva.com e3d-design「Query Commands」章（索引页 1049766；`Q ATT` 独立页 912035）。该站 Zoomin JS 渲染，WebFetch 只回导航壳，**逐字语法图未取到**（标 toc-only，第 9 章现有条目不升级置信度）。
- **`Q ITLE` / `Q ATLE`**：所有官方检索**零命中**，疑为笔误/非标准拼写，标 **not-found**，绝不臆造。

## 附录④：第三轮定向重扒（2026-07-03，含 Q 查询返回值 + 诚实置信度）

> 本附录是对附录②/③仍为 **community / official-toc-only / unverified** 的 **31 条命令**做的**第三轮定向重扒**。分 5 组：Q-queries、control-flow、spec-extra、editing-extra、misc。
>
> **诚实总纲（先说清哪些没升上去、为什么）：**
> - **Q 查询族（Q ATT / Q NAME / Q TYPE / Q OWN / Q MEM / Q ORI / Q BORE·HBORE·TBORE / Q P1·P2·P3 / Q ITLE / Q ATLE / Q TULE / Q SPRE / Q CONNECTIONS·HCON·TCON / Q ANGLE / Q RADIUS / Q PARA / Q NVOL / Q NWEI）**：**整族未能升级为 official-verbatim**。根因——这些 DESIGN 命令行 QUERY 动词的官方逐字页只在 `docs.aveva.com` e3d-design「Query Commands」(929318 / 912035)、「Pseudo-Attributes」(929320)、「Command Syntax」(925396) 上，**全是 Zoomin JS 渲染 SPA**，本轮 WebFetch 仍只回导航壳（'Results for How to create a CRG?'）无正文。**以下 Q 语法为社区来源 + 返回值说明，真机 `$Q` 为准。** 关键辨误：任务点名的 **QRM 是错手册**——静态可抓的 QRM 实为**外部 Query/ODBC 数据服务器接口**（EXTERNAL OPEN/CLOSE、ODBC DSN），与 Q ATT 族无关。
> - **`Q MTXX`**：官方与社区**均无记载**，疑笔误，标 **not-found**，绝不臆造；可用的材料引用查询是 `Q MATREF`。
> - **`FORWARDS` / `BACKWARDS`（独立命令行铁路图）**：仍 **community**——官方只把它们记为**建模方向模式**（FLIP 页 DRMPCM24.09.25 逐字出现「Forwards mode / Backwards mode」；PDUV 坡度页 With Flow/Against Flow；分支 FLOW 属性取 FORWARDS/BACKWARDS），**没有独立命令铁路图**。缩写 FORW/BACK 与「FORWARDS … DIR TOW NEXT」用法来自训练/社区材料。
> - **`LOCK ALL` / `LOCK CE`（元素级锁属性）**：仍 **community**——官方页「Special Syntax for LOCK」只在 docs.aveva.com 911901（Zoomin JS 抓不到）。**注意与项目库锁分开**：ACRM 的 LOCK/UNLOCK 是**锁整个项目库**（无 ALL 关键字、有 `AT <loc>`，本轮 official-verbatim）；元素级 `LOCK ALL/CE` 是设**元素及成员的 LOCK 属性**（防改），是另一条命令。
> - **`REPR UPDATE`**：仍 **official-toc-only**——命令确实存在（社区命令表一致），但官方 Element Representation 概览页 DRMPGC15.05.06 只有介绍文字、子页铁路图未把 UPDATE 画成独立分支，逐字图未取到。
> - **`CUT` / `SPLIT` / `GAP` / `JOIN`（作 3D 建模动词）**：**负向确认成立**，命令行不存在，维持黑名单。管道切分只在 GUI *Split Pipe* 窗口。辨误：字面名 `SPLIT` 只在 ISODRAFT（控图幅数）、`CUTBACK/CUTMARKS/CUTPIPELISTFILE/CUTTINGLIST` 也只在 ISODRAFT，**均非 3D 几何**。
>
> **本轮真正拿到 official-verbatim 的是这些**（来源 = `help.aveva.com` 的 1.1 / 2.1 / 3.1 **静态全文页**，可 WebFetch）：控制流 IF/DO/BREAK/HANDLE + 铁路记号图例、spec 的 SPREF/ISPEC/TSPEC/LSTUBE/`<uval>`/TUBE(TUBI)、editing 的 MIRROR、misc 的 LOCK/UNLOCK(项目库)/SESSION COMMENT/REPR HOLES·OBST·INSU/BANG。**铁路记号原样保留，不重排、不翻译语法本体。**

### ④.1 Q 查询族（community + 返回值说明；docs.aveva.com Zoomin JS 抓不到官方逐字，真机 `$Q` 为准）

> 下列全部 **confidence = community**（`Q ITLE/ATLE` 为 **unverified**，`Q MTXX` 为 **not-found**）。语法列的多为**社区一致缩写形态**，非官方铁路图。**返回值**来自 pdmsforyou(2009) / piping-knowledge(2021) / pipingengg3d(2024) / pipingguide / mehungryblog 多源交叉一致。

| 命令 | 语法（社区） | 返回值（社区说明） | 置信度 |
|---|---|---|---|
| `Q ATT` | `Q ATT` / `Q ATT /PIPE-100` | 把当前元素完整属性清单（名=值 对）转储到命令输出 | community |
| `Q NAME` | `Q NAME` | 元素 NAME 字符串（可为 `/` 前缀层级名，未命名返回空） | community |
| `Q TYPE` | `Q TYPE`（另见 `Q STYP` 选型/元素类型） | 元素 TYPE 关键字（PIPE/BRAN/ELBO/EQUI/BOX…）；本组社区证据最薄 | community |
| `Q OWN` | `Q OWN` | 属主（父）元素名或引用 | community |
| `Q MEM` | `Q MEM` | 直接成员（子元素）名/引用清单 | community |
| `Q ORI` | `Q ORI [IN <coordsys>] [WRT <element>\|/*]` | 朝向以方向轴表示，如 `Y is N and Z is U`（owner 坐标）或 `WRT /*`（世界）；是轴/方向记号非数值角 | community |
| `Q BORE` | `Q BORE` | 构件公称孔径（当前显示单位 mm） | community |
| `Q ABORE` / `Q LBORE` | `Q ABORE` / `Q LBORE` | Arrive 侧 / Leave 侧孔径（mm） | community |
| `Q HBORE` | `Q HBORE` | **分支头侧公称孔径（mm）** | community |
| `Q TBORE` | `Q TBORE` | **分支尾侧公称孔径（mm）** | community |
| `Q P1` / `Q P2` | `Q P1 [<element>]`（可嵌套 `Q P1 BOLT1 BLEN`） | P 点 bore、方向、连接类型、位置 | community |
| `Q P3` | `Q P3` | P 点 P3 数据（**仅三通类三向件如 TEE 有**） | community |
| `Q ITLE` | `Q ITLE` | 相邻两管件间隐含（inter-tube）管段长度（mm） | **unverified** |
| `Q ATLE` | `Q ATLE` | 分支第一段管长（mm） | **unverified** |
| `Q TULE` | `Q TULE` | 分支管道总长（整个分支 tubing 总长，mm） | community |
| `Q SPRE` / `Q SPREF` | `Q SPRE` | 规格引用——指向给出完整目录描述的 Specification Component（一个 DB 引用/规格名）；**属性定义本身官方逐字**（DRMPCM24.09.22），查询动词社区 | community |
| `Q HCON` / `Q TCON` | `Q HCON` / `Q TCON` | 分支头 / 尾连接类型/引用 | community |
| `Q ACON` / `Q LCON` | `Q ACONN` / `Q LCONN` | 组件 Arrive / Leave 连接类型/引用；**无单一 `Q CONNECTIONS` 动词，按端分查** | community |
| `Q ANGLE` | `Q ANGLE` | 构件 ANGLE 属性（如弯头/弯管角，度）；属性官方逐字，动词社区 | community |
| `Q RADIUS` | `Q RADIUS` | 构件 RADIUS 属性（如弯曲半径，mm）；属性官方逐字，动词社区 | community |
| `Q PARA` | `Q PARA` | 当前元素设计参数（PARAM 数组，驱动几何的数值参数） | community |
| `Q NVOL` | `Q NVOL` | CE 净体积（数值，立方长度单位，伪属性按需计算） | community |
| `Q NWEI` | `Q NWEI` | CE 净重（数值，当前质量单位，伪属性） | community |
| `Q MTXX` | — | **not-found**：无任何来源记载，疑笔误。材料引用请用 `Q MATREF`（返回材料引用） | **not-found** |

- 来源（社区）：pdmsforyou / piping-knowledge / pipingengg3d / pipingguide / mehungryblog。官方逐字目标页（**未抓到**）：docs.aveva.com/bundle/e3d-design/page/929318.html（Query Commands）、929320.html（Pseudo-Attributes）、925396.html（Command Syntax）、912035.html（Q ATT）。

### ④.2 control-flow 组（PML2 控制流，**多数升为 official-verbatim**，SOFTCG 1.1 静态页）

**IF / ELSEIF / ELSE / ENDIF** — official-verbatim — SOFTCG6.06.02
```
if (boolean_expression) then
  command_block
elseif (boolean_expression) then
  command_block
else
  command_block
endif
```
- **返回**：无（控制流结构）。逐个检查 BOOLEAN 表达式，遇第一个 TRUE 即执行其块、其余到 endif 忽略；else/elseif 可选，最简形 `if (expr) then ... endif`。
- **规则**：(1) 命令不得同行拼接——`if (…) then !Neg = TRUE endif` 与 `if (…) !Neg = TRUE` 均**禁止**（每关键字/块各占一行）；(2) IF/ELSEIF 布尔后须 `then`，`endif` 必填；(3) 短路：`(!TrueValue OR !UnsetValue)` 中未设值若不影响结果可被忽略。
- 例：`if (!Number LT 0) then` / `  !Negative = TRUE` / `endif`。相关页 SOFTCG6.06.03（嵌套）/6.06.04（布尔表达式）/6.06.05（IF TRUE）。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/SOFTCG/SOFTCG6.06.02.html

**DO / ENDDO（do !x from N to M by S）** — official-verbatim — SOFTCG6.06.06
```
do [!var] [from n] [to m] [by s]
  command_block
enddo
```
- **返回**：无（循环结构）。默认值：`from`=1；`to`=无穷（**无 `to` = 无限循环**，靠 break/golabel 跳出）；`by`=+1（可正可负）。`!var/from/to/by` 全可选，`enddo` 必填。
- 循环变量 `!var` 为 REAL，每次迭代自动更新计数值，循环后保留最终值；同名已存在变量在循环初始化时被删除。**PML 无 FOR/WHILE/REPEAT，全由 DO 替代。**
- 例：`do !x from 10 to 100 by 10` / `  !Total = !Total + !X` / `enddo`。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/SOFTCG/SOFTCG6.06.06.html

**DO !x VALUES / DO !x INDEX（数组迭代）** — official-verbatim — SOFTCG6.06.10
```
DO !x VALUES !array   $* !x 取每个数组元素的值
DO !x INDEX  !array   $* !x 为 1..数组大小的下标
```
- **返回**：无（循环结构）。VALUES 把数组每个元素直接赋 `!x`（如遍历 collection results 的 DBREF）；INDEX 给 1..size 的下标用 `!array[!x]` 取值。1.1 手册关键字为 **INDEX**（单数）。
- 例：`do !site values !allSites` / `  q var !site.name` / `enddo`；`do !index index !allSites` / `  q var !allSites[!index].name` / `enddo`。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/SOFTCG/SOFTCG6.06.10.html

**BREAK / BREAK IF（停止 DO 循环）** — official-verbatim — SOFTCG6.06.07（本轮新增条目）
```
break                          $* 无条件退出循环
break if (boolean_expression)  $* 条件为 TRUE 时退出
```
- **返回**：无。break 时循环计数变量保留 break 发生时的值。break if 可用任意表达式（含 PML Function），只要结果为 BOOLEAN；条件外括号可选但推荐。
- 例：`do !Number` / `  break if (!Number GT 100)` / `  !Result = !Result + !Number` / `enddo`。搭档 SOFTCG6.06.08（Skip / Skip if）。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/SOFTCG/SOFTCG6.06.07.html

**HANDLE / ELSEHANDLE / ENDHANDLE（错误处理）** — official-verbatim — SOFTCG11.11.2
```
$* 可能报错的命令
handle (err_group, err_number)
  $* 针对该具体错误的处理块
elsehandle (err_group, err_number)
  $* 针对另一具体错误
elsehandle ANY
  $* 处理任何(未匹配)错误
elsehandle NONE
  $* 仅在无错误时处理
endhandle
```
- **返回**：无（错误处理结构）。错误以 `(group, number)` 对标识（如 `(46,28)`）。命中某 handle 块时 PML 执行该块命令而非输出错误，然后从 ENDHANDLE 后继续；错误后无 handle 命令时行为取决于 ONERROR（默认 `onerror RETURN`）。
- **关键规则**：宏/函数中的错误不立即报出，取决于下一行输入——handle 命令必须是紧接的下一条命令。`ELSEHANDLE ANY` 捕获任何未匹配错误；`ELSEHANDLE NONE` 仅在无错误时运行。
- 例（社区建模示例，仅作说明）：`NEW EQUI /ABCD` / `handle (41, 8) $P Need to be at a ZONE or below` / `elsehandle (41, 12) $P name already used` / `elsehandle ANY $P another error` / `elsehandle NONE $P EQUI created` / `endhandle`。相关页 SOFTCG11.11.1 / 11.11.3（ONERROR）/ 11.11.4。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/SOFTCG/SOFTCG11.11.2.html

**铁路记号图例** — official-verbatim — QRM4.4.01（另 CSRM2.03.3 同规约）
```
Mixed-case word   = 可缩写命令；大写=最小缩写（如 CONnect → CON..CONNECT）
lowercase italics = 命令参数（你提供的值）
<lowercase>       = 子语法图引用
+                 = 选项汇合：任取一支或默认(RETURN)
*                 = 回跳节点：可重复该选项任意组合
>-  ... ->        = 铁路主线（图的进出口）
```
- **返回**：N/A（记号说明）。用于解码本手册所有命令 syntax 里的 `>-` / `+` / `*` / `<小写>` / 大写缩写记号，跨 QRM/CSRM/DRMPGC 通用。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/QRM/QRM4.4.01.html

**建模方向相关（本轮同批抓到，附此说明置信度）：**
- `DIRECTION` / `DIR`（DRMPCM24.09.37）、`CONNECT` / `CON`（DRMPCM24.09.38）、`FLIP`（DRMPCM24.09.25，**唯一逐字点名 Forwards / Backwards 模式**的官方页）——均 **official-verbatim**（3.1 DRMPGC 静态页），已在附录③.3/③.4 与数据集登记。
- `FORWARDS` / `BACKWARDS`（**独立命令铁路图**）—— 仍 **community**：官方只记为**分支建模方向模式**（FLIP 页逐字「Forwards mode / Backwards mode」；PDUV 坡度页 With Flow=forwards / Against Flow=backwards；分支 FLOW 属性）。独立命令铁路图 Zoomin/JS 抓不到，缩写 FORW/BACK 与「FORWARDS … DIR TOW NEXT」用法来自训练/社区材料，真机为准。

### ④.3 spec-extra 组（**升为 official-verbatim**，DRMPGC/DRMPCM/DBRM 静态页）

**SPREF（组件规格引用属性）** — official-verbatim — DRMPCM24.09.26 + DRMPCM24.09.22
```
>-- SPRef name -->     （设置形态，官方页以平文本流呈现）
```
- **返回**：`Q SPREF` 返回本元素所指规格组件的名字/引用（如 `/SPEC208/EL50BW`）；**未设 = 无几何**。
- 逐字：SPREF「points to a Specification Component that provides the complete Catalogue description of the current element.」；「This attribute is usually inserted automatically as a direct result of the CHOOSE (or SELECT) command. It can, however, be set directly to the name of the required Specification Component.」；「If the SPREF is not set, a Valve, for example, is merely a hierarchical element and has no geometry.」缩写 SPRE。⚠ 交互 `Q SPREF` 查询动词形态为社区。
- 例：`SPREF /SPEC208/EL50BW`、`Q SPREF`。来源：https://help.aveva.com/AVEVA_Everything3D/3.1/DRMPGC/DRMPCM24.09.26.html

**ISPEC（绝缘/保温规格）** — official-verbatim — DRMPCM24.09.22
- **语法**：`ISpec name`（设置，同 SPREF 模式）；`ISPEC NULREF`（社区：清除绝缘）；`Q ISPEC`（社区：查询）。
- **返回**：`Q ISPEC` 返回当前元素/分支所指绝缘规格引用；绝缘厚度据 **Branch TEMPERATURE** 属性从该规格派生。
- 逐字：ISPEC「points to an Insulation Specification. The Branch "TEMPERATURE" attribute is automatically used to determine an insulation thickness from this Specification.」——引用属性，同族 SPREF/TSPEC。⚠ `Q ISPEC` / `ISPEC NULREF` 交互动词为社区；P&ID 中性平文件里 `ISPEC insulation_spec`（官方 PDUV14.5.2）是**导入文件字段**，非交互命令语法。
- 来源：https://help.aveva.com/AVEVA_Everything3D/3.1/DRMPGC/DRMPCM24.09.22.html

**TSPEC（伴热/示踪规格）** — official-verbatim — DRMPCM24.09.22
- **语法**：`TSpec name`（设置）；`Q TSPEC`（社区：查询）。
- **返回**：`Q TSPEC` 返回本元素所指的**哑**伴热规格引用；供 ISODRAFT 在等轴图上标示伴热要求。
- 逐字：TSPEC「points to a dummy Tracing Specification and is used by ISODRAFT to indicate trace heating requirements.」——引用属性，同族 SPREF/ISPEC。⚠ `Q TSPEC` 交互动词为社区；`TSPEC tracing_spec`（PDUV14.5.2）是导入文件字段。
- 来源：https://help.aveva.com/AVEVA_Everything3D/3.1/DRMPGC/DRMPCM24.09.22.html

**LSTUBE（离开侧管段规格引用）** — official-verbatim — DRMPCM24.09.22（本轮新增条目）
- **语法**：引用属性，经 `Q LSTUBE` 查询。
- **返回**：`Q LSTUBE` 返回给出离开侧 Tube 之 **LSROD** 目录描述的规格组件引用。
- 逐字：LSTUBE「These point to a Specification Component that provides the complete Catalogue LSROD description of the Tube emerging from the current element Leave Point.」——是隐含 TUBE 几何的**规格侧搭档**，控制离开管段尺寸。
- 来源：https://help.aveva.com/AVEVA_Everything3D/3.1/DRMPGC/DRMPCM24.09.22.html

**`<uval>`（物理量/带单位数值子语法）** — official-verbatim — DRMPGC13.03.07
```
>-+- <value> -+- [EXPON int] -+- [SQuare|CUBIC|CUbe] -+- [QUANTITY] -+- <unit> -+->
              '- <unit> = syntaxUnit [prefix] | prefix standardUnit -+  [PER <unit>] | [addSyntaxUnits|addStandardUnits]
```
- **返回**：非查询命令——`<uval>` 是**输入/解析带单位物理量**的标准记号（数值 + 单位），存库用数据库单位、回显用用户显示单位。
- 铁路顶层次序：(1) 必填 `<value>`；(2) 可选 `EXPON int`；(3) 可选幂限定 SQUARE/CUBIC/CUBE；(4) 可选 `QUANTITY`；(5) 单位（syntaxUnit 带可选 prefix 或 prefix+standardUnit）；(6) 可选复合尾 PER / addSyntaxUnits / addStandardUnits。逐字规则：「Initial standard units cannot be abbreviated if no secondary units follow.」；标准单位复合用点(.)相乘、斜杠(/)相除并带幂指数；prefix 与单位无空格拼接（如 centilitre）；幂指数为 <9 整数可带 +/-；FINCH 混合英尺英寸用撇号(')。
- 单位表 100+ 条含 MM/METRE/INCH/FEET/KG/POUND/LITRE/GALLON/DEGC/KELVIN/BAR/PASCAL/PSI/WATT/VOLT/AMPERE/HERTZ/DEG/RADIAN 及货币 GBP/USD/EURO。例：`100 METRE/s`、`50 KG`、`5'8"`。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/DRMPGC/DRMPGC13.03.07.html （docs.aveva.com 云镜像 926161 返回搜索壳无正文）

**TUBE（隐含管段，元素类型 TUBI）** — official-verbatim — DBRM11
- **语法**：**无独立 NEW TUBE 命令**；元素类型 = `TUBI`；集合中以 `IL TUB OF =<component>` 寻址。
- **返回**：TUBI/隐含管成员代表分支中相邻两管件间的管段；集合写作 `IL TUB OF =20/302`。其 bore/几何来自离开侧管件的 LSTUBE（LSROD 目录）。
- 逐字：「If a selection contains elements of type TUBIng, then the collection describes it as the Leave Tube of an existing database element」，写作 `IL TUB OF =<component>`。⚠ 社区/训练补充：底层目录 TUBE 用单一轴向 P 点(PTAX)、PBOR=PARA 1、PDIS=0，隐含 TUBE 的 PARA 2 预留 0.0——目录编写细节非可抓官方页。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/DBRM/DBRM11.html

### ④.4 editing-extra 组（MIRROR **升为 official-verbatim**；CUT/SPLIT/GAP/JOIN 维持黑名单）

**MIRROR（平面反射）** — official-verbatim — DRMPCM23.08.32
```
>-- MIRRor -- <plane> -->
```
- **返回**：无返回值。把当前元素(CE)移到「反射其初始位置于指定平面」算得的新位置，保持右手系朝向。
- 唯一官方例：`MIRROR PLANE E45D THRO /TANK5`（在过 `/TANK5`、方向 `E45D` 的平面内反射 CE 位置）。`<plane>` = `PLAne <bdir> [THRough <bpos> | DISTance <uval> | CLEArance <uval>]`。⚠ `<plane>` 分支子语法被 markdown 转换弄糊，**仅顶层行 + 例逐字可信**；`<plane>` 分支细节为按 23.08 组共享 plane-token 重构，非逐字。缩写 MIRR。**与结构/GUI「MI/Mirror」工具、「Copy Mirror」装配工具是不同特性。**
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/DRMPGC/DRMPCM23.08.32.html

**CUT / SPLIT / GAP / JOIN（作 3D 建模动词）** — not-found / 黑名单维持
- **负向确认**：管道切分是 **GUI-only**——PDUV7.4.4 逐字「All of the tasks that are associated with the splitting of pipes is initiated from a central Split Pipe window which acts as a task hub.」三种操作（Split on a Plane / into Segments / by moving Component）全是 GUI 工作流，**无命令行 SPLIT/CUT 切 3D 几何**。
- **辨误（本轮新查证）**：字面名 `SPLIT` **确实存在但只在 ISODRAFT**（ISORM3.03.103：控制管线跨多张图纸的图幅数，`>--- SPLIt --- value ---+--- DRwgs | PERCent -->`，如 `SPLIt 3 DRwgs`）；`CUTBACK/CUTMARKS/CUTPIPELISTFILE/CUTTINGLIST` 也只在 ISODRAFT——**均为等轴图输出/注记，非 3D 几何编辑**。`GAP` / `JOIN` 在 DRMPGC 定位命令组与 ISORM 命令表标题中**无任何页**。
- 结论：`CUT/SPLIT/GAP/JOIN` 作 3D 建模动词 = **黑名单成立**，程序化切管须走「插入组件 + 重设头尾 + 新建分支」。
- 来源：https://help.aveva.com/AVEVA_Everything3D/2.1/PDUV/PDUV7.4.4.html ；https://help.aveva.com/AVEVA_Everything3D/2.1/ISORM/ISORM3.03.103.html

### ④.5 misc 组（LOCK/UNLOCK 项目库 · SESSION COMMENT · REPR · BANG **升为 official-verbatim**；元素级 LOCK ALL 维持 community）

**LOCK / UNLOCK（项目/数据库级锁）** — official-verbatim — ACRM8.08.053 / ACRM8.08.101
```
>--- LOCK ---+--- AT <loc> ---.        >--- UNLOck ---+---------------.
             |                 |                       |               |
             '----------------->                       '--- AT <loc> -->
```
- **返回**：无直接返回值（执行即锁/解锁库）。`Q LOCK` 返回项目当前锁定状态。
- LOCK 锁定整个项目数据库、阻止其它用户进入该项目直到 UNLOCK；UNLOCK「Unlocks all locked databases.」。`AT <loc>` 仅在 Global Project Hub/管理位置可用（锁远端库）。「Locking and Unlocking commands are not recorded in the transaction database.」缩写 UNLOck。
- **⚠ 注意**：ACRM 的 LOCK **无 `ALL` 关键字**——`LOCK ALL` 是**元素级**锁属性命令（见下条），别混淆。
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/ACRM/ACRM8.08.053.html ；.../ACRM8.08.101.html

**LOCK ALL / LOCK CE / UNLOCK ALL（元素级锁属性）** — community
- **语法**：`LOCK` / `LOCK CE` → 锁当前元素(CE)；`LOCK ALL` → 锁 CE 及所有子元素/成员；`UNLOCK` / `UNLOCK CE` → 解锁 CE；`UNLOCK ALL` → 解锁 CE 及所有子元素/成员。
- **返回**：设置元素 LOCK 属性(TRUE/FALSE) 防误改；`Q LOCK` 返回该元素锁状态。
- **⚠ 与项目库锁两条不同命令**：本条设**元素及成员的 LOCK 属性**（防改），ACRM 那条锁**整个项目库**（防进入）。官方页「Special Syntax for LOCK」只在 docs.aveva.com 911901（Zoomin JS，本轮抓不到），ALL 形式逐字铁路图未取到，语义形态为社区一致来源。

**SESSION COMMENT** — official-verbatim — DBRM5.06.3（查询 DBRM12.13.04）
```
>-- SESSION COMMENT -- text -->      （亦可 SAVEWORK 'MY COMMENTS' 一步带上）
Q --+-- SESSComment --+
    |-- SESSUser -----|
    '-- SESSDate -----+-- integer -->
```
- **返回**：SESSION COMMENT 本身无返回值。`Q SESSComment integer` 返回该 session 注释文本；`Q SESSUser integer` 返回责任用户名；`Q SESSDate integer` 返回创建日期时间。
- 逐字：「Lets you associate comment text with the current MODEL session」——给当前设计会话打注释，便于识别哪次 session 改了什么。**应在 SAVEWORK 之前输入**。integer=session 号；Session 1、2 由 ADMIN 建 MODEL DB 与 World 元素时创建，故第一个真正设计 session 是 **Session 3**。
- 例：`SESSION COMMENT 'Addition of upper platform'`、`Q SESSComment 58`。来源：https://help.aveva.com/AVEVA_Everything3D/1.1/DBRM/DBRM5.06.3.html

**REPR HOLES / OBST / INSU（显示表示 + 透明度）** — official-verbatim — DRMPGC15.05.15 / 15.05.10
```
HOLES:      >-- REPResentation --*-- HOLes ---+-- ON | OFF -->
OBST/INSU:  >- REPResentation -*- OBSTruction | INSUlation -+- ON [TRANSLucency value] | OFF -->
```
- **返回**：设置孔/障碍/保温包络显示。`Q REPR` 列全部；`Q REPR HOLES` / `Q REPR OBST` / `Q REPR INSU` / `Q REPR INSU TRANSL` 分别返回各状态。
- HOLES ON=「更真实孔洞视图，孔后物体可透视」/ OFF=「把孔表示为构件表面图案区域」。TRANSLucency value 取 0–99（就近取 25/50/75/87）。铁路 `*` 循环节点=一条命令可连设多项。刷新用 `REPR UPDATE`（见下）。
- 例：`REPR HOLES ON`、`REPR OBST ON INSU OFF`、`REPR INSU ON TRANSLUCENCY 25`。来源：https://help.aveva.com/AVEVA_Everything3D/1.1/DRMPGC/DRMPGC15.05.15.html ；.../DRMPGC15.05.10.html

**REPR UPDATE（刷新图形视图）** — official-toc-only — DRMPGC15.05.06
- **返回**：无返回值；立即按当前 REPR 设置重绘图形窗口。
- 命令确实存在（社区命令表 `REPR HOLES ON UPDATE` / `REPR OBST ON UPDATE` 一致），但官方 Element Representation 概览页只有介绍文字、子页铁路图未把 UPDATE 画成独立分支——**逐字语法图未取到**，标 official-toc-only。（注：数据集另有 `e3d-cmd-display-update` 从 DRMPGC15.05.17 已登记为 official-verbatim 的 `>-- REPResentation -- UPDATE -->`。）
- 来源：https://help.aveva.com/AVEVA_Everything3D/1.1/DRMPGC/DRMPGC15.05.06.html

**BANG（Beta Angle — 结构截面绕轴转角属性）** — official-verbatim — DRMPCM26.11.09（3.1 分支）
```
>-- BANGle value -->          查询：Q BANGle
```
- **返回**：设置结构截面(SCTN)的 **Beta 角**属性=截面绕其中性轴/纵轴的旋转角；`Q BANG` 返回当前 Beta 角(度)。
- 逐字：「controls rotational orientation of a structural section around its neutral axis」，旋转量=从 POSS 朝 POSE 方向观察的顺时针角=「the angle of rotation from the default orientation」。**BANG = Beta ANGle，是钢结构截面的方位属性，不是一条独立「绕轴转角动作」命令**——任务描述「绕轴转角，若存在」成立=它是截面 Beta 角属性。缩写 BANGle(BANG)。1.1 Standard Component Attributes(DRMPCM24.09.22) 只列通用 ANGLE 不含 BANG，逐字语法取自可抓的 3.1 分支。
- 例：`BANG 90`、`BANGLE 45`、`Q BANG`。来源：https://help.aveva.com/AVEVA_Everything3D/3.1/DRMPGC/DRMPCM26.11.09.html

---

**第三轮一句话诚实小结**：本轮把 **control-flow（IF/DO/BREAK/HANDLE + 铁路图例）、spec-extra（SPREF/ISPEC/TSPEC/LSTUBE/`<uval>`/TUBE）、editing（MIRROR）、misc（项目库 LOCK·UNLOCK / SESSION COMMENT / REPR HOLES·OBST·INSU / BANG）** 从 community/toc-only/unverified **升为 official-verbatim**（全部来自可抓的 `help.aveva.com` 1.1/2.1/3.1 静态页，含逐字铁路图），并新增 LSTUBE / BREAK / 铁路图例 / 元素级 LOCK 四条；但 **Q 查询整族仍升不上去**——官方逐字页在 `docs.aveva.com` Zoomin JS 上抓不到，只补齐了**返回值说明**（真机 `$Q` 为准），`Q MTXX` 判 not-found，`FORWARDS/BACKWARDS` 独立命令铁路图与元素级 `LOCK ALL` 仍是 community。要把 Q 族真正升到官方逐字，需**JS 可渲染的浏览器**抓 docs.aveva.com/bundle/e3d-design/page/929318·929320·925396·912035。