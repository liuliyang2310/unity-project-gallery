# Unity Project Gallery · v0.9.0-beta

**状态：对外测试版（Beta）**  
**发布包：`UnityProjectGallery-0.9.0-beta-win-x64.zip`**  
**发布日期：2026-10-31**

---

## 快速开始

1. 将 ZIP 解压到普通文件夹，不要在压缩包内直接运行。
2. 双击 `UnityProjectGallery.App.exe` 启动。
3. 在项目库中添加包含 Unity 工程的目录并扫描。
4. 如需手动管理 Unity 版本，打开左侧「Unity Editor 管理」设置页。

> 未签名版本可能触发 Windows SmartScreen 或杀毒软件提示。  
> 确认文件来源后选择「更多信息 → 仍要运行」。

## 系统要求

- Windows 10（22H2 或更新）或 Windows 11，64 位。
- 发布包已包含 .NET 运行时，**无需另外安装 .NET**。
- 建议安装 FFmpeg / FFprobe（见下方说明）。
- 本机 Unity Editor 可选；没有 Editor 时仍可浏览工程与素材。

## 功能范围

- 递归扫描 Unity 工程，读取 Unity 版本与依赖包。
- 仅索引工程根目录下 `_ProjectPreview/` 中的图片与视频。
- 项目卡片、封面、详情页、素材查看器、视频播放。
- 简介、标签、功能说明、素材说明、收藏、最近打开。
- 数据保存到 SQLite，并同步写入工程目录 `_ProjectPreview/project-gallery.json`。
- 路径失效提示与重新关联。
- 搜索、排序、分类、最近打开/收藏筛选。
- Unity Editor 管理：手动添加、自动读取版本、多路径、状态检测、默认启动版本、删除失效记录。

## FFmpeg 说明

发布包**不包含 FFmpeg**。视频时长与封面提取需要 `ffmpeg.exe` / `ffprobe.exe`。

任选一种方式：

1. 将 FFmpeg 的 `bin` 目录加入系统 `PATH`。
2. 设置环境变量 `FFMPEG_PATH`，指向 `ffmpeg.exe` 或其所在目录。
3. 把 `ffmpeg.exe` / `ffprobe.exe` 放到软件安装目录。

缺少 FFmpeg 时图片浏览仍可用，但视频可能没有封面和时长；视频播放能力还取决于 Windows 已安装的解码器。

## 数据与文件位置

| 内容 | 位置 |
| --- | --- |
| 工程数据库 | `%LOCALAPPDATA%\UnityProjectGallery\unity-project-gallery.db` |
| Editor 设置 | `%LOCALAPPDATA%\UnityProjectGallery\unity-editors.json` |
| 缩略图缓存 | `%LOCALAPPDATA%\UnityProjectGallery\Cache\Thumbnails` |
| 运行日志 | `%LOCALAPPDATA%\UnityProjectGallery\Logs` |
| 工程用户编辑 | `<工程目录>\_ProjectPreview\project-gallery.json` |

除 `project-gallery.json` 外，软件不会修改工程内的其他文件。

## Beta 重要提醒

- **请先备份重要 Unity 工程**，不要在唯一副本上测试。
- 首次编辑封面、标签、收藏、简介等数据时，会在工程 `_ProjectPreview` 下创建或更新 `project-gallery.json`。
- 测试版的数据结构可能变化；升级前请保留上一版程序包与数据库备份。
- 本版本无自动更新、无遥测、无崩溃自动上传。
- 程序未做代码签名，SmartScreen 提示属于正常现象。
- 当前只发布 `win-x64` 包，不支持 32 位系统。

## 已知限制

- Unity Editor 自动发现仅覆盖常见安装目录；非标准路径请到设置页手动添加。
- 「默认启动版本」只在找不到工程精确版本时使用；用较低版本打开高版本工程时，Unity 可能提示升级工程。
- 视频播放与封面提取依赖系统解码器和 FFmpeg，不同机器表现可能不同。
- 仅索引 `_ProjectPreview/`，不会扫描 `Screenshots`、`Videos` 等其他目录。
- 工程位于只读目录、网络盘或权限受限目录时，`project-gallery.json` 可能写入失败，状态栏会提示，但 SQLite 数据仍会保存。
- 无安装程序、无代码签名、无自动更新。
- 中文路径与含空格路径已支持，但仍建议在测试中重点验证。

## 建议测试重点

1. 干净机器首次启动，无历史数据库。
2. 添加扫描目录，扫描真实 Unity 工程。
3. 图片大图、视频播放、封面设置、隐藏/恢复素材。
4. 编辑简介、标签、功能、素材说明，重启后确认恢复。
5. 设置页添加 Unity.exe、设置默认版本、删除失效记录。
6. 移动工程目录后检查路径失效提示与重新关联。
7. 快速关闭程序，重新启动确认没有丢失刚编辑的数据。
8. 检查日志目录是否生成 `unity-project-gallery-*.log`。

## 干净虚拟机验收清单

请在未安装 .NET SDK / Visual Studio / FFmpeg 的 Windows 10 或 11 x64 虚拟机上执行：

- [ ] 解压 ZIP 后可直接启动，无缺少运行时提示。
- [ ] SmartScreen 提示可以按说明继续运行。
- [ ] 首次启动无崩溃，数据库与日志目录自动创建。
- [ ] 添加目录并扫描成功，中文/空格路径正常。
- [ ] 图片查看、视频播放与封面功能符合预期（分别在有/无 FFmpeg 环境测试）。
- [ ] 编辑内容重启后仍在。
- [ ] 设置页添加 Editor、设置默认版本、删除失效路径正常。
- [ ] 工程移动后路径失效提示与重新关联正常。
- [ ] 退出后进程正常结束，日志可见。

## 反馈方式

请通过本项目的 GitHub Issues 反馈，并附上：

- 软件版本（设置页显示，例如 `0.9.0-beta`）
- Windows 版本
- 操作步骤与预期结果
- 实际结果与截图
- `%LOCALAPPDATA%\UnityProjectGallery\Logs` 下对应日期的日志

反馈模板：

```text
版本：
Windows：
操作步骤：
预期结果：
实际结果：
是否可复现：
日志文件：
```

## 校验

压缩包 SHA256 见同目录：

```text
UnityProjectGallery-0.9.0-beta-win-x64.sha256.txt
```

PowerShell 校验：

```powershell
Get-FileHash .\UnityProjectGallery-0.9.0-beta-win-x64.zip -Algorithm SHA256
```
