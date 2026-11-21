# LGC_VRCMAFury_中文拓展 v2.3.7

## 2.3.7：世界 SDK

依据 MAP/ndmz 中的 VRChat Worlds SDK 3.10.6 补充世界上传页及组件汉化。覆盖层配置 / 碰撞矩阵、出生点、空间音频、UI 超采样、贴图过滤、物理骨骼 / 接触数量、世界容量与调试说明、社区实验室发布提示，以及世界组件的中英文标题、添加组件菜单、字段和悬停提示。

世界组件使用独立的 UI Toolkit 检查器适配，控件完成绑定后继续更新显示；旧版可选接口缺失时跳过。保留用户输入、对象名、层名、枚举值、绑定路径、网络 ID、蓝图 ID、验证记录和自动修复回调。停用 SDK 汉化后恢复记录的显示；必要时重新选中对象或重开 SDK 面板。本次发布前只核对包结构、词典和版本，未运行 Unity、编译或测试脚本。

安装或升级：在 VCC / ALCOM 刷新原订阅，将本包更新至 2.3.7；也可将 Release ZIP 解压至 `Packages/com.lgcchina.ma-fury-localization`。自行修改的 JSON 请先备份，包更新可能覆盖它们。

为 VRChat SDK、Modular Avatar 和 VRCFury 提供独立的简体中文编辑器界面。自动检测已安装插件，分别启用汉化、停用还原与重载词典；未安装的插件会跳过，不会自动安装 MA 或 VRCFury。

- 作者：[LgcChina](https://github.com/LgcChina)+CODEX编写，创意来源<img width="304" height="146" alt="image" src="https://github.com/user-attachments/assets/1cb383af-043e-42ac-af6e-36760c4576ae" />

- 仓库：https://github.com/LgcChina/LGC_MA_Fury_Localization
- VPM 订阅：https://lgcchina.github.io/LGC-VRC-Packages/index.json
- 包合集：https://github.com/LgcChina/LGC-VRC-Packages
- 包 ID：`com.lgcchina.ma-fury-localization`
- Unity 入口：**LGC > LGC_VRCMAFury_中文拓展 v2.3.7**

## 安装与旧版升级

在 VCC / ALCOM 添加或刷新上述订阅，安装或更新 **LGC_VRCMAFury_中文拓展 2.3.7**。已安装 2.0.0 的用户直接点击更新即可，包 ID、仓库地址和订阅地址沿用原值。

Unity 自行完成导入后，重新打开管理窗口或 SDK 面板、重新选中组件。也可把 Release 中的 ZIP 内容解压至项目 `Packages/com.lgcchina.ma-fury-localization`，根目录应直接包含 `package.json` 和 `Editor`，避免重复嵌套文件夹。

更新前请另存自己修改的包内 JSON 词典，包管理器更新会替换包文件。包内配置键、程序集名、命名空间、词典路径及已有 meta GUID 保持稳定；人物参数、动画、预制件和资源名称不随工具改名。

## 2.3.7 更新内容

本次补充世界 SDK 上传页及组件，并沿用 **LGC_VRCMAFury_中文拓展** 展示名称。原 2.0.0 的 MA 与 VRCFury 适配继续保留，VRCFury 词条依据项目内 1.1485.0 补充。

| 模块 | 主要范围 |
| --- | --- |
| VRChat SDK | 登录页面、人物 / 世界上传页面、设置页、常用世界组件与人物描述器；内容管理仅翻译“世界 / 人物”两个按钮 |
| Modular Avatar | 字段、说明、组件标题、添加组件列表、上下文菜单及已适配的选项显示 |
| VRCFury | 组件、动作、菜单、SPS、提示、弹窗及已适配的枚举和固定选项 |

原版插件文件保持原样。每个模块独立接入；缺少插件、词条或可选方法时跳过，并在工具窗口记录可用状态。BlendShape 统一使用“形态键”，用户实际形态键名称保留原值。

## SDK 界面与上传页

SDK 模块包含 VRCHeadChop、常用组件、人物描述器、Pipeline Manager 与 Preview Override 等已适配检查器。人物描述器标题保留“人物描述器（VRC Avatar Descriptor）”，可播放层保留中英文，例如“特效（FX）”；Pipeline Manager 在人物项目展示为“人物蓝图ID”，在世界项目展示为“世界蓝图ID”。

上传页面覆盖内容准备、可见性 / 风格 / 内容警告、缩略图、检查提示、保存 / 放弃修改和上传状态。根据项目内 SDK 3.10.6 的 C# 源码与 UXML 补充未触发分支，包括骨架 / 动画层 / 表情参数、贴图与材质、PhysBone / 接触 / 约束 / 粒子性能限制、蓝图归属与权限、大小限制、审核、取消与失败，以及缩略图真实性确认、平台切换和多平台进度。

面部追踪包含已适配字段、目标 / 输入 / 映射警告及自定义表达式校验。账号、密码、验证码、上传数据、SDK 校验记录、下拉实际值、资源选择器和原有回调保持原值。顶部 Create an Avatar 属于 SDK 图片，未替换；未知服务端或第三方构建工具产生的消息可能保留英文。

## 词典与重载

| 文件 | 用途 |
| --- | --- |
| `Editor/Localization/ma.zh-Hans.json` | MA 词条、组件、菜单和额外界面文字 |
| `Editor/Localization/vrcfury.zh-Hans.json` | VRCFury 组件、动作、SPS、选项与动态提示 |
| `Editor/Localization/vrcsdk.zh-Hans.json` | SDK 世界 / 人物组件、登录、上传、设置及显示模板 |

保存 JSON 后点击对应模块的词典重载按钮，重新选中组件或重开窗口 / 菜单。读取失败保留上一份有效词典；未知词条保留原文。

SDK 词典中 `entries` 用于组件，`accountEntries` 用于登录及设置等界面，`componentEnums` / `componentTitles` / `componentMenus` 用于组件显示。上传页的整句为 `panelEntries`，动态消息为 `panelTemplates`，已知 SDK 解释片段为 `panelFragments`。

`worldEntries` 优先处理世界组件专用词义，例如将世界放置区域的 Size 显示为“尺寸”，不影响人物组件中的“数量”。

模板优先匹配固定文字较长的完整消息。请保留原文空格、换行和占位符编号；普通 `{0}` 等占位符复制动态内容，`:panel`、`:fields`、`:bones`、`:details`、`:face` 仅用于明确属于 SDK 的解释片段或结构，不要用于用户填写的内容。递归处理有深度限制。

## 兼容性与还原

- 需要 Unity 2022.3 与 VRChat SDK Base 提供的 `0Harmony.dll`；发行包不附带或改写 DLL。
- MA 最低适配目标为 1.10.0，低于该版本或版本无法识别时跳过；可选接口按实际存在情况处理，不锁定 NDMF 版本。
- VRCFury 补译依据 1.1485.0，SDK 补译依据 3.10.6；这不代表所有历史版本或条件分支均已在 Unity 中验证。
- 用户输入、对象引用、实际形态键 / 参数 / 动画 / 资源路径保持原样。部分原生顶部菜单、图片文字与未知动态文本仍可能为英文。
- 在管理窗口停用对应模块可撤销本包补丁并恢复已记录的显示；已打开的窗口或菜单可重开。编辑器退出或程序集重载时撤销本包自己的补丁。

通过 VCC / ALCOM 安装的包可在包管理器中卸载；手动安装的包停用后移除上述固定包目录。原 MA、VRCFury、NDMF 与 SDK 无需重装。工具启用选择保存在当前编辑器会话。

此前汉化效果由使用者在专用沙盒中手动检查；发行阶段只核对文件结构、词典、版本、GUID、ZIP 与在线索引，未启动 Unity、编译或运行测试，也未创建测试脚本。

## 发布维护与许可

版本 2.3.7 同步记录于包元数据、窗口、菜单、脚本注释和文档。运行资源使用稳定名称；版本号只用于工具版本、展示与修改日志。发行构建由 `scripts/package.py` 生成 VPM ZIP、包清单和 SHA-256，再由 GitHub Actions 发布 Release；合集使用现有来源配置刷新在线索引。

本工具以 MIT 许可发布。MA 词典及组件命名整理自使用者提供的 MA 1.19.0 汉化补丁；VRCFury 与 SDK 译文依据界面文字编写，不分发原版实现、图片、图标或 Harmony DLL。来源与许可见 `THIRD_PARTY_NOTICES.md`。
