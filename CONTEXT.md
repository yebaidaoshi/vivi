# 项目上下文（给 AI / 协作者）

> 这是同人续作工程，不是原版工程。任何 AI 对话接手前先读完本文件。

## 1. 作品

- 原作：**『ヴィヴィと魔法の島』**（Vivi to Mahou no Shima），社团 **X-Rabbit**
- 类型：2D 横版 ACT
- 女主：**ヴィヴィ（Vivi）**
- 本工程：**同人后续**，不是照搬原作，敌人/Boss 允许重新设计

## 2. 工程

| 项 | 值 |
|---|---|
| 路径 | `D:\HE\unity\Git\GITUHUB\vivi` |
| Unity | 2022.3.62f3 |
| 渲染 | URP 14.0.12（2D） |
| Spine | 自带 runtime，位于 `Assets/TextAsset/Runtime`（asmdef `spine-unity`） |
| 原版解包 | `D:\HE\youxi\Veivei\wenjian\AssetRipper.GUI jiebao\vivi\ExportedProject` |
| 已提取 prefab | `Assets/GameObject`（435）+ `Assets/GameObject/prefab`（30 重名） |
| 依赖恢复 | `Assets/_Restored`（材质/贴图/音频等，脚本类不强求） |

## 3. 敌人架构（已完成，勿推倒重来）

```
EnemyController           总装 + 安全阀 + 左上角调试行（F1）
├─ EnemyAI (Hyena)        FSM 数据驱动 Brain
├─ EnemyMotor             移动/物理/朝向
├─ EnemyAnimDriver        动画切换（按层查找状态）
├─ EnemyCombat            爪击 / 束缚（Mount）
├─ EnemyHealth            血量 / 死亡 / 伤害数字 / 特效
├─ EnemyFlinching         护甲 / 硬直
├─ EnemyStunGauge         眩晕槽
├─ EnemyStunStars         头顶星星（程序生成）
├─ EnemyColorFlasher      受击闪色（只改 RGB，不动 alpha）
├─ EnemyCameraLink        战斗镜头构图
├─ EnemyAudio             音效收口
├─ EnemyWeakPoint         弱点（暴击）
├─ VfxPool                打击特效对象池
├─ DamageNumber           伤害数字（白=敌人，红=玩家）
└─ EnemyHealthBarWorld    世界空间血条（可选）
```

### FSM 数据

- 数据源：`Assets/Resources/EnemyFsm/_HyenaParent.json`（20 个 FSM / 254 状态 / 873 动作）
- `EnemyAI` 读 `_Hyena` / `AI`：111 状态，**约 40 个状态有显式分支**，其余走兜底（停 0.3s 自动 `Next`）
- 全局事件表在 `globalTransitions`（37 条）
- 已实现：感知（前 42 / 后 7）、巡逻 Roam、爪击、束缚（Mount）、跳攻已删除、破甲、眩晕、死亡逃跑

## 4. 关键数值 / 已知坑

| 坑 | 结论 |
|---|---|
| `Movement` 动画 | 是 **BlendTree**，必须每帧 `SetFloat("Speed", motor.Speed)`，否则站着滑行 |
| 受击反馈 | **不要切动画**（`Play` 会从第 0 帧重播 → 肉眼"卡一帧"）；只闪色/震屏/数字 |
| 顿帧 | 已全部移除；`EnemyHitStop` 保留但不再调用（全局 timeScale 很容易卡） |
| 动画冻结 | `EnemyCombat` 束缚击杀会 `FreezeAnimator`，`EnemyController` 有看门狗强制恢复 |
| 眩晕 | 按伤害累积，`maxStun=75`（5 伤 ×15 发），动画 `Break` 3 秒，死亡不触发 |
| 弱点 | 独立碰撞体 + `EnemyWeakPoint`，伤害 ×2，无视护甲 |
| 相机 | 自研 `CameraDirector`（替代 ProCamera2D），会禁用 ScoutCameraFinal / ProCamera2D |
| 调试 | 左上角一行：状态 / 距离 / 动画 / STUN / DEAD，F1 开关；**无其它 Debug 刷屏** |

## 5. 其它系统

- `Assets/Scripts/Scene/Camera/`：CameraDirector / CameraBounds2D / ZoomTrigger
- `Assets/Scripts/Player/`：玩家控制、射击、近战、Evade、Binding（束缚）
- `Assets/Audio/Clips`：1149 个音频；鬣狗音效已按原版 FSM 映射
- 原版音效权威表（来自 FSM 数据）：
  - `ClawAttack` → `Hyena_VClaw`
  - 受击 → `Damage1`；暴击 → `MeleeCritical`
  - 破甲/眩晕 → `Break1`
  - 死亡 → `Hyena_Vdeath`
  - 束缚中 → `Hyena_JumpAttackLoop`；发动 → `Hyena_VJumpAttack2`
  - 玩家挣脱 → `Damage_BearHug*` 随机；`Damage_BearHug_Escape`

## 6. 编辑器菜单（本项目加的）

- `Tools/Vivi/敌人/自动匹配鬣狗音效（精确）`
- `Tools/Vivi/敌人/自动填充束缚音效（选中 BindingProfile）`
- `Tools/Vivi/UI/创建伤害数字配置`

## 7. 任务清单（`D:\beifen\细节.txt`）

已完成/推进：
1. 敌人攻击后的特效 —— 受击特效已接，攻击轨迹未做
4. 摄像机逻辑 —— 已完成
6. 击晕敌人 —— 已完成
7. 音频 —— 鬣狗 + 束缚玩家已接，BGM/其它敌人未做

未做：2 玩家控制器细节、3 准心、5 玩家暴击、8 Live2D、9 载体、10 BGM、11 桥段致敬、12 AI 改编曲、13 风格学习

## 8. 工作约定

- 中文回复；用户要求**不要备份**，改前不需要征得同意
- 编译校验：`dotnet csc` + Unity 2022.3 引用 + `C:\Windows\Temp\enemy_stubs.cs` 桩
- 敌人改动优先走数据驱动（FSM JSON），不要臆造新状态机
- prefab 恢复：第一层（资源）自动，第二层（脚本）剥离，第三层（PlayMaker 行为）用本项目组件复刻
