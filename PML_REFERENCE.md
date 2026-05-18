# AVEVA E3D PML 完整参考手册

> 来源: `yanjiaqing2026-art/AVEVA_PML` + `E3D_CSharp_Addin` + `vscode-pml-aveva-e3d`
> AI Agent 可以直接按本文档语义生成 PML 代码

---

## 1. 变量

### 声明
```pml
!x = 'hello'                  # PML2: 直接赋值
var !x 'hello'               # PML1: VAR 声明
!x = object string()          # 空字符串
!x = object array()           # 空数组
!x = object real()            # 空实数
!x = object boolean()         # 空布尔
!!x = 'global'                # 全局变量 (!! 前缀)
```

### 基本类型
```pml
!x = 5                        # REAL
!x = 'Tommy'                  # STRING
!x = |Tommy|                  # STRING (竖线定界)
!x = true                     # BOOLEAN
```

### 复合类型
```pml
!arr = object array()
!arr[1] = 5                   # 数组索引从 1 开始
!arr[2] = 'Tommy'
!arr[3] = true
!arr[4] = object array()      # 多维数组

!pos = position                # 位置 (East, North, Up, Origin)
!ori = orientation             # 朝向 (Alpha, Beta, Gamma, Origin)
```

### 类型转换
```pml
!bool = !boolString.boolean()  # 转布尔
!real = !realString.real()     # 转实数
!str = !real.string('D2')      # 转字符串 (D2=两位小数)
!str = !bool.string()          # 转字符串
```

### 变量操作
```pml
q var !x                        # 查询变量值
!x.delete()                     # 销毁变量
!x.objecttype()                 # 获取类型名
!x.methods()                    # 列出所有方法
!!ce.attributes()               # 列出元素所有属性名
```

---

## 2. 运算符

### 比较 (PML1 单词式 / PML2 方法式)
```
EQ  / .EQ()     等于
NEQ / .NEQ()    不等于
LT  / .LT()     小于
LE  / .LEQ()    小于等于
GT  / .GT()     大于
GE  / .GEQ()    大于等于
```

```pml
if (!x.gt(5)) then ... endif
if (!x eq 5) then ... endif
```

### 逻辑
```
AND / .AND()
OR  / .OR()
NOT / .NOT()
```

```pml
if (!a.eq(1).and(!b.eq(2))) then ... endif
if (!a eq 1 and !b eq 2) then ... endif
```

### 算术
```pml
!x + !y    # 加
!x - !y    # 减
!x * !y    # 乘
!x / !y    # 除
```

### 字符串连接
```pml
!result = !a.string() & !b.string()
!result = |hello| & | world|    # → 'hello world'
```

### 字符串函数
```pml
!str.upcase()                  # 转大写
!str.downcase()                # 转小写
!str.length()                  # 长度
!str.substr(1, 3)             # 子串
!str.match(|*pattern*|)        # 通配符匹配
!str.replace(|old|, |new|)     # 替换
!str.trim()                    # 去首尾空格
!str.split()                   # 按空格分割为数组
!str.split(|,|)               # 按逗号分割
!arr.join(|,|)                # 数组用逗号连接
```

### 数学函数
```pml
abs(!x)  nint(!x)  sqrt(!x)  exp(!x)  log(!x)
sin(!x)  cos(!x)  tan(!x)  asin(!x)  acos(!x)  atan(!x)
mod(!x, !y)  max(!x, !y)  min(!x, !y)  round(!x)  random()
```

---

## 3. 条件

```pml
if (!x eq 5) then
    $p x is 5
elseif (!x eq 6) then
    $p x is 6
else
    $p x is something else
endif

# 三元式
!result = iftrue(!cond, !trueVal, !falseVal)
```

---

## 4. 循环 (DO ... ENDDO)

```pml
# 无限循环 + 条件退出
do
    if (!x.gt(5)) then break endif
    !x = !x + 1
enddo

# 带 BREAK IF
do
    break if (!x.gt(5))
    !x = !x + 1
enddo

# 自动计数 1→5
do !i to 5
    q var !i
enddo

# FROM TO
do !i from 5 to 10
    q var !i
enddo

# 带步长
do !i from 5 to 10 by 2
    q var !i
enddo

# 遍历数组索引
do !idx indices !arr
    q var !idx
enddo

# 遍历数组值
do !val values !arr
    q var !val
enddo

# 跳过
do !i from 1 to 10
    skip if (!i.eq(7))
    q var !i
enddo
```

---

## 5. 错误处理

```pml
new pipe /MyPipe
handle (41, 8)
    $p Error: Need to be at a ZONE or below
elsehandle (41, 12)
    $p Error: That name has already been used
elsehandle any
    $p Error: Another error has occurred
elsehandle none
    $p Everything OK. PIPE created
endhandle
```

错误号和严重度：`HANDLE (错误号, 严重度)` 匹配特定错误。`ELSEHANDLE ANY` 匹配任何错误。`ELSEHANDLE NONE` 在无错误时执行。

---

## 6. 跳转

```pml
golabel /MyLabel
label /MyLabel
```

---

## 7. 集合操作 (COLLECT / EVALUATE)

### COLLECT (收集元素)
```pml
var !sites collect all sites                              # 收集所有 SITE
var !zone collect all zone for ce                         # 从 CE 下收集所有 ZONE
var !zone collect all zone for /*                         # 从世界根收集
var !pipes collect all pipe with (odia gt 150) for ce    # 带条件过滤
```

### EVALUATE (提取属性)
```pml
var !names evaluate name for all zone from !zones        # 提取所有 ZONE 的 NAME
var !pos evaluate position for all zone from !zones      # 提取所有 ZONE 的 POSITION
var !calc evaluate (position[1] * 2) for all zone from !zones  # 表达式提取
var !filtered evaluate name for all zone with matchwild(name, |*AREA*|) from !zones  # 条件提取
```

### EVALUATE 用法（读 CE 属性）
```pml
var !result evaluate (!!ce.odia)     # 读取当前元素外径
var !result evaluate (!!ce.name)     # 读取当前元素名称
```

---

## 8. 元素操作

### 创建
```pml
new pipe /MyPipe              # 创建一个 PIPE
new equi /MyEqui              # 创建设备
new zone /MyZone              # 创建 ZONE
new elbow /MyElbow            # 创建 ELBOW
new valve /MyValve            # 创建 VALVE
new box /MyBox                # 创建 BOX
```

### 删除
```pml
/PIPE-101 delete              # 删除元素
delete /PIPE-101              # 同上
```

### 修改
```pml
!!ce.name = /NewName          # 重命名
!!ce.odia = 219.1             # 设置外径
!!ce.wall = 8.18              # 设置壁厚
!!ce.mtyp = |A106 Gr.B|      # 设置材质
!!ce.pres = 1.6               # 设置设计压力
!!ce.temp = 350               # 设置设计温度
!!ce.desc = |New Description| # 设置描述
```

### 复制/移动
```pml
copy /PIPE-101 to /ZONE-01   # 复制
move /PIPE-101 to /ZONE-02   # 移动
```

### 读取属性
```pml
q var !!ce.name               # 查询名称
q var !!ce.odia               # 查询外径
q var !!ce.wall               # 查询壁厚
q var !!ce.position           # 查询位置
q var !!ce.owner              # 查询父级
q var !!ce.members()          # 查询子元素列表
q var !!ce.type()             # 查询类型
q var !!ce.attributes()       # 查询所有属性名
```

### 层级导航
```pml
!!ce = /PIPE-101              # 导航到指定元素
!!ce = !!ce.owner             # 导航到父级
!!ce = !!ce.members()[1]      # 导航到第一个子元素
!!ce = /*                     # 导航到世界根
```

---

## 9. 数据库操作

```pml
savework                      # 保存到工作区
getwork                       # 获取最新保存版本
undo                          # 撤销
extract all pipe for /*      # 从数据库提取所有 PIPE
getchanges                    # 获取变更列表
commit                        # 提交
```

---

## 10. 几何操作

```pml
/PIPE-101 pos 500 500 500     # 设置绝对位置
/PIPE-101 by 100 0 0          # 相对移动 100mm East
q var !!ce.position           # 查询位置
q var !!ce.orientation        # 查询朝向
distance from /A to /B        # 距离测量
/PIPE-101 reverse             # 反转方向
connect /A to /B              # 连接
/A disconnect                 # 断开连接
```

---

## 11. 查询命令

```pml
q att odia of pipe            # 查询 PIPE 的 ODIA 属性定义
q type pipe                   # 查询 PIPE 类型定义
q spec /SpecName              # 查询规格
q ce                          # 查询当前元素
q mem                         # 查询当前元素子元素
q proj                        # 查询项目信息
q mdb                         # 查询 MDB 信息
q sess                        # 查询会话信息
list                          # 列出当前元素信息
list all                      # 列出所有属性
dump                          # 导出元素所有数据
```

---

## 12. 视图控制

```pml
zoom /PIPE-101                # 缩放到元素
fit                           # 适配视图
col /PIPE-101 red             # 设置颜色
add view /MyView              # 创建新视图
remove view /MyView           # 删除视图
graphics on                   # 打开图形
graphics off                  # 关闭图形
draw /PIPE-101                # 绘制元素
autodraw                      # 自动绘制
shaded                        # 着色模式
wire                          # 线框模式
```

---

## 13. 碰撞/检查

```pml
clash /A /B                   # 两元素碰撞检查
clash all pipe                # 所有 PIPE 碰撞检查
clash clear                   # 清除碰撞结果
check /PIPE-101               # 设计规则检查
```

---

## 14. 规格/目录

```pml
q spec                        # 查询所有规格
q spec /SpecName             # 查询特定规格
q cat /CatName               # 查询目录项
bom /PIPE-101                # 查询材料表
q scom /SpecName             # 查询规格组件
q spco                       # 查询规格组件数据
!!ce.spref = /SpecName       # 设置规格引用
```

---

## 15. 函数

```pml
# 无返回值 (过程)
define function !!MyFunc()
    q ce
    q mem
endfunction

# 带参数
define function !!Rename(!name is string)
    !!ce.name = !name
endfunction

# 有返回值
define function !!GetMem() is array
    return !!ce.mem
endfunction

define function !!BoxVolume() is real
    if (!!ce.type.eq(|BOX|)) then
        return (!!ce.xlen * !!ce.ylen * !!ce.zlen)
    endif
    return 0
endfunction

# 调用
!!MyFunc()
!!Rename(|/NewName|)
q var !!GetMem()
q var !!BoxVolume()
```

函数文件放在 `pmllib` 目录，扩展名 `.pmlfnc`，文件名必须匹配函数名。

---

## 16. 对象

```pml
define object MyObject
    member .name is string
    member .value is real
    member .items is array
endobject

define method .MyObject()
endmethod

define method .MyMethod()
    q var !this.name
endmethod

# 使用
!obj = object MyObject()
!obj.name = |Test|
!obj.value = 100
!obj.MyMethod()
```

对象文件放在 `pmllib` 目录，扩展名 `.pmlobj`。

---

## 17. 表单 (Forms)

### 基础框架
```pml
setup form !!MyForm
    !this.formTitle = |My Form|
    button .ok |OK| OK
    button .cancel |Cancel| at x20 CANCEL
exit

define method .MyForm()
    $p Constructor
endmethod
```

文件放在 `pmllib`，扩展名 `.pmlfrm`。加载：`pml reload form !!MyForm`。显示：`show !!MyForm`。

### 回调方法
```
.initCall        → init()     每次显示时调用
.firstShownCall  → firstShown()  首次显示时调用
.okCall          → okCall()      OK按钮点击
.cancelCall      → cancelCall()  Cancel按钮点击
.quitCall        → quitCall()    关闭按钮点击
.killingCall     → killCall()    销毁时调用
```

### 常用控件
```pml
button .b1 |Text| width 15                    # 按钮
button .b1 |Text| OK                          # OK按钮
button .b1 |Text| CANCEL                      # Cancel按钮
text .t1 |Label| width 30 is string           # 文本输入
text .t2 |Num| width 10 is real               # 数字输入
text .t3 |Pwd| width 10 NOE is string         # 密码输入
textpane .tp1 |Pad| width 40 height 10        # 多行文本
list .l1 |Title| width 20 height 10 SINGLE    # 单选列表
list .l2 |Title| width 20 height 10 MULTI     # 多选列表
option .op1 |Title| width 15                  # 下拉选项
combo .op2 |Title| width 15                   # 组合框
toggle .tg1 tagwid 15 |Title|                # 复选框
rgroup .rg1 |Title| FRAME horizontal          # 单选组
    add tag |OptA| select |A|
    add tag |OptB| select |B|
exit
slider .s1 horizontal range 0 100 step 5      # 滑块
numeric .n1 |Title| range 0 10 step 1         # 数字选择器
frame .f1 TABSET                              # 标签页框架
frame .f2 FOLDUP |Title|                     # 折叠面板
frame .f3 PANEL |Title|                      # 面板
view .v1 ALPHA height 15 width 60             # 文字视图
view .v2 VOLUME width 50 height 30            # 3D 体积视图
line .l1 vertical width 1 height 10           # 分隔线
```

---

## 18. 打印与调试

```pml
$p Hello World                 # 打印到控制台
$p |Value: | & !x.string()    # 打印变量
q var !x                       # 查询变量
q ce                           # 查询当前元素
q mem                          # 查询子元素列表
```

---

## 19. 宏与同义词

```pml
# 宏 ($M 执行)
$M/C:\Path\Macro.mac arg1 arg2

# 宏参数: $1 $2 $3 ... $9 (最多9个)
# 默认参数: $D1 = |default value|

# 同义词
$S缩写=完整命令 $S1 $S2
$SK                           # 清除所有同义词
```

---

## 20. 文件类型

| 扩展名 | 内容 |
|--------|------|
| `.pml` | 通用 PML 脚本 |
| `.pmlfnc` | 函数定义 |
| `.pmlobj` | 对象定义 |
| `.pmlfrm` | 表单定义 |
| `.pmlmac` / `.mac` | 宏 |
| `.pmlcmd` | 命令定义 |

---

## 21. 常用 E3D 元素类型

```
WORLD  SITE  ZONE  PIPE  BRAN  ELBO  TEE  REDU  VALV
FLAN  GASK  BOLT  NOZZ  EQUI  BOX  CYL  CONE  DISH
STRU  SUBE  PCOM  SPEC  SCOM  SPCO  PANE  PLAT  WALL
```

## 22. 常用属性名

```
NAME  TYPE  OWNER  POS  ORI  DIR  DESC  PURP  FUNC
ODIA  WALL  MTYP  PRES  TEMP  SBOR  SCHD  SPREF  BORE
RADIUS  ANGLE  LENGTH  HEIGHT  WIDTH  CONN  ARRIVE
LEAVE  HEAD  TAIL  CTYPE  STYPE  TTYP  DTSE  SPWL  MTYS
CREATED  MODIFIED  LOCKED  CLAIMED
```

## 23. 常见错误号

```
(41,8)   - Not at correct level (e.g., not at ZONE for NEW PIPE)
(41,12)  - Name already used
(41,6)   - Element not found
(41,15)  - Invalid attribute value
(41,18)  - Access denied / element locked
```
