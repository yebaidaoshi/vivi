# 敌人框架（Enemy Framework）

## 结构

```
Assets/Scripts/Enemy/
├─ public/                   公用模块（任何敌人复用）
│  ├─ EnemyController.cs     总装：拼 5 个模块、转发动画事件
│  ├─ EnemyBrainBase.cs      决策基类（目标 / 玩家死亡 / 玩家被束缚 / 距离工具 / NotifyHit）
│  ├─ EnemyAttackModule.cs   攻击基类（IsAttacking / PerformAttack / CancelAttack / 冷却）
│  ├─ EnemyMotor.cs          物理移动（跑、停、朝向、击退、冻结、插值）
│  ├─ EnemyAnimDriver.cs     动画封装（PlayState / IsPlaying / 冻结）
│  ├─ EnemyHealth.cs         血量、受击、死亡、逃走
│  ├─ EnemyHighlight.cs      受击描边
│  ├─ EnemySensor.cs         视野锥 + 遮挡 + 背后感知
│  ├─ EnemyStateMachine.cs   泛型状态机
│  ├─ EnemyProfile.cs        数值表（ScriptableObject）
│  ├─ EnemyDifficulty.cs     难度档位 + 倍率（Easy / Normal / Hard）
│  └─ EnemyFramework_README.md
├─ Hyena/                    鬣狗专用
│  ├─ EnemyAI.cs             Brain：Idle / Patrol / Alert / Chase / BackOff / Hurt
│  └─ EnemyCombat.cs         Attack：爪击 + 飞扑束缚
└─ NeedlePlant/              针刺植物（炮台型敌人）
```

## 鬣狗行为（EnemyAI）

| 状态 | 触发 | 表现 |
|---|---|---|
| `Idle` | 初始 / 巡逻结束 | 播 `Idle`，随机 3~5 秒后巡逻 |
| `Patrol` | Idle 计时结束 | 左右随机 ±4 移动，播 `Movement` |
| `Alert` | 首次发现玩家 | 播 `Surprised`，停顿 `alertDuration`，面向玩家 |
| `Chase` | Alert 结束 / 已追击 | 按距离选择 走/跑；近战、飞扑、爬墙都由这里调度 |
| `BackOff` | 贴太近（< `backOffDistance`）且攻击冷却中 | 后退 `backOffDuration`，拉开距离（原版 MoveAway） |
| `Hurt` | 受击（`EnemyHealth.TakeDamage` → `NotifyHit`） | 播 `Hit`，硬直 `hitStunDuration`，打断攻击 |

难度档位 `difficulty` 会缩放：

| 档位 | 移动速度 | 攻击冷却 |
|---|---|---|
| Easy | ×0.85 | ×1.35 |
| Normal | ×1.0 | ×1.0 |
| Hard | ×1.15 | ×0.75 |

调试可视化（Gizmos）：视野锥、背后感知圈、攻击范围、飞扑距离。

## 新增一个敌人（5 步）

1. **数值**：`Assets → Create → Enemy → Enemy Profile`
2. **Brain**：
   ```csharp
   public class GoblinBrain : EnemyBrainBase
   {
       public override void Tick()
       {
           if (target == null || Health.IsDead) return;
           Motor.MoveTo(...); Motor.FaceDirection(...); Anim.PlayState("Run");
       }
       public override void NotifyHit() { /* 硬直 */ }
   }
   ```
3. **Attack**（可选）：
   ```csharp
   public class GoblinAttack : EnemyAttackModule
   {
       public override bool IsAttacking => _busy;
       public override void PerformAttack() { /* 开判定盒、播动画、起协程 */ }
   }
   ```
4. **预制体**：`EnemyController` + 你的 Brain + 你的 Attack + `EnemyHealth` + `EnemySensor`（可选）
5. **动画事件**：`EnemyController.SendEvent(name)` 会同时转发给 Brain 和 Attack 的 `OnAnimEvent(name)`

## 状态机用法（可选）

```csharp
enum GoblinState { Idle, Patrol, Chase, Attack }
readonly var fsm = new EnemyStateMachine<GoblinState>(GoblinState.Idle);

fsm.Changed += (from, to) => { if (to == GoblinState.Chase) Anim.PlayState("Run"); };
fsm.Change(GoblinState.Chase);
fsm.Tick(Time.deltaTime);
if (fsm.Is(GoblinState.Chase) && fsm.TimeInState > 2f) ...
```

## 兼容性 / 清理记录

- `EnemyAI` / `EnemyCombat` 类名、文件名、GUID 未变，鬣狗预制体引用不受影响
- `EnemyController` 序列化字段名保持 `ai` / `combat`，拖好的引用不会丢
- 已删除无用文件：`EnemySettings.cs`（被 `EnemyProfile` 取代）、`EnemyState.cs`（零引用）
- 已删除无用方法：`EnemyCombat.OnClawStart/OnClawEnd/OnAttackEnd`（空实现）、
  `EnemyCombat.GetAttackCollider`、`EnemyCombat.MountCooldownRemaining`、`EnemyMotor.SetVelocityX`

## 待办

1. 把 `EnemyAI` 的视野检测换成 `EnemySensor`
2. 把 `EnemyAI` / `EnemyCombat` 硬编码数值迁进 `EnemyProfile`
3. `PlayerEvade.cs` 对 `EnemyCombat` 的直接引用改成 `EnemyAttackModule`
