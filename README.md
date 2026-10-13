# EPIC 机械臂上下料小游戏

使用 Unity 6.6（6000.6.0f1）打开项目，在 `Assets/Scenes/RobotMachineGrab.unity` 中点击 Play。

- 单击机械臂：输送带送料并停稳 → 抓取原料 → 开门上料 → 机械臂退出、关门加工 → 工作台返回并开门 → 取出阶梯轴并放到成品台。
- 双击机械臂：切换损坏或修复，损坏期间流程暂停。
- 右键拖动旋转视角，滚轮缩放，中键平移，R 恢复视角。
- 指示灯：绿色正常/加工，黄色转移动作，红色故障。

此组织仓库包含完整模型文件，当前演示资源直接通过 Git 保存，克隆后无需额外下载 LFS 模型：

```sh
git clone https://github.com/epic699/epic.git
cd epic
```

Game 窗口取消勾选 `Low Resolution Aspect Ratios`，避免低分辨率预览放大。详细实现和操作说明见 [RobotGrab_README.md](RobotGrab_README.md)。

这是固定工位布局的交互原型，加工以动画和工件模型切换表示，不模拟真实切削或工业夹持力。

项目迁移自 [scy8848/EPIC](https://github.com/scy8848/EPIC)，包含原料输送带两侧导向轨道和带面移动同步修正。实现及验证范围见 `RobotGrab_README.md`。
