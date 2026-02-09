# Multi-Path Industrial Processor

## 中文

**多路工业加工站**是一座可建造的农场建筑，通过“空间化多通道投放”把多种原版加工器整合到一台设备中，并将产物统一汇总到同一个出口。

### 核心玩法 / 设计思路

- **空间语义**：玩家站在不同的投放口位置进行投放，位置 = 加工语义（不做复杂 UI 选择）。
- **模块化**：v1 固定 6 个模块，每个模块独立处理与并行。
- **容量 = 并行数**：容量表示“同时进行的加工任务数量”，v1 不引入显式队列；并行满则拒绝输入。
- **统一产物出口**：所有模块产物进入同一个输出口，使用原版箱子界面收取。
- **煤炭燃料池**：煤炭通过独立煤口进入“全局燃料池”，仅冶炼模块消耗；燃料不足会暂停冶炼。

### 端口布局（v1）

交互以**玩家站位**为准（也兼容玩家面前一格的交互方式）：

- **下方**：6 个输入口（模块）
  - 奶/蛋（蛋黄酱机、压奶酪）
  - 酿酒（木桶）
  - 腌制（腌菜罐）
  - 冶炼（矿/石英 → 锭/精炼石英）
  - 羊毛（织布机）
  - 制油（榨油机）
- **上方**：统一输出口（原版箱子 UI）
- **左侧**：煤口（只接收煤炭，加入燃料池）
- **右侧**：管理终端（规划中）

### 当前进度（WIP）

- 已有：可建造建筑、端口交互、统一输出箱子、基础冶炼燃料池、模块并行（默认 8）。
- 待做：管理终端 UI、模块升级（8→16→32→64）与消耗、更多规则/配方的可配置化、输出口满时策略等。

---

## English

**Multi-Path Industrial Processor** is a buildable farm building that merges multiple vanilla processors into one station using **spatial multi-port input** and a **unified output**.

### Core Design

- **Spatial semantics**: where you stand determines the processing mode (no complex UI selection for daily use).
- **Modules**: v1 ships with 6 fixed modules; each module runs independently.
- **Capacity = parallelism**: capacity means “how many jobs can run at the same time”; v1 has no explicit queue (full => reject input).
- **Unified output**: all products go to a single output, collected via the vanilla chest menu.
- **Global fuel pool**: coal goes into a shared fuel pool via a dedicated coal port; only smelting consumes fuel and pauses when fuel is insufficient.

### Port Layout (v1)

Interactions are based on the **player tile** (also supports the tile in front of the player):

- **Bottom**: 6 input ports (modules)
  - Animal (mayo/cheese)
  - Brewing (keg)
  - Preserves (jar)
  - Smelting (ore/quartz → bars/refined quartz)
  - Wool (loom)
  - Oil (oil maker)
- **Top**: unified output (vanilla chest UI)
- **Left**: coal port (coal only, adds to fuel pool)
- **Right**: management terminal (planned)

### Status (WIP)

- Working: buildable building, port interactions, unified output chest, basic smelting fuel pool, module parallelism (default 8).
- Planned: management terminal UI, module upgrades (8→16→32→64) + costs, configurable rules/recipes, output-full behavior, etc.
