 **中文说明** | [English](README_EN.md)

# EverMedia

> **Emby Plugin**

### **持久化 .strm 文件的音视频与字幕信息, 支持自动修复。告别重复扫描，提升加载速度。**

![EverMediaLogo](https://raw.githubusercontent.com/Swiftfrog/swiftfrog.github.io/master/EverMediaLogo.png)

---

## 功能

| 功能 | 说明 |
|------|------|
|  **自动  备份** | 当 `.strm` 文件被扫描或更新时，自动将音视频流、字幕、时长、分辨率等信息保存到 `.medinfo` 文件中。 |
|  **自动  恢复** | 自动从 `.medinfo` 恢复，无需手动刷新。 |
|  **自动更新字幕** | **添加/删除字幕后，自动更新media info。无需手动。** |
|  **自定义存储** | 可将所有 `.medinfo` 文件统一存放在指定目录 |
|  **自定义设置** | 可独立启用/禁用/参数设置等，灵活控制资源消耗。 |
|  **多线程设置** | 配置线程数量和执行 FFProbe 间隔，避免高并发导致风控。 |
|  **增量扫描** | 计划任务只扫描“上次运行后修改过”的 `.strm` 文件，效率高。 |

> **适用场景**：  
> - 使用 `.strm` 播放网络视频（如 网盘、Plex 链接）
> - 媒体库中包含大量外链文件
> - 想要稳定、快速加载，不依赖 Emby 每次重扫
> - 网络源不稳定，频繁触发探测导致封控

---

## 安装说明

### 1. 下载插件
从 Releases 页面下载最新版：
```
EverMedia.dll
```

### 2. 安装到 Emby
```
EverMedia.dll 复制到 Emby下的config/plugins下
```
> **提示**：插件安装后默认**不自动运行**，需手动启用（见下文配置）。

---

## 配置说明

安装后，进入：  
**仪表盘 → 插件 → EverMedia**

### 🔧 基础设置

| 选项 | 说明 |
|------|------|
| **启用插件** (`EnablePlugin`) | ✅ 开启后，监听 `.strm` 文件，自动更新 `.medinfo`（推荐开启） |
| **启用计划任务** (`EnableBootstrapTask`) | ✅ 开启后，计划任务方可使用。执行扫库任务特别耗费资源，避免误操作，所以添加此开关。 |
| **备份模式** (`BackupMode`) | - `SideBySide`： `.medinfo` 与 `.strm` 同目录（默认）<br>- `Centralized`： `.medinfo` 统一存入指定目录。 |
| **上次任务运行时间UTC**|任务运行后，自动记录时间。非必要不操作。<br>如果想扫描某个时间段后添加的媒体项目或者重新扫描，可以根据时间自行设定。|
| **任务速率限制** (`BootstrapTaskRateLimitSeconds`) | 控制任务的线程数和访问频率，防止网盘/网站封控，请根据自己的情况自行定义。 |
| **多线程** (`MaxConcurrency`) | 根据主机性能和网盘风控情况自行调整 |
| **备份模式** (`BackupMode`) | - `SideBySide`： `.medinfo` 与 `.strm` 同目录（默认）<br>- `Centralized`： `.medinfo` 统一存入指定目录 |

> **建议配置**：  
> - 启用插件
> - 启用计划任务
> - 速率限制：`2`  
> - 并发数：`2`  
> - 备份模式：`SideBySide`
> - **首次安装建议手动执行一次计划任务**

---

## 文件结构示例

### 默认模式（SideBySide）
```
/Media/Movies/
├── MyMovie.strm
├── MyMovie.medinfo     ← 自动生成，存储元数据
└── MyMovie.srt         ← 字幕文件（自动识别）
```

### 中心化模式（Centralized）
```
/Media/Movies/
└── MyMovie.strm

/.evermedia/
└── Movies/
    └── MyMovie.medinfo     ← 统一存放，便于备份和迁移
```

> `.medinfo` 是**纯文本 JSON 文件**，可手动查看或备份，但**不要手动修改**。

---

## 使用场景示例

| 场景 | EverMedia 行为 |
|------|----------------|
| **添加一个新的 `.strm` 文件** | 自动探测 → 生成 `.medinfo` → 下次播放秒开 |
| **为 `.strm` 添加中文字幕** | 检测到“仅字幕变化” → 删除旧 `.medinfo` → 重新探测 → 保存含新字幕的新文件 |
| **Emby 数据库崩溃** | 重启后，计划任务自动扫描所有 `.strm` → 从 `.medinfo` 恢复所有元数据 |
| **网络源断开导致探测失败** | `.medinfo` 保留历史信息，播放仍可正常显示标题、时长、分辨率 |
| **手动删除 `.medinfo`** | 下次播放时，自动重新探测并重建 |

---

## ❓ 常见问题

### Q1：`.medinfo` 文件能删除吗？
> 可以删除。插件会在下次播放或扫描时自动重建。但**不建议手动修改**内容。

### Q2：插件会影响性能吗？
> **极低开销**。  
> - 事件监听：仅在 `.strm` 文件变化时触发；  
> - 计划任务：默认每天执行一次，或手动触发；  
> - FFProbe：仅在首次或字幕变更时执行，之后直接读 `.medinfo`。

### Q3：支持 Jellyfin 吗？
> 目前仅支持 **Emby 4.9.1.x**（基于 .NET 8）。Jellyfin 还没开发计划，暂不支持。

### Q4：为什么播放时还是慢？
> 请检查：
> - `.medinfo` 文件是否存在？
> - `.strm` 文件路径是否正确？
> - 是否启用了“启用插件”和“启用计划任务”？
> - 网络源是否可访问？

### Q5：如何手动触发扫描？
> 进入 **仪表盘 → 计划任务 → EverMedia Bootstrap Task → 点击“运行”**。

---

## 📜 开发者说明

- **项目地址**：https://github.com/Swiftfrog/EverMedia  
- **贡献**：欢迎提交 Issue / Pull Request  
- **许可证**：[![License: GPL v3](https://img.shields.io/badge/License-GPL%20v3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0) 任何基于本项目代码的分发（包括商业用途）**必须以相同许可证开源全部源代码**。
- **依赖**：Emby Server 4.9.1.80，.NET 8

---

## 💬 支持与反馈

如有问题、建议或想支持本项目：

- 在 GitHub 提交 [Issue](https://github.com/Swiftfrog/EverMedia/issues)  
- 给项目点个 ⭐️，让更多人受益！

> EverMedia —— 让你的媒体库，更智能、更稳定、更持久。
