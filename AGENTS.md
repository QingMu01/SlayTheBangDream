# AGENTS.md

Slay the Spire 2 模组项目：两个 C# 项目，使用 Godot.NET.Sdk/4.5.1 + Harmony

## 项目结构

```
BangDreamInSpire.sln
├── BangDreamLib/     — 共享前置库（依赖 游戏本体sts2.dll与STS2-RitsuLib）
└── ItsCrychic/      — 角色模组合集（依赖 BangDreamLib）
```

C# 代码位于各项目的 `Scripts/` 目录下，Godot 资源（图片、场景、本地化 JSON）位于以 mod-id 命名的子文件夹中（如
`BangDreamLib/BangDreamLib/`、`ItsCrychic/ItsCrychic/`）。

在编写新功能前，可先参考依赖库的文档。

- STS2-RitsuLib存在XML文档，可于Nuget包中找到
- sts2.dll提供同名XML文档，与sts2.dll位置一致

## 关键约定

- **命名空间**与 `Scripts/` 下目录结构完全对应
- **抽象类**以 `Abstract` 前缀命名
- 项目全局启用 `<Nullable>enable</Nullable>`、`<LangVersion>13.0</LangVersion>`、`<ImplicitUsings>true</ImplicitUsings>`
- 保持程序风格一致
- 执行修改任务时，先定制修改方案再进行修改工作
- 每次改动项目后，以表格的形式列举被修改的文件

## 本地化

- 当修改Card，Power或者Relic时需要同步修改其本地化文件。
- JSON 文件位于 `BangDreamLib/BangDreamLib/localization/zhs/` 和 `ItsCrychic/ItsCrychic/localization/zhs/`，是运行时加载的资源文件。
- `固有`、`消耗`、`虚无`等关键字由游戏自动添加说明，无需主动在本地化文件中添加。


## 游戏钩子

- 原版游戏提供`Hook`类（`MegaCrit.Sts2.Core.Hooks.Hook`）统一处理游戏事件
- `BangDreamLib` 通过`BangDreamHook`类处理一系列自定义 Hook 接口（在
  `Scripts/Interfaces/GameHook/` 下）供模组实现自定义游戏行为。

## 回复风格

全程使用简体中文，仅保留必要信息，去除礼貌用语等无关紧要的内容。