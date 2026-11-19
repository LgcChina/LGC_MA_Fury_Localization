# LGC_MAFury_中文拓展 2.0.0

本次撤回首次 2.0.0 发行包，统一中文展示名称后以相同版本号重新发布。

MA 与 VRCFury 汉化正式合并发布。自动检测项目中已安装的插件，分别提供中文界面、停用还原和词典重载；使用者已在专用沙盒确认汉化生效。

- MA：保留字段、说明、组件标题和菜单汉化，最低适配目标 1.10.0；缺少词条或可选接口时跳过。
- VRCFury：扩展组件、动作、菜单、SPS 和选项显示；BlendShape 统一为“形态键”，组件标题保留英文便于搜索。
- 两份 JSON 词典独立维护，保留现有自定义译文；动态规则无效时只跳过该条，并在工具窗口显示原因。
- 原版 MA / VRCFury / NDMF 文件及项目运行资源名称保持原样。

## 安装与升级

在 VCC / ALCOM 添加或刷新订阅：https://lgcchina.github.io/LGC-VRC-Packages/index.json ，安装 **LGC_MAFury_中文拓展 2.0.0**。

包 ID：`com.lgcchina.ma-fury-localization`。也可将本 Release ZIP 内容解压至 `Packages/com.lgcchina.ma-fury-localization`。旧 1.0.0 发行版停止提供，合集仅收录新的统一包；安装或升级前请备份自定义词典。

Unity 顶部入口：**LGC > LGC_MAFury_中文拓展 v2.0.0**。需要 Unity 2022.3、VRChat SDK Base 提供的 Harmony，以及需要汉化的原插件；本包不会自动安装 MA 或 VRCFury。

## 注意事项

已安装此前同号发行包时需重新安装本次发行包。升级前另存包内自定义译文。修改 JSON 后点击对应模块的重新加载按钮，并重开组件检查器或菜单。MA 1.10.0 为适配目标，VRCFury 扩展依据 1.1460.0；未经实际检查的界面或版本可能保留英文。

本版由使用者手动检查效果；未运行 Unity 自动测试、未添加测试脚本。发行阶段执行包结构、版本和校验值检查。仓库更名为 `LgcChina/LGC_MA_Fury_Localization`，订阅地址继续使用原合集地址。
