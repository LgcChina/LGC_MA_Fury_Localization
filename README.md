# LGC_MA_Fury_Localization v2.0.0

一个包管理 Modular Avatar 与 VRCFury 的简体中文界面。自动检测已安装插件，分别启用翻译；未安装的插件跳过，不自动安装 MA 或 VRCFury。可以分别停用还原与重新加载词典，原版插件文件保持原样。

- 作者：[LgcChina](https://github.com/LgcChina)
- 仓库：https://github.com/LgcChina/LGC_MA_Fury_Localization
- 当前状态：2.0.0 正式版，MA 与 VRCFury 合并发布；沙盒汉化效果已由使用者手动确认。
- VPM 合集：https://github.com/LgcChina/LGC-VRC-Packages
- VPM 订阅：https://lgcchina.github.io/LGC-VRC-Packages/index.json
- 包 ID：`com.lgcchina.ma-fury-localization`
- 窗口入口：**Unity 顶部 LGC > LGC_MA_Fury_Localization v2.0.0**

## 安装

推荐通过 VCC / ALCOM 安装：

1. 在 VCC / ALCOM 添加上方 VPM 订阅地址。
2. 在 Unity 2022.3 的 VRChat 项目中安装 **LGC_MA_Fury_Localization**。
3. 等待 Unity 导入、编译，重新选中对象并重开菜单或 Inspector。
4. 从顶部 LGC 菜单打开管理窗口，查看每个插件的检测结果与汉化状态。

本包只声明 VRChat SDK Base 依赖，以复用 SDK 提供的 Harmony DLL。MA 和 VRCFury 都是可选插件，不会因安装汉化包而自动安装。
也可将 Release ZIP 的内容解压到项目 `Packages/com.lgcchina.ma-fury-localization`；ZIP 根目录直接包含 `package.json` 和 `Editor`。

## 自动检测与独立控制

| 项目中的插件 | 汉化行为 |
| --- | --- |
| 只有 MA | 接入 MA，跳过 VRCFury |
| 只有 VRCFury | 接入 VRCFury，跳过 MA |
| 两者都有 | 分别接入，独立停用、还原与重载 |
| 两者都没有 | 保持待用，不报缺少插件错误 |

插件安装或移除后，Unity 重新编译会重新检测，也可在窗口点击“重新检测已安装插件”。某个模块的词典或界面接口异常只影响该模块；缺少的词条、组件或可选入口按对应功能跳过。

## MA 汉化范围

- 保留已有完整词典、字段、说明、警告和错误提示翻译。
- 保留添加组件列表、中文搜索、组件标题、上下文菜单和部分直接显示文字的适配。
- 保留 MA Menu Item 控件类型下拉翻译，不改变枚举值。
- BlendShape 的中文术语统一为“形态键”，组件括号内可保留英文名称。
- 最低适配目标 **MA 1.10.0**；缺少的可选接口独立跳过。此前 MA 1.15.0 的有限项检查成功，完整旧版覆盖仍待实际确认。

## VRCFury 汉化范围

1.2.0 基于本地 **VRCFury 1.1460.0** 扩展编辑器汉化，包含：

- 功能组件：骨架联动、形态键联动、开关、完整控制器、手势、口型、眨眼、菜单调整、约束和优化等。
- 动作编辑器：动画片段、对象开关、形态键、材质属性与替换、缩放、物理骨骼重置、循环动画、SPS 动作等。
- SPS：插入端、接口、触碰区域、触碰发送器、全局碰撞体、通道、引导路径、深度／半径动画、旧版兼容与 SPS 选项。
- 完整控制器中的面部追踪分区、公共列表操作、说明、提示、部分调试信息和原版对话框。
- 添加组件列表与中文搜索、组件标题、VRCFury 创建的右键／动作菜单，以及已适配枚举和固定选项的显示。

组件与动作名称使用“中文 (English)”便于搜索和对照教程。可在添加组件窗口搜索 Armature Link、Toggle、Full Controller、Gestures、SPS Plug 或 SPS Socket，添加后重新选中对象查看。
下拉选项仅通过显示格式化翻译，原始 choices、枚举索引、绑定路径和事件回调保留。不会翻译用户输入值、参数名、对象引用或实际形态键名。
动态提示使用整句模板，保留插入的对象名、数值和路径。新增动态内容通常在可见界面半秒内更新；已打开的菜单和组件窗口需要重开。

此范围说明记录本次代码适配与词典内容，不代表全部界面均已在 Unity 中验证。未匹配的文字、新版 SDK 自己绘制的嵌套界面、底层异常详情及部分原生顶部菜单可能仍为英文。VRCFury 旧版缺少可选接口时跳过，不承诺未经检查的最低版本。

## 两份独立词典

| 文件 | 用途 |
| --- | --- |
| `Editor/Localization/ma.zh-Hans.json` | MA 词条、组件、菜单和额外界面文字 |
| `Editor/Localization/vrcfury.zh-Hans.json` | VRCFury 组件、动作、菜单、SPS 与选项显示 |

MA 使用本地化加载器与显示适配；VRCFury 使用限定组件范围的界面适配。两者共用工具窗口，保留各自的接入逻辑与词典。
修改对应的译文值，保留键、匹配原文、类型名、路径和格式占位符。保存后在窗口点击对应模块的 **重新加载词典**，再重新选中对象并重开相关界面。
VRCFury 词典内 `entries` 是精确原文与译文；`titles` 是保留英文的组件／动作标题；`enumEntries` 仅供选项显示；`patterns` 是整句动态模板，必须保留相同的命名占位符，例如 `{index}`、`{name}`；`choiceSets` 是允许翻译的固定选项集合；`componentTitles` 将组件完整类型名映射到标题。修改译文即可，保留原文键、类型名与选项集合。未知段落会被旧版工具忽略，但 1.2.0 的完整功能需要同版脚本。

1.2.2 修复动态占位符导致 VRCFury 词典整体加载失败的问题。占位符可包含点号、括号等表达式文字，内部使用独立的安全分组名称。单条动态规则不合法时，仅跳过该条并在窗口详情中记录原文与原因，其他有效词条继续加载。

仅保存 JSON 不会自动重载。读取或解析失败时保留该模块已加载的词典和显示状态，不影响另一个模块；停用时重载仍保持停用。
MA 中同一概念可能有多个显示来源，例如 `control_type.RadialPuppet` 与 `controls` 中的 `Radial Puppet`，希望各处一致时需要同步修改。
升级前请另存自定义译文，包管理器会覆盖包内文件。

## 包标识与更新

正式包展示名称及仓库均为 `LGC_MA_Fury_Localization`，包 ID 为 `com.lgcchina.ma-fury-localization`。旧的 1.0.0 发行版停止提供，合集只收录本统一包的正式发行版。
包 ID、程序集名称、两份词典路径及资源 GUID 采用固定名称；后续升级前请备份包内自定义译文。

## 还原与卸载

在管理窗口分别点击 **停用 MA 并恢复原版显示** 或 **停用 VRCFury 并恢复原版显示**，重开相关界面。
通过 VCC / ALCOM 安装的包在包管理器中移除；手动安装的包停用后删除 `Packages/com.lgcchina.ma-fury-localization`。
原版 MA、VRCFury 与 NDMF 无需重装。退出编辑器或程序集重载时只撤销本包自己的补丁。
启用选择保存在当前编辑器会话，重新启动默认启用已检测到的插件汉化。

## 限制与注意事项

- Unity 2022.3 / VRChat SDK 环境；SDK Base 需提供 `0Harmony.dll`，本包不附带或改写 DLL。
- MA 低于 1.10.0 或版本无法识别时跳过；不锁定 NDMF 版本。
- 不直接引用 MA 或 VRCFury 程序集，未安装其中之一不会因缺少该插件程序集而产生编译引用错误。
- 顶部原生 GameObject / Tools / Component 的原版命令仍可能显示英文，MA 菜单适配主要覆盖添加组件弹窗和上下文菜单。
- 用户输入、实际形态键名、对象名、参数、动画与资源路径保持原样。
- 新增或变化的原版内部接口可能导致部分翻译跳过，不代表该版本所有界面均已验证。
- 使用者已在专用沙盒手动确认 MA 与 VRCFury 汉化生效；未运行 Unity 自动测试、未添加测试脚本，未验证每个界面与插件版本。

## 发布维护与许可

版本 **2.0.0** 同步记录在包元数据、窗口、菜单、脚本注释、README 与 CHANGELOG。包 ID、程序集名和两份词典路径使用固定命名，后续版本号只用于工具版本和记录。
发行包只包含工具脚本、词典、元数据和文档。更新版本、译文和修改日志后，推送 `v版本号` 标签；GitHub Actions 使用 `scripts/package.py` 生成 VPM ZIP、清单和校验值并创建 Release。随后在合集运行 **Build Repo Listing** 刷新在线索引。

本工具以 MIT 许可发布。MA 词典和组件命名整理自使用者提供的 MA 1.19.0 汉化补丁；VRCFury 中文依据原界面文字编写。来源说明见 `THIRD_PARTY_NOTICES.md`，不包含原版图片、图标或 Harmony DLL。
