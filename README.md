# Stardew Valley（星露谷物语）SMAPI Mod 开发环境（模板）

这个仓库是一个最小可用的 **C# / SMAPI** Mod 开发模板，包含：

- 本地安装的 `.NET 6 SDK`（下载安装到本仓库的 `.dotnet/`，不污染系统环境）
- 一个可编译的 Mod 工程骨架（`net6.0` + `Pathoschild.Stardew.ModBuildConfig`）
- `stardewvalley.targets` 示例（用于在非默认安装路径下指定游戏目录）

## 1) 前置条件

- 你本机已安装 **Stardew Valley** 与 **SMAPI**
- 你能访问外网（用于下载 .NET SDK 与 NuGet 依赖）

## 2) 安装 .NET 6（本地到仓库）

在仓库根目录执行：

```bash
./scripts/install-dotnet.sh
```

之后你可以用 `./dotnetw` 代替系统的 `dotnet`。

## 3) 配置游戏路径（如果自动识别失败）

当构建时报 “Failed to find game install path” 或找不到 `StardewModdingAPI` 等引用时，创建 `stardewvalley.targets`：

```bash
cp stardewvalley.targets.example stardewvalley.targets
```

然后把 `PATH_HERE` 改成你的游戏目录（包含可执行文件的那个文件夹，例如 `Stardew Valley.exe` / `StardewValley` 所在目录）。

## 4) 还原与构建

```bash
./scripts/doctor.sh
./scripts/restore.sh
./scripts/build.sh
```

如果 `ModBuildConfig` 找到了游戏目录，它会在构建时把输出自动复制到你的 `Mods/` 下（默认行为）。

如果你在运行游戏/SMAPI 时构建，Windows 可能会锁定已加载的 `*.dll`，导致复制失败（Access denied）。此时：

- 先关闭游戏/SMAPI，再运行 `./scripts/build.sh`；或
- 使用 `./scripts/build-nodeploy.sh` 仅编译，不自动复制到 `Mods/`。

## 5) 下一步

- 修改 `manifest.json` 里的 `UniqueID`、`Name`、`Author`。
- 在 `ModEntry` 里写你的功能逻辑。

---

参考：
- Stardew Valley Wiki：建议目标框架 `.NET 6`，并推荐使用 `Pathoschild.Stardew.ModBuildConfig`。  
  https://stardewvalleywiki.com/Modding:Modder_Guide/Get_Started
