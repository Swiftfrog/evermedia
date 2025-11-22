[中文说明](README_CN.md) | **English**

# EverMedia

> **Emby Plugin**

### **Persist `.strm` metadata, support auto-repair, and accelerate playback speed.**

![EverMediaLogo](https://raw.githubusercontent.com/Swiftfrog/swiftfrog.github.io/master/EverMediaLogo.png)

-----

## Features

| Feature | Description |
| :--- | :--- |
| **Auto Backup** | Automatically saves audio/video streams, subtitles, duration, and resolution to a `.medinfo` file when a `.strm` file is scanned or updated. |
| **Self-Healing** | Automatically restores metadata from `.medinfo` without requiring a manual refresh. |
| **Subtitle Sync** | Automatically updates media info when subtitles are added or removed. |
| **Centralized Storage** | Supports storing all `.medinfo` files in a single, specified directory. |
| **Custom Control** | Flexible settings to enable/disable features and manage resource consumption. |
| **Anti-Ban Logic** | Configurable thread counts and FFProbe intervals to prevent triggering API rate limits (risk control). |
| **Incremental Scan** | Scheduled tasks only scan `.strm` files modified since the last run for high efficiency. |

> **Ideal Scenarios**:
>
>   * Playing network videos via `.strm` (e.g., Cloud Drives, Plex links).
>   * Libraries containing a large number of external links.
>   * Users requiring stable, fast loading without relying on Emby's frequent re-scanning.
>   * Unstable network sources where frequent probing leads to bans.

-----

## Installation

### 1\. Download

Download the latest release:

```
EverMedia.dll
```

### 2\. Install to Emby

```
Copy EverMedia.dll to the Emby /programdata/plugins folder
```

> **Note**: The plugin is **not active** by default after installation. You must manually enable it in the settings (see below).

-----

## Configuration

Go to: **Dashboard → Plugins → EverMedia**

### 🔧 Basic Settings

| Option | Description |
| :--- | :--- |
| **Enable Plugin** | ✅ **Required.** Monitors `.strm` files and updates `.medinfo` automatically. |
| **Enable Task** | ✅ **Recommended.** Allows the background scheduled task to run. (Disabled by default to prevent accidental resource usage). |
| **Backup Mode** | - `SideBySide`: `.medinfo` is stored next to the `.strm` file (Default).<br>- `Centralized`: `.medinfo` is stored in a unified directory. |
| **Last Run Time (UTC)** | Automatically records the last task time. Modify this only if you need to force a re-scan from a specific date. |
| **Rate Limit** | Controls the interval between tasks to prevent cloud/site bans. Adjust based on your provider. |
| **Max Concurrency** | Adjust thread count based on server performance and provider risk controls. |

> **Recommended Setup**:
>
>   * Enable Plugin: On
>   * Enable Task: On
>   * Rate Limit: `2`
>   * Concurrency: `2`
>   * Backup Mode: `SideBySide`
>   * **Tip:** Manually run the scheduled task once after the first installation.

-----

## File Structure

### Default Mode (SideBySide)

```
/Media/Movies/
├── MyMovie.strm
├── MyMovie.medinfo     ← Auto-generated metadata
└── MyMovie.srt         ← Subtitle file (Auto-detected)
```

### Centralized Mode

```
/Media/Movies/
└── MyMovie.strm

/.evermedia/
└── Movies/
    └── MyMovie.medinfo     ← Unified storage for easier backup/migration
```

> `.medinfo` files are **plain text JSON**. You can view or backup them, but **do not manually modify** the content.

-----

## Usage Examples

| Scenario | EverMedia Action |
| :--- | :--- |
| **Add new `.strm`** | Auto-detects → Generates `.medinfo` → Instant playback next time. |
| **Add Chinese Subtitle** | Detects "subtitle only" change → Deletes old info → Re-probes → Saves new file with subtitle data. |
| **Emby DB Crash** | After restart, the scheduled task scans `.strm` files → Restores all metadata from `.medinfo` instantly. |
| **Network Source Down** | `.medinfo` retains history; titles, duration, and resolution display correctly even if the source is offline. |
| **Delete `.medinfo`** | The file will be automatically re-probed and rebuilt upon the next playback or scan. |

-----

## ❓ FAQ

### Q1: Can I delete `.medinfo` files?

> Yes. The plugin will automatically rebuild them during the next playback or scan. However, **manual modification** of the file content is not recommended.

### Q2: Does this affect performance?

> **Minimal overhead.**
>
>   * **Event Listeners:** Only trigger when `.strm` files change.
>   * **Scheduled Task:** Runs daily (default) or manually.
>   * **FFProbe:** Only runs on first add or subtitle changes; subsequent reads come directly from the text file.

### Q3: Is Jellyfin supported?

> Currently only supports **Emby 4.9.1.x** (based on .NET 8). There are no immediate plans for Jellyfin.

### Q4: Why is playback still slow?

> Please check:
>
>   * Does the `.medinfo` file exist?
>   * Is the `.strm` path correct?
>   * Are "Enable Plugin" and "Enable Task" turned on?
>   * Is the network source actually accessible?

### Q5: How do I manually trigger a scan?

> Go to **Dashboard → Scheduled Tasks → EverMedia Bootstrap Task → Click "Play"**.

-----

## 📜 Developer Info

  * **Repo**: [https://github.com/Swiftfrog/EverMedia](https://github.com/Swiftfrog/EverMedia)
  * **Contribution**: Issues and Pull Requests are welcome.
  * **License**: [![License: GPL v3](https://img.shields.io/badge/License-GPL%20v3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0) Any distribution based on this code (including commercial use) must open source the full code under the same license.
  * **Dependencies**: Emby Server 4.9.1.90, .NET 8

-----

## 💬 Support & Feedback

If you have questions, suggestions, or want to support the project:

  * Submit an [Issue](https://github.com/Swiftfrog/EverMedia/issues) on GitHub.
  * Give the project a ⭐️ star\!

> EverMedia — Making your media library smarter, more stable, and more persistent.

-----

### Next Step

Would you like me to generate a `config.xml` snippet or a specific file structure example based on this documentation to help you set it up?