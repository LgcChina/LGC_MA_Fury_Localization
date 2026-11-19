# LGC MA 简体中文扩展 v1.0.0

为 Modular Avatar 提供独立的简体中文界面扩展。在编辑器进程内接入原版界面，保留原始包文件，支持随时停用并恢复原版显示。

- 作者：[LgcChina](https://github.com/LgcChina)
- 工具仓库：https://github.com/LgcChina/LGC-MA-Localization
- VPM 包合集：https://github.com/LgcChina/LGC-VRC-Packages
- VPM 订阅地址：https://lgcchina.github.io/LGC-VRC-Packages/index.json
- 窗口入口：**Unity 顶部 LGC > MA 汉化 v1.0.0**

## 安装

1. 在 VCC / ALCOM 中添加上方 VPM 订阅地址。
2. 在已安装 Modular Avatar 的 Avatar 项目中安装 **LGC MA 简体中文扩展**。
3. 等待 Unity 导入、编译，重新选中对象并重开组件菜单或窗口。
4. 从顶部 LGC 菜单打开汉化窗口，可启用中文、停用并恢复原版显示、重新加载词典。

也可从本仓库 Release 下载 ZIP，将其内容解压到项目的 `Packages/com.lgcchina.ma-localization-probe`。ZIP 根目录直接包含 `package.json` 和 `Editor`。
从沙盒版本升级时继续使用同一包 ID，无需更换脚本、词典文件名或 GUID。已有手工译文请先另存副本，包管理器升级可能覆盖包内修改。

原版 Modular Avatar 的 VPM 源：https://vpm.nadena.dev/vpm.json 。如包管理器找不到 MA，请先添加其官方源。

## 功能

- 翻译原版字段、说明、提示、警告及错误信息。
- 为添加组件列表、中文搜索、组件 Inspector 标题和上下文菜单提供中文或中英对照显示。
- 补充原代码里直接写出的界面文字和部分 UI Toolkit 标签。
- 翻译 MA Menu Item 的菜单类型下拉项，保留原始枚举索引。
- BlendShape 的中文名称统一为“形态键”；组件括号内保留英文名称，方便识别与搜索。
- 词典由独立 JSON 提供，可在窗口中手动重新加载。
- 旧版不存在的词条、组件、菜单或可选接口分别跳过，保留对应原版行为。

## 环境与兼容范围

- Unity **2022.3**，在 VRChat Avatar SDK 项目中使用；本包复用 SDK 提供的 `0Harmony.dll`，不另附 DLL。
- MA 最低适配目标为 **1.10.0**，依赖声明为 `>=1.10.0`；不锁定 NDMF 版本。
- 使用者已反馈沙盒中的汉化应用和词典重载成功；此前 MA 1.15.0 的有限项检查成功。最低目标版本与其他版本的完整覆盖仍需实际确认。
- 安装到低于 1.10.0、版本无法识别或内部接口不同的 MA 时，按可接入范围跳过，不生成旧版没有的功能。
- 包内没有场景自动添加组件、修改预制件或改写原插件源码的逻辑。

## 修改译文

词典位置：`Editor/Localization/zh-Hans.json`。

| 分类 | 内容 |
| --- | --- |
| `entries` | 原版本地化键对应的字段、说明、提示与警告 |
| `components` | 添加组件选项与组件标题 |
| `menus` | 上下文菜单名称 |
| `literals` / `uiLiterals` | 额外界面文字 |
| `controls` | 菜单控件类型名称 |

修改相应译文，保留键、匹配原文、类型名、路径与格式占位符。保存后打开 `LGC > MA 汉化 v1.0.0`，点击 **重新加载词典并连接 MA**，再重新选中对象并重开菜单或窗口。
仅保存 JSON 不会自动重载。读取或解析失败时，窗口显示原因并保留当前已加载的词典与启用状态；停用状态下重载会保留停用状态。
同一概念可能有多个显示来源，例如 `control_type.RadialPuppet` 和 `controls` 中的 `Radial Puppet`，希望各处措辞一致时需要同步修改。

## 还原与卸载

在窗口中点击 **停用并恢复原版显示**，然后重新打开相关界面。
通过 VCC / ALCOM 安装的包可在包管理器中移除；手动解压安装的包可在停用后删除 `Packages/com.lgcchina.ma-localization-probe`。
原版 MA 与 NDMF 无需重装。退出编辑器时会撤销本包的补丁；启用选择保存在当前编辑器会话，重新启动默认启用。

## 容易踩的坑

- 已打开的组件菜单、Inspector 或窗口可能缓存旧文字，修改译文后请重开相关界面。
- 顶部原生 GameObject / Tools / Component 菜单的原始命令仍可能显示英文；本包的菜单翻译覆盖添加组件弹窗和上下文显示。
- 本包强制覆盖接入的 MA 显示文字，保留 NDMF 的语言偏好。
- 用户填写的参数名、对象名、实际形态键名、动画层名和资源路径保持原样。
- 原版内部接口变化可能影响部分显示适配，未匹配的界面保留原文。
- 按使用者要求没有运行 Unity 自动测试或提供测试脚本，界面效果由使用者手动确认。

## 发布维护

版本 1.0.0 同步记录于包元数据、窗口、菜单、脚本注释、README 和 CHANGELOG。
包 ID 沿用 `com.lgcchina.ma-localization-probe`，程序集名、文件名、翻译资源路径、Harmony ID 和会话键保持固定。

维护者更新上述版本、译文及修改日志后，提交并推送 `v版本号` 标签。GitHub Actions 会使用 `scripts/package.py` 打包并创建 Release，附带 VPM ZIP、`package.json` 与 SHA-256。随后在包合集仓库运行 **Build Repo Listing** 以刷新在线索引。
不应删除已发布版本的资源，以便已有项目继续安装对应版本。

## 许可与来源

本工具以 MIT 许可发布。中文词典和组件命名整理自使用者提供的 MA 1.19.0 汉化补丁，并结合本工具调整；原项目许可见 `THIRD_PARTY_NOTICES.md`。不包含原项目图片、图标或 Harmony DLL。
