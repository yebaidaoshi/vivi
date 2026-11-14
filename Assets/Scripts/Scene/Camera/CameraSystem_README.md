# 相机系统（CameraDirector）

原版游戏的相机是 **Cinemachine**（Brain + vcam + Confiner + TargetGroup）+ 少量 ProCamera2D 遗留。
本工程没有安装 Cinemachine，所以这里用 **纯 C#** 复刻它的行为，走「ProCamera2D 环境 + 自研主控」的路线。

## 文件

| 文件 | 作用 |
| --- | --- |
| `CameraDirector.cs` | 主控：跟随、死区、阻尼、跳跃上抬、战斗多目标构图、缩放、震屏、边界调用 |
| `CameraBounds2D.cs` | 相机边界（对应 CinemachineConfiner） |
| `CameraZoomTrigger.cs` | 区域缩放（对应原版 `ZoomKey` 那套 FSM） |
| `Editor/CameraDirectorMenu.cs` | 一键初始化菜单（Tools/Vivi/摄像机/...） |

## 接入步骤

1. 打开 `Assets/Scenes/Timelines/Cutscene_Stage.unity`。
2. 菜单 **Tools → Vivi → 摄像机 → 初始化 Main Camera（接管 ProCamera2D）**。
   * 自动给 Main Camera 添加 `CameraDirector`
   * 自动禁用同物体上的 `ProCamera2D` / `ProCamera2DShake`（避免两边抢 transform）
3. 选中 Main Camera，在 Inspector 里确认：
   * `Target` 留空即可（运行时按 tag `PlayerPresence` → `Player` 自动找）
   * `Target Offset`：默认 `(0, 7)`，和原场景 ProCamera2D 的 `TargetOffset` 一致
   * `Default Ortho Size`：默认 `10`（你场景当前值；原版是 `7.5`，想要原版构图就改成 7.5）
4. 需要边界时：菜单 **Tools → Vivi → 摄像机 → 创建相机边界区域**，再把生成物的 `BoxCollider2D` 拉大到你想要的战斗区域。

> 手动配置也可以：Main Camera 上 Add Component → `CameraDirector`，然后自己拖 Target。

## 参数与原版的对应关系

| CameraDirector | 原版（Cinemachine / PlayMaker） | 默认值 |
| --- | --- | --- |
| `targetOffset` | vcam Transposer `m_FollowOffset (1.66, 0.15, -10)` / ProCamera2D `TargetOffset` | `(0, 7)` |
| `deadZone` | FramingTransposer `m_DeadZoneWidth/Height = 0` | `(0.5, 0.5)` |
| `followDamping` | FramingTransposer `m_X/Y Damping = 0.5` | `0.5` |
| `combatInfluence` | `PC2DAddCameraTarget` 的 `Weight = 0.5` | `0.5` |
| `combatBlendSpeed` | CinemachineBrain 默认 Blend `2s` | `2` |
| `defaultOrthoSize` | 主相机 `orthographic size = 7.5` | `10` |
| `Shake(duration, strength)` | `PC2DShakeWithValues`（全工程 93 处） | `0.2s / 0.35` |
| `CameraBounds2D` | `CinemachineConfiner`（52 处，`m_ConfineScreenEdges = 1`） | — |

## 给其它系统用的接口

```csharp
// 敌人进入交战（原版是 Sight FSM 里的 PC2DAddCameraTarget）
CameraDirector.Instance.AddCombatTarget(enemyTransform, 1f);

// 敌人脱战 / 死亡
CameraDirector.Instance.RemoveCombatTarget(enemyTransform);

// 受击震屏（原版是 _Heroine Effects FSM 的 PC2DShakeWithValues）
CameraDirector.Instance.Shake(0.18f, 0.4f);

// 缩放
CameraDirector.Instance.SetZoom(8f, 0.3f);
```

> `CameraDirector.Instance` 已在代码里提供（`OnEnable` 注册），敌人 / 玩家系统可直接调用。

## 与原有两个相机脚本的关系

* `ScoutCameraFinal.cs`：保留不动，其它场景继续用；`Cutscene_Stage` 换成 `CameraDirector`。两者的死区/跳跃上抬逻辑一致，`CameraDirector` 多了阻尼、战斗构图、边界、缩放、震屏。
* `ProCamera2D`：保留在场景里但禁用。以后想要它的 Shake 预设，可以在 `CameraDirector.Shake()` 里转调，不影响现在。

## 已知待办

1. 玩家的「空中状态」：`CameraDirector` 用 `Rigidbody2D.velocity.y` 判断跳跃；如果你想让 `PlayerJump.OnAir` 直接驱动，给 `PlayerJump` 实现 `SceneCamera.IPlayerAirState` 即可（一行）。
2. 敌人交战注册：等鬣狗逻辑移植时，在 `Spot` 进入 / `Break` 退出时调用 `AddCombatTarget` / `RemoveCombatTarget`。
3. 过场运镜（`CameraPaths` / DOTweenTransformPath）暂不搬。

## 回滚

改动前已备份：`D:\HE\unity\Git\2\vivi_camera_20261113_2131`
（保留了 `Assets` / `Packages` / `ProjectSettings` / `UserSettings` / `.git`，排除了 `Library`、`obj`、`Logs`、`.vs`）

本次只新增了 `Assets/Scripts/Scene/Camera/` 下的 4 个文件，没有修改任何已有文件。
