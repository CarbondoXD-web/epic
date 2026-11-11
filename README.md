# EPIC · 六机床柔性生产线

一个基于 Unity 的工业产线交互原型：机械臂沿长地轨依次为六台数控机床上下料，加工完成的阶梯轴逐层码放到末端货架，全程可观察、可干预。

![产线总览](Documentation/FactoryLayout/layout-overview.png)

## 功能特性

- **产线循环**：机械臂从原料区直接取料，沿地轨服务六台机床，最后把十二件阶梯轴按每层四件码放到末端货架。
- **两种视角**：整线全景观察（右键旋转 / 中键平移 / 滚轮缩放）与第一人称漫游（WASD 行走、Shift 疾跑、空格跳跃）。
- **直接操作**：单击机械臂开始搬运，双击切换损坏/修复，`P` 暂停继续，界面提供 0.5×～3× 倍速与重新开始。
- **状态反馈**：机床状态面板、机械臂指示灯（绿=正常/加工、黄=转移、红=故障），加工声从各机床主轴位置发出并随距离衰减。
- **编辑期工具**：`Tools > EPIC` 菜单提供布局重建、间隙标定、流程校验等 20 余项一键工具，重复执行不会叠加对象。

## 环境要求

| 项 | 版本 |
| --- | --- |
| Unity | **6000.6.0f1**（Unity 6.6） |
| 渲染管线 | Universal RP 17.6.0 |
| 输入系统 | Input System 1.20.0（工程已设为 New Input System，旧 Input Manager 不可用） |
| Git LFS | **必需**，音频与文档图片以 LFS 存储；FBX 模型直接存入仓库（见 `.gitattributes` 末尾的覆盖规则） |

## 快速开始

```sh
# 1. 先安装并初始化 Git LFS（音频与文档图片依赖它）
git lfs install

# 2. 克隆仓库
git clone https://github.com/CarbondoXD-web/epic.git
cd epic

# 3. 确认 LFS 实体已落地（clone 时自动 smudge 失败的话再执行一次）
git lfs pull
```

用 Unity Hub 以 **6000.6.0f1** 打开工程。首次导入需重建 `Library`，模型合计约 200 MB，通常需要几分钟。

打开 `Assets/Scenes/FactoryLayout.unity`，点击 Play。

> **注意：不要用 GitHub 网页的 “Download ZIP”。**
> 源码压缩包不含 LFS 实体。FBX 模型因为直接存在仓库里，ZIP 中是完整的；
> 但 `Assets/Audio/*.wav` 与 `Documentation/**/*.png` 会变成 100 多字节的指针文本，
> 结果是加工音效丢失、文档图片损坏。请始终使用 `git clone` + `git lfs pull`。

## 操作说明

### 全景观察（默认）

| 操作 | 功能 |
| --- | --- |
| 右键拖动 | 绕中心旋转 |
| 中键拖动 | 平移焦点 |
| 滚轮 | 缩放（4–65 m） |
| `1` / `2` / `3` / `4` | 整线全景 / 俯视布局 / 原料区域 / 成品货架 |
| `R` | 恢复默认视角 |

### 第一人称漫游

按 `F` 在漫游与全景之间切换。

| 操作 | 功能 |
| --- | --- |
| `W` `A` `S` `D` / 方向键 | 行走（2.4 m/s） |
| `Shift` | 疾跑（6.0 m/s） |
| `空格` | 跳跃（约 1.1 m 高） |
| 鼠标 | 环视 |
| `Esc` | 释放鼠标，点击画面重新锁定 |
| `R` | 回到出生点 |
| `F` | 返回全景观察 |

### 产线控制

| 操作 | 功能 |
| --- | --- |
| 单击机械臂 | 开始当前搬运任务 |
| 双击机械臂 | 切换损坏 / 修复（损坏期间流程暂停） |
| `P` | 暂停 / 继续 |

## 场景

| 场景 | 说明 |
| --- | --- |
| `Assets/Scenes/FactoryLayout.unity` | **主场景**，六机床柔性生产线 |
| `Assets/Scenes/RobotMachineGrab.unity` | 单机床演示：输送带送料 → 抓取 → 开门上料 → 加工 → 取件，详见 [RobotGrab_README.md](RobotGrab_README.md) |
| `Assets/Scenes/RobotGrab.unity` | 早期机械臂抓取演示 |
| `Assets/Scenes/SampleScene.unity` | URP 模板空场景 |

## 项目结构

```
Assets/
  Models/          FBX 模型（LFS），含 StorageRack/、合并/、围网_合并/
  Scenes/          四个场景
  Scripts/         运行时脚本，Factory/ 下为产线逻辑
  Editor/          Tools > EPIC 编辑器工具
  Materials/       材质
  Settings/        URP 管线与渲染配置
Documentation/     布局说明、验证记录与截图
docs/              校验报告（JSON）
Tools/             Validate-Unity.ps1 等命令行工具
Packages/          manifest.json 与 packages-lock.json
ProjectSettings/   Unity 工程设置
```

## 文档

- [模型布局说明](Documentation/FactoryLayout/模型布局说明.md)
- [直取原料使用说明](Documentation/FactoryLayout/直取原料使用说明.md)
- [单机床演示说明](RobotGrab_README.md)
- [开发记录](Documentation/开发记录.md)：各阶段实现细节与调整原因

## 本地依赖说明

仓库的 `Packages/manifest.json` **不包含任何指向本机绝对路径的本地包**，保证 clone 后可直接解析。

如果你本地使用了 Unity MCP（`com.coplaydev.unity-mcp`）这类 `file:` 本地包，请只在本机添加，
不要提交——否则其他人 clone 后会因为解析不到该路径而无法正常打开工程。

## 仓库体积说明

模型以普通 git blob 存储，仓库完整克隆约 250 MB。其中
`Assets/Models/mobile robot+tray.fbx` 单个文件 **96.5 MB**，已接近 GitHub 100 MiB 的单文件硬上限。

后续若要继续扩充模型，建议改为 Git LFS 存储（需要 `git lfs migrate` 重写历史），
或让美术导出减面版本，否则该文件任何一次修改都可能导致推送被拒。

## 已知限制

这是固定工位布局的交互原型，加工过程以动画与工件模型切换表示，不模拟真实切削、刀具磨损或工业夹持力。

## 致谢

项目迁移自 [scy8848/EPIC](https://github.com/scy8848/EPIC)，并在此基础上扩展了丝杠输送线、加工仓门避让、
机床附件复原、六机床产线与第一人称漫游。

## 许可

本仓库未附带开源许可协议。如需在其它项目中使用其中的模型或代码，请先联系仓库作者取得授权。
