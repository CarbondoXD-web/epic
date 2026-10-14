# EPIC 机械臂上下料小游戏

使用 Unity 6.6（6000.6.0f1）打开项目，在 `Assets/Scenes/RobotMachineGrab.unity` 中点击 Play。

- 单击机械臂：输送带送料并停稳 → 抓取原料 → 开门上料 → 机械臂退出、关门加工 → 工作台返回并开门 → 取出阶梯轴并放到成品台。
- 双击机械臂：切换损坏或修复，损坏期间流程暂停。
- 右键拖动旋转视角，滚轮缩放，中键平移，R 恢复视角。
- 指示灯：绿色正常/加工，黄色转移动作，红色故障。

原料输送线现按参考图布置在机床前方中央通道，沿机床正面平行延伸 3.05 米；原料行程 2.65 米，送料约 5 秒，停稳后由固定底座的机械臂抓取。取料端保留 200 mm 无护栏区域，末端支架向上游后移，避开机械臂底座及摆动范围。旧 CAD 输送带和支架保留在场景中并停用。

在 Unity 菜单选择 `Tools > EPIC > Show Central Aisle Dynamic Preview`，自动打开主场景并进入连续加工演示。左上角可关闭连续循环；按 `1` 恢复透视，按 `2` 切换俯视，用于检查轨道与机床、机械臂的相对位置。停止 Play 即退出演示。

`Tools > EPIC > Build Central Aisle Feed Line` 可重建同一布局，重复执行不会叠加轨道。修改位置后运行 `Tools > EPIC > Validate Conveyor And Full Cycle`，验证完整动作、固定底座、输送线与机床/成品台间隙以及故障暂停/恢复。

此组织仓库包含完整模型文件，当前演示资源直接通过 Git 保存，克隆后无需额外下载 LFS 模型：

```sh
git clone https://github.com/epic699/epic.git
cd epic
```

Game 窗口取消勾选 `Low Resolution Aspect Ratios`，避免低分辨率预览放大。详细实现和操作说明见 [RobotGrab_README.md](RobotGrab_README.md)。

这是固定工位布局的交互原型，加工以动画和工件模型切换表示，不模拟真实切削或工业夹持力。

项目迁移自 [scy8848/EPIC](https://github.com/scy8848/EPIC)，包含原料输送带两侧导向轨道和带面移动同步修正。实现及验证范围见 `RobotGrab_README.md`。

加工仓门沿独立的外置上下导轨平移，行程 1.05 米，开合各约 1.6 秒。侧面托架避开门板停靠区域；五次缓动使起止速度和加速度均为零。门完全打开后才允许上料，机械臂退出后才关门，工作台回位后才再次开门。选择 `Tools > EPIC > Show Chamber Door Dynamic Preview` 可从机床正面观看连续开门、上料、加工、取件和关门过程。

已通过 Unity 6000.6.0f1 的四轮实际加工循环、故障暂停/恢复及机床和输送带碰撞检查，第四轮专门检查开门中途故障暂停和恢复。门板整个行程另以 201 个采样点检查机壳、侧面托架及上下导轨。报告见 `docs/conveyor-validation.json` 和 `docs/door-clearance.json`。Windows 首次导入或自动验证可运行 `Tools/Validate-Unity.ps1`，自动处理该编辑器版本中渲染管线转换器脚本缺少元数据的问题。
