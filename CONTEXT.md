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
- `Assets/Scripts/Player/Live2D/`：Live2DActor（对话立绘）/ Live2DRealtimeHud（游戏内实时立绘）/ Live2DHudConfig / Live2DHudBootstrap+Spawner —— 见第 9 节
- **完美闪避（原版逻辑：HitBox FSM 的 `Dodged` 事件）**
  - 判定：`PlayerEvade` 的无敌帧窗口内被敌人攻击判定扫到 → `PlayerEvade.NotifyPerfectDodge(attacker)`
  - 玩家反馈：拖影（已有）+ 震屏 + `PlayBackStep` + 无敌延长 `perfectExtraInvincible`
  - 敌人反馈：`EnemyAttackModule.OnPlayerDodged()` → 鬣狗这一爪落空并追加冷却（`EnemyCombat.dodgedCooldownMultiplier`）
  - 判定入口：`EnemyCombat.OnTriggerEnter2D`（爪击，原来会被 `IsPlayerInAirOrInvincible` 静默挡掉）
    和 `PlayerHealth.TakeDamage` 的 `_evadeInvincible` 分支（投射物等其它伤害源）
  - HUD：完美闪避事件直接切 `Face_Smile2`
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
- `Tools/Vivi/Live2D/自检实时 HUD 配置` —— 查动作片段有没有登记进 FadeMotionList、配置里的动作名对不对
- `Tools/Vivi/Live2D/运行时冒烟测试（会切场景）` —— 打开 forest 进 Play，验立绘生成 / 淡入 / 受击动作
- `Tools/Vivi/Live2D/生成游玩时 HUD 预制体` —— 生成 `Assets/Resources/Live2D/Live2DHud.prefab`（游玩时专用）

## 7. 任务清单（`D:\beifen\细节.txt`）

已完成/推进：
1. 敌人攻击后的特效 —— 受击特效已接，攻击轨迹未做
4. 摄像机逻辑 —— 已完成
6. 击晕敌人 —— 已完成
7. 音频 —— 鬣狗 + 束缚玩家已接，BGM/其它敌人未做
8. 玩家实时的 Live2D —— 已完成（游戏内立绘跟着血量/受击/动作实时变），见第 9 节

未做：2 玩家控制器细节、3 准心、5 玩家暴击、9 载体、10 BGM、11 桥段致敬、12 AI 改编曲、13 风格学习

## 8. 工作约定

- 中文回复；用户要求**不要备份**，改前不需要征得同意
- 编译校验：`dotnet csc` + Unity 2022.3 引用 + `C:\Windows\Temp\enemy_stubs.cs` 桩
  - Unity 编辑器在 `D:\HE\unity\unity2022.3.62f3\Unity.exe`，可以直接
    `Unity.exe -batchmode -nographics -quit -projectPath <工程> -logFile <log>` 做全量编译校验
- 敌人改动优先走数据驱动（FSM JSON），不要臆造新状态机
- prefab 恢复：第一层（资源）自动，第二层（脚本）剥离，第三层（PlayMaker 行为）用本项目组件复刻

## 9. 游戏内实时 Live2D（任务 8，已接入）

对话用的 Live2D 和这个**互不影响**：`Live2DActor` 新增了 `hideOnStart` / `manageTransformAndFade`
两个开关，对话场景保持默认 `true`，行为完全没变；实时 HUD 用 `false`。

对话收场：`Live2DActor.hideInPlace = true`（默认）→ 对话结束时**原地淡出**，不再滑到 `hidePosition` 才消失；
要恢复旧行为把它勾掉即可。

### 结构

```
Live2DHudBootstrap         RuntimeInitializeOnLoadMethod，开局建一个常驻 Spawner
└─ Live2DHudSpawner        每次场景加载后等玩家出现 → 生成一次 HUD
   └─ Live2DRealtimeHud    主体：站位 / 淡入淡出 / 状态机
      ├─ HUD Camera        只画 L2D 层、渲染到 RenderTexture（透明底，不会黑边）
      ├─ HUD Canvas        ScreenSpaceOverlay，上面一张 RawImage 显示 RT
      └─ Vivi.prefab 实例  Live2DActor + CubismMotionController / RenderController
```

- 生效方式：**优先用预制体** `Assets/Resources/Live2D/Live2DHud.prefab`（游玩时专用）；
  预制体不存在就按同样结构在代码里现搭（`Live2DRealtimeHud.BuildRig()`）。
  预制体用菜单 `Tools/Vivi/Live2D/生成游玩时 HUD 预制体` 生成，生成后可以随便改相机 / RectTransform。
- 触发条件：场景里出现 `PlayerController` 才生成。主菜单、纯过场不生成。
- 不需要在场景里摆任何物体，**也别手动往场景里拖 HUD**（会重复）。
- 配置资源：`Assets/Resources/Live2D/Live2DHudConfig.asset`（模型、动作片段、站位、状态→动作映射都在这）
- **站位**：屏幕左上角、血条旁边的一块小方框，只露上半身（原版的做法）：
  - `bustTopLeftAnchor` / `bustSize`：小框相对屏幕左上角的偏移 / 尺寸（都是屏幕比例）
  - `bustFraction`：从头顶往下露整身高的多少，0.45 ≈ 头 + 胸；调小 = 更近的大头
  - 实现：HUD 相机渲染到 RT（大小 = 小框像素），UI 的 RawImage 摆在左上角 —— 不靠遮罩、不靠视口裁剪
- **渲染**：HUD 相机 `rendererIndex = 1`（`CubismURPRenderer`，上面 `CubismRenderPassFeature` 是开的），
  跟主相机的 Renderer2D / 刀光那条链路完全隔离；主相机保持默认 Renderer2D 不动
- **透明/变暗**：一律作用在 UI 的 RawImage 上（`color.a` 淡入淡出、`deadTint` 乘色压暗），
  **不要**去改 `CubismRenderController.Opacity` —— 那个是逐 drawable 乘 alpha，重叠部位会露出"透底"，
  不同部位看起来透明度不一样
- 过场：`PlayerController.cutsceneMode == true` 且 `hideDuringCutscene` 勾选时淡出（默认关）

### 状态 → 动作（都能在配置里改）

| 触发 | 动作 | 确定度 |
|---|---|---|
| 血量 >87.5% / >62.5% / >37.5% / >8% / >0 / 死亡 | `HP_100` / `HP_075` / `HP_050` / `HP_025` / `HP_001` / `HP_000`（循环） | ✅ 原版就是这套按血量换身体待机 |
| 掉血 | `Damage1` / `Damage2` 交替，播完回当前血量循环 | ✅ 名字就是 Damage |
| 死亡 | `Damage2` 播一次 → 回到 `HP_000`，整体淡到 55% | ✅ |
| 被束缚 / 回血 | `Face_Sad1` / `Face_Smile1` | ◐ 语义明确，但没原版证据 |
| 攻击 / 闪避 / 滞空 / 瞄准 / 蓄力 / 开枪 / 换弹 / 蹲下 / 完美闪避 | **默认留空（不播）** | ❓ 原版没有对应动作（攻击确认没有），确认后再填 |

- 动作全部来自 `Vivi.prefab` 的 `vivi live2d.fadeMotionList`，44 个动作都登记好了，名字直接写；
  配置里的片段通过 `Live2DHudConfig.motionClips` 在运行时登记进 `Live2DActor`
- Vivi 的动作里 `Vivi_Intro` / `FirstImpression` / `Motion_*` 会改 `ParamCameraZoom` / `ParamRotate`，
  是给过场演出用的，**别拿来当 HUD 待机**
- 调试：游戏里按 **F2** 打一行状态；或勾 `Live2DHudConfig.showDebugLine`

### 已知坑

- **Live2D 在编辑器里不是靠 MeshRenderer 出画面的**：这个版本的 Cubism URP SDK 会把 MeshRenderer 的材质
  换成 `Live2D Cubism/TransparentPicking`（只给 SceneView 拾取用），`isVisible` 永远是 false；
  真正出画面靠**相机所用 Renderer 上的 `CubismRenderPassFeature`**。
  - `Assets/Settings/Renderer2D.asset` 上那个 feature 是 `m_Active: 0`，所以**默认渲染器（Renderer2D）的相机不会画 Live2D**；
  - `CubismURPRenderer.asset`（`m_RendererDataList` 索引 1）上的 feature 是开的，`Cutscene_Stage` 的相机
    `m_RendererIndex: 1` 就是靠这个显示对话 Live2D 的；
  - 实时 HUD 因此自带一台 `rendererIndex = 1` 的相机，跟主渲染器解耦。
  - 如果以后有人把 `Renderer2D` 上的 feature 打开，要注意它会让所有用 Renderer2D 的相机都去画 Live2D。
- 模型包围盒是用 drawable 顶点量的（打包后也有效）；万一量不到会退回写死的 Vivi 尺寸。
- 已验证（2026-11）：`-batchmode` 全量编译通过；`Live2DHudSelfCheck` 动作全可用；
  `Live2DHudPlayTest` 在 forest 真跑：HUD 生成 → 绑定玩家 → 量到尺寸 → 受击播 `Damage1`。
  注意 batchmode 下编辑器不推进帧循环（`Time.frameCount` 一直是 1），
  所以冒烟测试是用反射手动调 `TickPerformance` 驱动的，不是靠帧。
