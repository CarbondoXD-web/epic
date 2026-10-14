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

加工仓门采用顶挂避让路线：先向外和向上各退让 200 mm，再向远离原装附件的一侧滑动 700 mm，开合各约 1.6 秒。五次缓动使起止速度和加速度均为零。门完全打开后才允许上料，机械臂退出至门外等待点后才关门，工作台回位后才再次开门。选择 `Tools > EPIC > Show Chamber Door Dynamic Preview` 可从机床正面观看连续开门、上料、加工、取件和关门过程。

机床左侧两个原装附件 `CONV-SHELF-VF-SM` 和 `CONV-HOLDER-VF-XS` 按 FBX 的原始位置、旋转和比例恢复到机床上。此前添加的独立工具架与下门轨停用，保留对象供 Undo/恢复；所有安装工具均不再将原装附件向外移动。输送线十根支腿与五根横梁、带床保持连接，顶挂门轨与避让段由机壳支架固定。结构检查报告见 `docs/structure-validation.json`，`MachineAttachmentSetup.ApplyBatch` 可重复恢复附件并适配门轨；重复执行不会叠加对象。

加工仓内部已恢复用户提供的 `Haas Mill (1).fbx` 原始主轴立柱、主轴、刀具、罩壳和夹具位置。该文件与项目的 `Assets/Models/Haas Mill Latest.fbx` 内容相同，SHA-256 为 `e8c359976078c593bdb1388f512824d0d44821c72146e993543f64641d11182e`，无需重复模型资源。加工时主轴头回到原始位置，上下料时沿 Z 轴临时上退 250 mm；取放点对齐原始夹具。轨道、机械臂底座、成品台和四阶段工作流程保持不变。携料时先退出机床再从附件外侧高位经过，在原成品台的外侧槽位放料，原台面位置不变。恢复校验见 `docs/original-chamber-validation.json`，可用 `Tools > EPIC > Restore Original Chamber Internals` 重复恢复仓内组件。

已通过 Unity 6000.6.0f1 的四轮实际加工循环、故障暂停/恢复及机床和输送带碰撞检查，第四轮专门检查开门中途故障暂停和恢复。门板整个行程另以 201 个采样点检查机壳、侧面托架及上下导轨。报告见 `docs/conveyor-validation.json` 和 `docs/door-clearance.json`。Windows 首次导入或自动验证可运行 `Tools/Validate-Unity.ps1`，自动处理该编辑器版本中渲染管线转换器脚本缺少元数据的问题。
