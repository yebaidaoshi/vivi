# 鬣狗原版 FSM 规格（自动导出）

> 来源：`_HyenaParent.prefab`　导出时间：2026-11-13 23:13
> 由 YAML 直接解析，未经人工改写。动作名已去掉命名空间前缀。

## 总览

| 项 | 值 |
| --- | --- |
| FSM 数量 | 20 |
| 状态总数 | 254 |
| 动作实例总数 | 873 |
| 主 AI 状态数 | 111 |

| # | 宿主物体 | FSM 名 | 起始状态 | 状态数 |
| ---: | --- | --- | --- | ---: |
| 1 | _HyenaParent | FSM | State 1 | 3 |
| 2 | CollisionShieldRight | FSM | State 1 | 1 |
| 3 | CollisionShieldLeft | FSM | State 1 | 1 |
| 4 | _Hyena | AI | State 9 | 111 |
| 5 | _Hyena | StunGauge | State 1 | 17 |
| 6 | _Hyena | Life | State 1 | 29 |
| 7 | _Hyena | H_SE | State 1 | 28 |
| 8 | _Hyena | Flinching | State 1 | 12 |
| 9 | _Hyena | TimeControl | State 1 | 4 |
| 10 | HitBox | FSM | State 1 | 4 |
| 11 | HitBox_WeakPoint | FSM | State 1 | 4 |
| 12 | Sight | FSM | Sight | 3 |
| 13 | BackSight | FSM | Sight | 3 |
| 14 | LedgeChecker | FSM | Serching | 3 |
| 15 | JumpAttack_Range | FSM | Wait | 2 |
| 16 | JumpAttack_HitBox | FSM | Wait | 11 |
| 17 | ClawAttack_Range | FSM | Wait | 2 |
| 18 | ClawAttack_HitBox | FSM | Wait | 7 |
| 19 | PutMeInCamera | Camera | Idle | 6 |
| 20 | BackSight2 | FSM | Sight | 3 |

## 事件（跳转条件）清单

10x、ArmorBreak、ArmorEnd、ArmorOff、Back、BlowAway、Break、Break_Heal、CanFlinch、Claw、Claw_End、ColorOff、Count、Counter、CounterCheck、CritBreak、Dead、Death、Detect、do、Dodged、Down、Easy、End、Escaped、FINISHED、FleeLeft、FleeRight、Fore、GetCloser、GoArmor、GoCheck、H_Check、H_Move、Hard、Heal、Hit、InRange、JAttack、JAttack_End、Jump、Jump_High、Jump_Low、LockReset、Loop、Mount_Damage、MoveAway、MoveLeft、MoveRight、Next、Normal、OnGround、OutofSight、Particle、Refresh、RoamLeft、RoamRight、Spot、Step、Stop、StopTrigger、Stun、Trigger、Up、Zero

## 动作频次

| 动作 | 次数 |
| --- | ---: |
| ActivateGameObject | 98 |
| SendEventByName | 50 |
| AudioPlay | 45 |
| AnimatorPlay | 44 |
| RunFSM | 36 |
| BoolTest | 30 |
| Trigger2dEvent | 29 |
| SetProperty | 28 |
| Wait | 25 |
| GetPosition2d | 24 |
| SetScale | 24 |
| FloatCompare | 23 |
| SetVelocity2dChronos | 22 |
| SetFloatValue | 21 |
| GetScale | 19 |
| GetAnimatorCurrentStateInfoIsName | 18 |
| CreateObject | 18 |
| SetFsmBool | 18 |
| ChronosWait | 17 |
| SetBoolValue | 16 |
| SetFsmGameObject | 15 |
| NextFrameEvent | 13 |
| SetPosition | 13 |
| GetParent | 12 |
| GameObjectIsNull | 12 |
| FloatMultiply | 11 |
| SetVector3XYZ | 11 |
| SetAnimatorLayerWeight | 11 |
| SetTag | 10 |
| SetFsmFloat | 9 |
| SetAnimatorFloat | 8 |
| ScaleGlobalClockTime | 8 |
| FloatSubtract | 8 |
| FsmFloatAdd | 8 |
| AddForce2d | 7 |
| FloatAbs | 7 |
| SetAnimatorBool | 7 |
| FloatAdd | 6 |
| iTweenMoveTo | 6 |
| GetFsmGameObject | 6 |
| DestroyObject | 5 |
| AudioStop | 5 |
| GetChild | 5 |
| FloatOperator | 5 |
| SetStringValue | 5 |
| GameObjectCompare | 4 |
| SetAnimatorTrigger | 4 |
| SendEvent | 4 |
| GetFsmFloat | 3 |
| ScaleClockTime | 3 |
| RayCast2d | 3 |
| StringSwitch | 3 |
| SetParent | 2 |
| StringCompare | 2 |
| DOTweenTransformScale | 2 |
| PC2DRemoveCameraTarget | 2 |
| DOTweenTransformShakeScale | 2 |
| EaseFloat | 2 |
| GetFsmBool | 2 |
| SetLayer | 1 |
| AddToFsmInt | 1 |
| FloatInterpolateChronos | 1 |
| SetAudioClip | 1 |
| FloatClamp | 1 |
| FloatSwitch | 1 |
| Vector2AddXY | 1 |
| GetName | 1 |
| SendRandomEvent | 1 |
| GetOwner | 1 |
| GetPosition | 1 |
| FloatDivide | 1 |
| GetVelocity2d | 1 |
| CircleCast2d | 1 |
| SetDataInt | 1 |
| PC2DCinematicsToggle | 1 |
| GetAnimatorCurrentStateInfoIsTag | 1 |

## 主 AI FSM（全量）

宿主：`_Hyena`　起始：`State 9`　状态数：111

### `Walk`

- 动作：StringCompare, ActivateGameObject, ActivateGameObject, AnimatorPlay
- 跳转：FINISHED → State 61
- 参数：Run, Walk, 1, 1

### `Spot`

- 动作：SetAnimatorBool, AnimatorPlay, SetBoolValue, NextFrameEvent
- 参数：caution, StandBy, 1, 1

### `Break`

- 动作：SetFsmFloat, SendEventByName, ActivateGameObject, ActivateGameObject, SetFsmBool, AnimatorPlay, BoolTest, GetFsmGameObject, GameObjectCompare
- 跳转：LockReset → State 7 ； Next → State 79
- 参数：{fileID: 1618682377417806}, Flinching, FlinchTime, Break, Life, Jumping, Break, ENEMYCHECKER, ATTACKER, 33, 1, 1

### `ClawAttack`

- 动作：GetFsmGameObject, GameObjectIsNull, SetFsmGameObject, GetScale, FloatMultiply, SetVector3XYZ, CreateObject, SendEventByName, ActivateGameObject, AnimatorPlay, AudioPlay, RunFSM
- 跳转：Claw → State 15
- 参数：{fileID: 1618682377417806}, {fileID: 1709254077376921, guid: 4391682998378a542bdc96f1a93ec8ed, type: 2}, {fileID: 1618682377417806}, ENEMYCHECKER, ATTACKER, ENEMYCHECKER, ATTACKER, Armor, {fileID: 8300000, guid: 81a9475080daefb4b92035546b612bfc, type: 3}, 4, 4, 1, {x: 0, y: 0, z: 0}, Vector3={x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}, Vector3={x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}

### `Walking`

- 动作：GetScale, FloatOperator, SetVelocity2dChronos, GameObjectIsNull, Wait
- 跳转：FINISHED → State 61
- 参数：7, 2, {x: 0, y: 0}, {x: 0, y: 0, z: 0}

### `State 15`

- 动作：ActivateGameObject, CreateObject, GetScale, SetScale
- 跳转：Claw_End → State 24
- 参数：{fileID: 1709254077376921, guid: 14f2812c528801145a32e9bc0035aa03, type: 2}, {fileID: 1618682377417806}, 1, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}

### `State 24`

- 动作：SendEventByName, SetFsmFloat, ActivateGameObject, ScaleGlobalClockTime, GetAnimatorCurrentStateInfoIsName
- 跳转：FINISHED → State 20
- 参数：ArmorEnd, Flinching, FlinchTime, Root, Idle_Caution, 4, 1

### `State 33`

- 动作：SetAnimatorLayerWeight, SetAnimatorFloat, SetAnimatorBool, SetFsmBool, ActivateGameObject, ActivateGameObject, SetProperty, SetProperty, SetProperty, SetProperty, SetProperty, SetProperty, SetProperty, ActivateGameObject, SetBoolValue, ChronosWait
- 参数：color, standby, AI, Spot, 1, 1, 1, 1, 1, 1, 1, 1, 1

### `State 34`

- 动作：GetChild, ActivateGameObject, ActivateGameObject, SetAnimatorLayerWeight, SetAnimatorFloat
- 跳转：FINISHED → State 75
- 参数：AlertText, Untagged, color, 1, 2, 1, 1, 1

### `State 36`

- 动作：iTweenMoveTo, SetAnimatorFloat, DOTweenTransformScale
- 跳转：Next → State 33
- 参数：color, _ShadowDampTime=0.5, 1, _ShadowDampTime=0.5, _ShadowDampTime=0.5, 1, 1, _ShadowTweenVector={x: 5, y: 12, z: 0}, {x: 0, y: 0, z: 0}, _ShadowTweenScale={x: 0, y: 0, z: 0}

### `State 37`

- 动作：SetScale, SetVector3XYZ, SetVector3XYZ, SetProperty, SetAnimatorLayerWeight, SetAnimatorFloat, SetProperty, SetProperty, SetProperty, SetProperty
- 跳转：FINISHED → State 56
- 参数：color, 0.8, 0.8, 1, 1, 3, -1, 1, 2, 1, {x: 0, y: 0, z: 0}, _ShadowTweenScale={x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}, _ShadowTweenVector={x: 5, y: 12, z: 0}, {x: 0, y: 0, z: 0}

### `State 38`

- 动作：SetVelocity2dChronos, SetScale
- 参数：_ShadowMoveVelocity=5, _ShadowScaleTo=1, {x: 0, y: 0}, {x: 0, y: 0, z: 0}

### `State 39`

- 动作：SetAnimatorBool, SetVelocity2dChronos
- 参数：shadow, {x: 0, y: 0}

### `Check`

- 动作：GetPosition, SetVector3XYZ, GetName, SendEventByName
- 参数：{x: 0, y: 0, z: 0}, Offset={x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}

### `Step_Up`

- 动作：ActivateGameObject, ActivateGameObject, GetParent, GetChild, AnimatorPlay, SendEventByName, SetVelocity2dChronos, ActivateGameObject, ActivateGameObject
- 跳转：Jump → State 5
- 参数：Step_Down, Jump_Middle, Jumping, 1, 1, 1, 1, {x: 0, y: 0}

### `Climb_Up`

- 动作：GetParent, GetChild, AnimatorPlay, SendEventByName, SetVelocity2dChronos, ActivateGameObject, ActivateGameObject
- 跳转：Jump_High → Climbed
- 参数：Climb_Down, Jump_High, Jumping, 1, 1, {x: 0, y: 0}

### `Climbed`

- 动作：iTweenMoveTo, GetAnimatorCurrentStateInfoIsName
- 参数：Idle_Caution, 0.3, Offset={x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}

### `Step_Over`

- 动作：ActivateGameObject, ActivateGameObject
- 参数：1, 1

### `Step_Down`

- 动作：ActivateGameObject, ActivateGameObject, GetParent, GetChild, AnimatorPlay, SendEventByName, SetVelocity2dChronos, ActivateGameObject, ActivateGameObject
- 跳转：Jump → State 72
- 参数：Step_Up, Jump_Middle, Jumping, 1, 1, 1, 1, {x: 0, y: 0}

### `Climb_Down`

- 动作：GetParent, GetChild, AnimatorPlay, SendEventByName, SetVelocity2dChronos, ActivateGameObject, ActivateGameObject
- 跳转：Jump_Low → State 1
- 参数：Climb_Up, Jump_Low0, Jumping, 1, 1, {x: 0, y: 0}

### `State 5`

- 动作：iTweenMoveTo
- 参数：0.33, 1, Offset={x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}

### `State 1`

- 动作：iTweenMoveTo, GetScale, FloatMultiply, AddForce2d, ChronosWait, GetAnimatorCurrentStateInfoIsName
- 跳转：Next → State 49
- 参数：Idle_Caution, 0.3, 20, 10, 0.5, 1, {x: 0, y: 0}, {x: 0, y: 0}, Offset={x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}

### `JumpAttack`

- 动作：GetFsmGameObject, GameObjectIsNull, SetFsmGameObject, SendEventByName, ActivateGameObject, AnimatorPlay, AudioPlay, RunFSM, RunFSM
- 跳转：JAttack → State 21
- 参数：{fileID: 1618682377417806}, ENEMYCHECKER, ATTACKER, ENEMYCHECKER, ATTACKER, Armor, StandBy, {fileID: 8300000, guid: 62f96e9ca36cefd409690945bdfa4ec8, type: 3}, 1

### `State 21`

- 动作：AudioPlay, ActivateGameObject, ActivateGameObject, CreateObject, GetScale, FloatOperator, FloatMultiply, SetScale, AddForce2d, ActivateGameObject, ActivateGameObject, ActivateGameObject, ActivateGameObject, SetFsmBool
- 跳转：JAttack_End → State 23 ； Hit → HitEvent
- 参数：{fileID: 1709254077376921, guid: a83f67ccc06f521449b5c97e8832892b, type: 2}, {fileID: 1618682377417806}, Life, Jumping, {fileID: 8300000, guid: 02811fff9fd4c7549ba7edcf20d51188, type: 3}, 50, -1, 23, 1, 1, 1, {x: 0, y: 0}, {x: 0, y: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}

### `State 23`

- 动作：ScaleGlobalClockTime, SetFsmBool, SendEventByName, SetFsmFloat, ActivateGameObject, SetFsmBool, SetFsmBool, GetAnimatorCurrentStateInfoIsName
- 跳转：Next → State 11
- 参数：Root, Life, Jumping, ArmorEnd, Flinching, FlinchTime, Life, Jumping, Life, Jumping, Idle_Caution, 1, 4, 1

### `Idle`

- 动作：BoolTest, BoolTest, BoolTest, BoolTest, BoolTest

### `Surprised`

- 动作：SetAnimatorBool, SetBoolValue, RunFSM, AnimatorPlay, GetAnimatorCurrentStateInfoIsName
- 跳转：Next → State 2
- 参数：caution, Surprised, Surprised, 1, 1

### `INIT`

- 动作：SetFsmBool, ActivateGameObject, ActivateGameObject, ActivateGameObject, ActivateGameObject, ActivateGameObject, ActivateGameObject, BoolTest, BoolTest, NextFrameEvent
- 跳转：FINISHED → Idle
- 参数：Life, Jumping, 1, 1, 1, 1, 1, 1

### `HitEvent`

- 动作：SetFsmBool, SetFsmBool, AnimatorPlay, AudioPlay, SetAudioClip, AudioPlay, SetBoolValue, SetBoolValue, GetParent, GetScale, SetFsmFloat, SetFsmGameObject, SendEventByName, GetParent, SetVelocity2dChronos, GetPosition2d, SetPosition, ActivateGameObject, ActivateGameObject, SendEvent, NextFrameEvent
- 跳转：FINISHED → State 76
- 参数：{fileID: 1618682377417806}, FSM, Jumping, Life, Jumping, Mounted, FSM, LockScale, Movement, LockingObject, Hyena_JumpAttack, {fileID: 8300000, guid: ea5dd2edde19f624c852a562c62a8cba, type: 3}, {fileID: 8300000, guid: 359cc4a79f592664fb6bb56b30dc34a3, type: 3}, SEVOLUME=0.4, 1, 1, {x: 0, y: 0}, {x: 0, y: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}

### `State 25`

- 动作：GetPosition2d, SetPosition
- 跳转：Escaped → State 45 ； Mount_Damage → State 41
- 参数：{x: 0, y: 0}, {x: 0, y: 0, z: 0}

### `State 40`

- 动作：AudioPlay, AudioPlay, AnimatorPlay, GetScale, FloatMultiply, AddForce2d, SetFsmGameObject, GetAnimatorCurrentStateInfoIsName
- 参数：Hit_Blow1, ENEMYCHECKER, ATTACKER, Idle_Caution, {fileID: 8300000, guid: 536a2066e8f5e7f47ae61fe6569076fd, type: 3}, {fileID: 8300000, guid: 78086edef1914e04aaa19c9a7c304d55, type: 3}, 30, 23, {x: 0, y: 0}, {x: 0, y: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}

### `State 41`

- 动作：AudioPlay, GetFsmFloat, FloatSubtract, SetFsmFloat, FloatCompare
- 跳转：do → State 25 ； Death → State 62
- 参数：Life, Life, Life, Life, {fileID: 8300000, guid: 59a6144420bc9cb4891b93ca27c4b645, type: 3}, 10

### `State 42`

- 动作：ActivateGameObject, SetTag, SetTag, GetScale, FloatMultiply, SetScale, SetBoolValue, SetProperty, SetVelocity2dChronos, SendEventByName, SendEventByName, SendEventByName, AnimatorPlay, AnimatorPlay, SendEventByName, ActivateGameObject, ActivateGameObject, AudioStop, NextFrameEvent
- 跳转：FINISHED → State 43
- 参数：Untagged, Untagged, SCENE, HSCENE, STOP, H_1_Start, H_Hyena1, V_MountDead, -1, 1, 1, {x: 0, y: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}

### `State 43`

- 动作：GetPosition2d, SetPosition, CreateObject, GetScale, SetScale, SendEventByName, PC2DCinematicsToggle, GetAnimatorCurrentStateInfoIsName
- 跳转：Next → State 51
- 参数：{fileID: 1709254077376921, guid: 697599dbaf445c54bb7ef3fa7203963b, type: 2}, {fileID: 1618682377417806}, {fileID: 1618682377417806}, HSCENE, H_2_Loop, {x: 0, y: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 10, z: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}

### `State 45`

- 动作：SetBoolValue, ChronosWait, AudioStop
- 跳转：FINISHED → State 40
- 参数：0.33

### `State 2`

- 动作：RunFSM, GetAnimatorCurrentStateInfoIsName
- 参数：Idle_Caution

### `State 3`

- 动作：ActivateGameObject, SetVelocity2dChronos, GetOwner, GetScale, FloatCompare
- 跳转：Fore → State 4 ； Back → State 6
- 参数：1, {x: 0, y: 0}, {x: 0, y: 0, z: 0}

### `State 4`

- 动作：AnimatorPlay, GetAnimatorCurrentStateInfoIsName
- 跳转：Next → State 36
- 参数：SpawnFore, Spawn_Fore

### `State 6`

- 动作：AnimatorPlay, GetAnimatorCurrentStateInfoIsName
- 跳转：Next → State 36
- 参数：SpawnBack, Spawn_Back

### `State 7`

- 动作：AudioStop, SendEventByName, GetFsmGameObject, GameObjectCompare
- 跳转：Next → State 79
- 参数：{fileID: 1618682377417806}, Breaked, ENEMYCHECKER, ATTACKER

### `State 8`

- 动作：Wait
- 跳转：FINISHED → State 10
- 参数：2

### `State 9`

- 动作：SetProperty, SetBoolValue, SetBoolValue, SetBoolValue, SetAnimatorBool, AnimatorPlay, SetVelocity2dChronos, FloatCompare
- 跳转：Easy → Easy ； Normal → Normal ； Hard → Hard
- 参数：caution, Idle, Difficulty=1, 1, {x: 0, y: 0}

### `State 10`

- 动作：ActivateGameObject

### `State 11`

- 动作：SetFsmGameObject, ChronosWait
- 跳转：FINISHED → State 22
- 参数：ENEMYCHECKER, ATTACKER, 0.6

### `State 12`

- 动作：Wait
- 跳转：FINISHED → INIT
- 参数：2

### `State 13`

- 动作：GetScale, FloatCompare
- 跳转：RoamLeft → State 14 ； RoamRight → State 17
- 参数：{x: 0, y: 0, z: 0}

### `State 14`

- 动作：AnimatorPlay, SetScale, SetVelocity2dChronos, Wait, BoolTest, BoolTest
- 跳转：FINISHED → State 16
- 参数：Walk, -1, -5, 4, {x: 0, y: 0}, {x: 0, y: 0, z: 0}

### `State 16`

- 动作：AnimatorPlay, Wait, BoolTest, BoolTest
- 跳转：FINISHED → State 17
- 参数：Idle, 1

### `State 17`

- 动作：AnimatorPlay, SetScale, SetVelocity2dChronos, Wait, BoolTest, BoolTest
- 跳转：FINISHED → State 18
- 参数：Walk, 1, 5, 4, {x: 0, y: 0}, {x: 0, y: 0, z: 0}

### `State 18`

- 动作：AnimatorPlay, Wait, BoolTest, BoolTest
- 跳转：FINISHED → State 14
- 参数：Idle, 1

### `State 19`

- 动作：BoolTest, ActivateGameObject, ActivateGameObject, ActivateGameObject, ActivateGameObject, BoolTest
- 跳转：H_Move → H_Move ； H_Check → State 27 ； End → State 48

### `State 20`

- 动作：SetFsmGameObject, ChronosWait
- 参数：ENEMYCHECKER, ATTACKER

### `State 22`

- 动作：FloatCompare, ChronosWait
- 参数：Difficulty=1, 1, 0.6

### `H_Move`

- 动作：GetPosition2d, GetPosition2d, FloatSubtract, FloatAbs, FloatCompare
- 跳转：MoveAway → MoveAway ； GetCloser → GalleryGetClose ； Next → State 31
- 参数：10, 0.5, {x: 0, y: 0}, {x: 0, y: 0}

### `State 27`

- 动作：BoolTest
- 跳转：H_Move → H_Move ； GetCloser → GetCloser

### `MovementLoop`

- 动作：GetPosition2d, GetPosition2d, FloatSubtract, FloatAbs, FloatCompare, RunFSM, RunFSM, GameObjectIsNull, NextFrameEvent
- 跳转：FINISHED → MovementLoop
- 参数：2, {x: 0, y: 0}, {x: 0, y: 0}

### `MoveAway`

- 动作：GetPosition2d, Vector2AddXY, RayCast2d, RayCast2d, GetPosition2d, GetPosition2d, FloatCompare
- 参数：2, 10, 10, 9, 9, 1, 1, Vector={x: 0, y: 0}, Vector={x: 0, y: 0}, Vector={x: 0, y: 0}, {x: 1, y: 0}, {x: 0, y: 0}, {x: 0, y: 0}, Vector={x: 0, y: 0}, {x: -1, y: 0}, {x: 0, y: 0}, {x: 0, y: 0}, {x: 0, y: 0}, {x: 0, y: 0}, {r: 1, g: 0.92156863, b: 0.015686275, a: 1}, {r: 1, g: 0.92156863, b: 0.015686275, a: 1}

### `State 30`

- 动作：RunFSM, RunFSM, GameObjectIsNull, NextFrameEvent
- 跳转：FINISHED → State 26

### `State 31`

- 动作：AnimatorPlay, RunFSM
- 参数：Idle

### `GetCloser`

- 动作：AnimatorPlay
- 跳转：FINISHED → State 74
- 参数：Walk

### `GalleryGetClose`

- 动作：AnimatorPlay
- 跳转：FINISHED → State 30
- 参数：Walk

### `State 44`

- 动作：AnimatorPlay, BoolTest
- 跳转：Next → State 32 ； MoveLeft → State 29
- 参数：Walk

### `State 46`

- 动作：AnimatorPlay, BoolTest
- 跳转：Next → State 29 ； MoveRight → State 32
- 参数：Walk

### `State 26`

- 动作：GetPosition2d, FloatSubtract, FloatAbs, FloatCompare
- 跳转：Back → State 30 ； Next → State 31
- 参数：10, 0.5, {x: 0, y: 0}

### `State 29`

- 动作：SetScale, SetVelocity2dChronos, GetPosition2d, FloatSubtract, FloatCompare, NextFrameEvent
- 跳转：Loop → State 29 ； Next → State 28
- 参数：-1, -5, -10, 1, {x: 0, y: 0}, {x: 0, y: 0}, {x: 0, y: 0, z: 0}

### `State 32`

- 动作：SetScale, SetVelocity2dChronos, GetPosition2d, FloatSubtract, FloatCompare, NextFrameEvent
- 跳转：Loop → State 32 ； Next → State 28
- 参数：1, 5, 10, 1, {x: 0, y: 0}, {x: 0, y: 0}, {x: 0, y: 0, z: 0}

### `State 28`

- 动作：AnimatorPlay, RunFSM
- 参数：Idle

### `State 35`

- 动作：ActivateGameObject, ActivateGameObject, GetFsmGameObject, GameObjectCompare
- 跳转：Next → State 79
- 参数：{fileID: 1618682377417806}, ENEMYCHECKER, ATTACKER, 1, 1

### `State 47`

- 动作：ChronosWait
- 跳转：ArmorOff → INIT

### `State 48`


### `State 49`

- 动作：RayCast2d, AddForce2d, AddForce2d, BoolTest
- 跳转：Next → State 50
- 参数：0.5, -10, -30, 1, 9, {x: 0, y: 0}, {x: 0, y: -1}, {x: 0, y: 0}, {x: 0, y: 0}, {x: 0, y: 0}, {x: 0, y: 0}, {x: 0, y: 0}, {x: 0, y: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}, {r: 1, g: 0.92156863, b: 0.015686275, a: 1}

### `State 50`

- 动作：AnimatorPlay, GetAnimatorCurrentStateInfoIsName
- 参数：Jump_Low2, Idle_Caution

### `State 51`

- 动作：ChronosWait, GetPosition2d, SetPosition
- 跳转：FINISHED → State 52
- 参数：2, {x: 0, y: 0}, {x: 0, y: 0, z: 0}

### `State 52`

- 动作：ActivateGameObject, BoolTest, GetPosition2d, SetPosition
- 跳转：Next → State 53
- 参数：1, {x: 0, y: 0}, {x: 0, y: 0, z: 0}

### `State 53`

- 动作：SetAnimatorTrigger, SetAnimatorTrigger, ActivateGameObject, GetPosition2d, SetPosition, GetAnimatorCurrentStateInfoIsName
- 跳转：Next → State 80
- 参数：SceneNext, SceneNext, H_4_AfterLoop, {x: 0, y: 0}, {x: 0, y: 0, z: 0}

### `Easy`

- 动作：SetStringValue, SetFloatValue, SetFloatValue, SetFloatValue, Wait
- 跳转：FINISHED → INIT
- 参数：Claw_Easy, 16, 0.9, 1.4, 1

### `Normal`

- 动作：SetStringValue, SetFloatValue, SetFloatValue, SetFloatValue, Wait
- 跳转：FINISHED → INIT
- 参数：Claw, 20, 0.7, 0.8, 1

### `Hard`

- 动作：SetStringValue, SetFloatValue, SetFloatValue, SetFloatValue, Wait
- 跳转：FINISHED → INIT
- 参数：Claw_Hard, 24, 0.5, 0.2, 1

### `State 70`

- 动作：ActivateGameObject, ActivateGameObject, ActivateGameObject, ActivateGameObject, ActivateGameObject, ActivateGameObject, AnimatorPlay, RunFSM, RunFSM, SendEventByName, GetAnimatorCurrentStateInfoIsName
- 跳转：Next → State 71
- 参数：Hit_Blow1, ArmorEnd, Hit_Blow1, 1, 1, 1, 1, 1, 1

### `State 71`

- 动作：GetAnimatorCurrentStateInfoIsName, GetAnimatorCurrentStateInfoIsName
- 参数：Idle, Idle_Caution

### `State 55`

- 动作：SetProperty, SetScale, SetVector3XYZ, SetVector3XYZ, SetAnimatorLayerWeight, SetAnimatorFloat, SetProperty, SetProperty, SetProperty, SetProperty
- 跳转：FINISHED → State 56
- 参数：color, 2.2, 2.2, 1, 1, 5, 8, 1, 2, 1, {x: 0, y: 0, z: 0}, _ShadowTweenScale={x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}, _ShadowTweenVector={x: 5, y: 12, z: 0}, {x: 0, y: 0, z: 0}

### `State 56`

- 动作：ActivateGameObject, ActivateGameObject, ActivateGameObject, ActivateGameObject, ActivateGameObject, BoolTest

### `State 57`

- 动作：SetProperty, SetScale, SetVector3XYZ, SetVector3XYZ, SetAnimatorLayerWeight, SetAnimatorFloat, SetProperty, SetProperty, SetProperty
- 跳转：FINISHED → State 56
- 参数：color, -2.2, 2.2, -1, 1, -5, 8, 1, 2, 1, {x: 0, y: 0, z: 0}, _ShadowTweenScale={x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}, _ShadowTweenVector={x: 5, y: 12, z: 0}, {x: 0, y: 0, z: 0}

### `State 58`

- 动作：SetProperty, SetScale, SetVector3XYZ, SetVector3XYZ, SetAnimatorLayerWeight, SetAnimatorFloat, SetProperty, SetProperty, SetProperty, SetProperty
- 跳转：FINISHED → State 56
- 参数：color, -0.8, 0.8, -1, 1, -3, -1, 1, 2, 1, {x: 0, y: 0, z: 0}, _ShadowTweenScale={x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}, _ShadowTweenVector={x: 5, y: 12, z: 0}, {x: 0, y: 0, z: 0}

### `State 59`

- 动作：iTweenMoveTo, SetAnimatorFloat, GetScale, EaseFloat, EaseFloat, SetScale, DOTweenTransformScale
- 参数：color, _ShadowDampTime=0.5, 1, _ShadowDampTime=0.5, _ShadowScaleTo=1, _ShadowDampTime=0.5, 1, _ShadowDampTime=0.5, 1, 1, _ShadowTweenVector={x: 5, y: 12, z: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}

### `State 60`


### `State 61`

- 动作：RunFSM
- 跳转：FINISHED → Walking

### `State 54`


### `State 62`

- 动作：SetDataInt, AnimatorPlay, AnimatorPlay, AudioStop, ActivateGameObject
- 跳转：FINISHED → State 42
- 参数：spine_1, Mount_Kill, Mounted_Dead, 1

### `State 63`

- 动作：ActivateGameObject, AnimatorPlay
- 跳转：Step → State 64
- 参数：Step, 1

### `State 64`

- 动作：RunFSM, GetAnimatorCurrentStateInfoIsTag
- 参数：Idle

### `State 65`

- 动作：SendRandomEvent
- 跳转：FINISHED → JumpAttack
- 参数：0.7, 0.3

### `State 66`

- 动作：Wait
- 跳转：FINISHED → State 67
- 参数：1

### `State 67`

- 动作：DestroyObject
- 参数：{fileID: 1709254077376921}

### `Walk 2`

- 动作：ActivateGameObject, ActivateGameObject, AnimatorPlay
- 跳转：FINISHED → State 68
- 参数：Run_Start, 1, 1

### `Walking 2`

- 动作：GetScale, FloatOperator, SetVelocity2dChronos, GameObjectIsNull, Wait
- 跳转：FINISHED → State 69
- 参数：27, 1.5, {x: 0, y: 0}, {x: 0, y: 0, z: 0}

### `State 68`

- 动作：RunFSM
- 跳转：FINISHED → RunUp

### `State 69`

- 动作：AnimatorPlay, ChronosWait
- 参数：Run_End, 0.5

### `RunUp`

- 动作：GetScale, FloatOperator, SetVelocity2dChronos, GameObjectIsNull, Wait
- 跳转：FINISHED → Walking 2
- 参数：0.6, {x: 0, y: 0}, {x: 0, y: 0, z: 0}

### `State 72`

- 动作：iTweenMoveTo
- 参数：0.33, 1, Offset={x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}

### `State 73`

- 动作：SetTag, SetTag, SetBoolValue, SendEventByName, ActivateGameObject, ActivateGameObject, AudioStop
- 参数：Untagged, Untagged, V_MountDead, 1

### `State 74`

- 动作：RunFSM, NextFrameEvent
- 跳转：FINISHED → MovementLoop

### `State 75`

- 动作：AudioPlay, Wait
- 跳转：FINISHED → State 3
- 参数：{fileID: 8300000, guid: 4a4b0dd430f53cc41a25da973ce38240, type: 3}, 1.5

### `State 76`

- 动作：GetFsmFloat, FloatSubtract, SetFsmFloat
- 跳转：FINISHED → State 41
- 参数：Life, Life, Life, Life, 10

### `State 77`

- 动作：ActivateGameObject, ActivateGameObject, GetFsmGameObject, GameObjectIsNull, GameObjectCompare, AnimatorPlay
- 跳转：Next → State 78
- 参数：{fileID: 1618682377417806}, ENEMYCHECKER, ATTACKER, Idle_Caution, 1, 1

### `State 78`

- 动作：SetFsmGameObject, NextFrameEvent
- 参数：ENEMYCHECKER, ATTACKER

### `State 79`

- 动作：SetFsmGameObject
- 参数：ENEMYCHECKER, ATTACKER

### `Walk 3`

- 动作：ActivateGameObject, ActivateGameObject, SetBoolValue, AnimatorPlay
- 跳转：FINISHED → Walking 3
- 参数：Walk, 1, 1, 1

### `Walking 3`

- 动作：GetScale, FloatOperator, SetVelocity2dChronos, GameObjectIsNull, Wait
- 跳转：FINISHED → Walking 3
- 参数：7, 2, {x: 0, y: 0}, {x: 0, y: 0, z: 0}

### `State 80`

- 动作：Wait, GetPosition2d, SetPosition, SendEventByName
- 跳转：FINISHED → State 81
- 参数：V_Hyena_17, 2, {x: 0, y: 0}, {x: 0, y: 0, z: 0}

### `State 81`

- 动作：SendEventByName, GetPosition2d, SetPosition
- 参数：ViviH, {x: 0, y: 0}, {x: 0, y: 0, z: 0}

## 子 FSM（全量）

### `_HyenaParent` / FSM（起始 State 1，3 状态）

- `State 1`
- `State 2`　动作：ActivateGameObject, ActivateGameObject, ActivateGameObject, SetPosition, SetPosition, SetPosition, SetScale, SetScale, SetScale, SetTag, SetTag, PC2DRemoveCameraTarget, NextFrameEvent　跳转：FINISHED → State 3　参数：{fileID: 1161607090078389}, Enemy, Enemy, XSCALE=-1, 1, XSCALE=-1, 1, XSCALE=-1, 1, 1, 1, 1, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}
- `State 3`

### `CollisionShieldRight` / FSM（起始 State 1，1 状态）

- `State 1`　动作：GetPosition2d, SetPosition　参数：{x: 0, y: 0}, {x: 0, y: 0, z: 0}

### `CollisionShieldLeft` / FSM（起始 State 1，1 状态）

- `State 1`　动作：GetPosition2d, SetPosition　参数：{x: 0, y: 0}, {x: 0, y: 0, z: 0}

### `_Hyena` / StunGauge（起始 State 1，17 状态）

- `Watch`　动作：BoolTest, FloatCompare, ChronosWait　跳转：Break → Break ； CritBreak → Break2 ； Heal → Regen　参数：StunGauge=12, 1
- `Regen`　动作：FloatAdd, FloatClamp　跳转：FINISHED → Watch　参数：StunGauge=12, 1, StunGauge=12, MaxStunGauge=12
- `Break`　动作：RunFSM, SendEventByName, CreateObject, SetParent, SetFsmGameObject, SetAnimatorTrigger, SendEventByName, SendEventByName, CreateObject, AudioPlay, ActivateGameObject, ActivateGameObject, FloatAdd　跳转：Break_Heal → Heal　参数：{fileID: 1709254077376921, guid: aa0c91b810e9c7748a0d5d8896b255c7, type: 2}, {fileID: 1884547391421755}, {fileID: 1884547391421755}, {fileID: 1618682377417806}, {fileID: 1709254077376921, guid: f6d73520d17afb24dbab8ccde7aa7f87, type: 2}, {fileID: 1618682377417806}, Stun, FSM, Parent, break, BREAK, BREAK, {fileID: 8300000, guid: bcfdfca459889ec4b9509ab0c4a6ad30, type: 3}, MaxStunGauge=12, 2, {x: 0, y: 3, z: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 2, z: 0}, {x: 0, y: 0, z: 0}
- `Init`　动作：SetFloatValue　跳转：FINISHED → Watch　参数：StunGauge=12, MaxStunGauge=12
- `Heal`　动作：DestroyObject, ActivateGameObject, ActivateGameObject, SendEventByName, SendEvent　参数：RESET
- `State 1`　动作：BoolTest, FloatCompare　跳转：EASY → State 2 ； NORMAL → State 3 ； HARD → State 4　参数：Difficulty=1, 1
- `State 2`　动作：SetFloatValue　跳转：FINISHED → Init　参数：MaxStunGauge=12, 80
- `State 3`　动作：SetFloatValue　跳转：FINISHED → Init　参数：MaxStunGauge=12, 90
- `State 4`　动作：SetFloatValue　跳转：FINISHED → Init　参数：MaxStunGauge=12, 100
- `State 6`　动作：GameObjectIsNull　跳转：Next → State 7
- `State 7`　动作：DestroyObject
- `State 5`
- `State 8`　动作：FloatCompare　跳转：Refresh → State 9 ； FINISHED → Watch　参数：StunGauge=12
- `State 9`　动作：SetFloatValue　跳转：FINISHED → Watch　参数：StunGauge=12, MaxStunGauge=12
- `State 10`　动作：SetFloatValue　跳转：FINISHED → Init　参数：MaxStunGauge=12, 1000
- `Break2`　动作：RunFSM, SetBoolValue, CreateObject, SetParent, SetFsmGameObject, SetAnimatorTrigger, SendEventByName, SendEventByName, CreateObject, AudioPlay, ActivateGameObject, ActivateGameObject, FloatAdd　跳转：Break_Heal → Heal 2　参数：{fileID: 1709254077376921, guid: aa0c91b810e9c7748a0d5d8896b255c7, type: 2}, {fileID: 1884547391421755}, {fileID: 1884547391421755}, {fileID: 1618682377417806}, {fileID: 1709254077376921, guid: f6d73520d17afb24dbab8ccde7aa7f87, type: 2}, {fileID: 1618682377417806}, FSM, Parent, break, BREAK, BREAK, {fileID: 8300000, guid: bcfdfca459889ec4b9509ab0c4a6ad30, type: 3}, MaxStunGauge=12, 2, {x: 0, y: 3, z: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 2, z: 0}, {x: 0, y: 0, z: 0}
- `Heal 2`　动作：DestroyObject, ActivateGameObject, ActivateGameObject, SendEventByName, SendEvent　参数：RESET

### `_Hyena` / Life（起始 State 1，29 状态）

- `State 1`　动作：SetTag, SetTag, SetFloatValue, NextFrameEvent　跳转：FINISHED → State 15　参数：Enemy, Enemy, Life=30, 30
- `Calculate`　动作：FloatAdd, FsmFloatAdd, FloatAbs, SetFsmBool, RunFSM, RunFSM　跳转：FINISHED → State 16　参数：StunGauge, StunGauge, AI, Detected, Life=30, 1
- `LifeCheck`　动作：FloatCompare, SendEventByName, SetStringValue, FloatInterpolateChronos, SetAnimatorLayerWeight　跳转：Dead → State 22 ； FINISHED → State 14　参数：Life=30, 1, 0.5, 1
- `State 2`　动作：SendEventByName, SetAnimatorLayerWeight, SetAnimatorLayerWeight, AudioPlay, AudioPlay, CreateObject, ActivateGameObject, ActivateGameObject, ActivateGameObject, ActivateGameObject, SendEventByName, SendEventByName, SendEventByName, GetPosition2d, GetPosition2d, AnimatorPlay, FloatCompare　跳转：FleeLeft → State 7 ； FleeRight → State 8　参数：{fileID: 1709254077376921, guid: 9a5f13c3fea8c8c4cb67dfec6c40748b, type: 2}, {fileID: 1618682377417806}, REMOVE, BREAK, DEAD, DEAD, Hit_Blow1, {fileID: 8300000, guid: 917ea4e9a53c05f48b008d841b0db86d, type: 3}, {fileID: 8300000, guid: 762f3a4c4b1d2e144b0987fb22d8ae35, type: 3}, 1, 1, 3, 1, 1, {x: 0, y: 0}, {x: 0, y: 0}, {x: 0, y: 5, z: 0}, {x: 0, y: 0, z: 0}
- `State 3`　动作：SetScale, AnimatorPlay, SetVelocity2dChronos, ChronosWait　跳转：FINISHED → State 4　参数：Flee, -1, -6, 2.433, {x: 0, y: 0}, {x: 0, y: 0, z: 0}
- `State 4`　动作：RunFSM, ActivateGameObject, DestroyObject　参数：{fileID: 1709254077376921}
- `State 5`　动作：GetPosition2d, GetPosition2d, FloatCompare　跳转：FleeLeft → State 3 ； FleeRight → State 6　参数：{x: 0, y: 0}, {x: 0, y: 0}
- `State 6`　动作：SetScale, AnimatorPlay, SetVelocity2dChronos, ChronosWait　跳转：FINISHED → State 4　参数：Flee, 1, 6, 2.433, {x: 0, y: 0}, {x: 0, y: 0, z: 0}
- `State 7`　动作：SetVelocity2dChronos, AddForce2d, SetScale, ChronosWait　跳转：FINISHED → State 5　参数：-20, 20, 1, 2.15, {x: 0, y: 0}, {x: 0, y: 0}, {x: 0, y: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}
- `State 8`　动作：SetVelocity2dChronos, AddForce2d, SetScale, ChronosWait　跳转：FINISHED → State 5　参数：20, 20, -1, 2.15, {x: 0, y: 0}, {x: 0, y: 0}, {x: 0, y: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}
- `Counter`　动作：AudioPlay, FloatAdd, SetFsmBool, FsmFloatAdd, SendEventByName, FloatAbs, SetFsmBool, RunFSM, RunFSM, RunFSM　跳转：FINISHED → State 16　参数：StunGauge, CritBreak, StunGauge, StunGauge, CriticalBreak, AI, Detected, {fileID: 8300000, guid: cc4d9da621ea5644dafdd407855b0edf, type: 3}, Life=30, -150, 1, 1
- `State 9`　动作：StringSwitch　跳转：FINISHED → Calculate ； Counter → State 19　参数：Melee1, JumpAttack, Slide_Attack
- `State 10`　动作：BoolTest　跳转：FINISHED → Calculate ； CounterCheck → State 9
- `State 11`　动作：SetFloatValue　参数：Life=30, 100
- `State 12`　动作：SetFloatValue　参数：Life=30, 150
- `State 13`　动作：SetFloatValue　参数：Life=30, 180
- `State 14`
- `State 15`　动作：FloatCompare　跳转：EASY → State 11 ； NORMAL → State 12 ； HARD → State 13　参数：Difficulty=1, 1
- `State 16`　动作：StringSwitch, StringCompare　跳转：Particle → State 17 ； GoCheck → LifeCheck　参数：CC, Melee
- `State 17`　动作：CreateObject　跳转：FINISHED → LifeCheck　参数：{fileID: 1709254077376921, guid: a5a612f286eb8af43b8f4928911275ec, type: 2}, {fileID: 1618682377417806}, {x: 0, y: 3, z: 0}, {x: 0, y: 0, z: 0}
- `State 18`　动作：StringSwitch　跳转：BlowAway → State 39　参数：BlowAway
- `State 39`　动作：SendEventByName, SetStringValue, SendEvent　参数：BLOWAWAY
- `State 19`　动作：ActivateGameObject, FloatSwitch　跳转：Stun → State 20 ； 10x → State 21　参数：Difficulty=1, 1, 2, 3, 1
- `State 20`　动作：FloatMultiply　跳转：FINISHED → HitStop　参数：2
- `State 21`　动作：FloatMultiply　跳转：FINISHED → HitStop　参数：10
- `HitStop`　动作：FsmFloatAdd, CreateObject, GetFsmFloat, FloatCompare　跳转：FINISHED → Counter　参数：{fileID: 1709254077376921, guid: c58a0614152bf5f4599fe0af053327b5, type: 2}, {fileID: 1618682377417806}, FSM, StopTime, FSM, StopTime, 0.25, {x: 0, y: 2, z: 0}, {x: 0, y: 0, z: 0}
- `State 22`　动作：CreateObject, SetLayer, SetTag, SetTag, GetParent, GameObjectIsNull, GameObjectIsNull　跳转：FINISHED → State 2 ； Count → State 24　参数：{fileID: 1709254077376921, guid: 5f574cb1ba0fe7c4aa2e60ce85522885, type: 2}, {fileID: 1618682377417806}, Untagged, Untagged, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}
- `State 23`　动作：FsmFloatAdd, AddToFsmInt　跳转：FINISHED → State 2　参数：FSM, Count, FSM, KillCount, 1, 1
- `State 24`　动作：NextFrameEvent　跳转：FINISHED → State 23

### `_Hyena` / H_SE（起始 State 1，28 状态）

- `State 1`
- `State 2`　动作：AudioPlay　参数：{fileID: 8300000, guid: da6e31fe432d9674da00e20ddd1ba3b5, type: 3}, 1
- `State 3`　动作：AudioPlay　参数：{fileID: 8300000, guid: 6b04ff84d729d1849831ef1174206ded, type: 3}, 1
- `State 4`　动作：AudioPlay　参数：{fileID: 8300000, guid: 90194e0997c916d40ba6d9a22489b81d, type: 3}, 1
- `State 5`　动作：AudioPlay　参数：{fileID: 8300000, guid: 3f52649540cd8b543a703f2f67cad623, type: 3}, 1
- `State 6`　动作：AudioPlay　参数：{fileID: 8300000, guid: def4c1fee2c269c42b93ca899f08953a, type: 3}, 1
- `State 7`　动作：AudioPlay　参数：{fileID: 8300000, guid: 659400934a074ab489986cb4a4db6182, type: 3}, 1
- `State 8`　动作：AudioPlay　参数：{fileID: 8300000, guid: 835fff32610048243bffe672282942a4, type: 3}, 1
- `State 9`　动作：AudioPlay　参数：{fileID: 8300000, guid: bb31049e66bf2c0459e5d28f76a250b7, type: 3}, 1
- `State 10`　动作：AudioPlay　参数：{fileID: 8300000, guid: bfd99acc839833e4e8af3efc9b7ba8ca, type: 3}, 1
- `State 11`　动作：AudioPlay　参数：{fileID: 8300000, guid: 0c9029b09cbedb847a01b565a619f85c, type: 3}, 1
- `State 12`　动作：AudioPlay　参数：{fileID: 8300000, guid: aad188f2caa11cf40a7e55b2d19e685f, type: 3}, 1
- `State 13`　动作：AudioPlay　参数：{fileID: 8300000, guid: e2f9abecfef74574c830a0d83c3bf605, type: 3}, 1
- `State 14`　动作：AudioPlay　参数：{fileID: 8300000, guid: a8faf913cdc614a409f173366bf3e9da, type: 3}, 1
- `State 15`　动作：AudioPlay　参数：{fileID: 8300000, guid: 1e8d24f84ee31f349a4fa08c35a0b6db, type: 3}, 1
- `State 16`　动作：AudioPlay　参数：{fileID: 8300000, guid: 24665d9b9014eaa46a08107ae2906ad8, type: 3}, 1
- `State 17`　动作：AudioPlay　参数：{fileID: 8300000, guid: 1b5680a7816257149ad02ce259d0f813, type: 3}, 1
- `State 18`　动作：AudioPlay　参数：{fileID: 8300000, guid: bfd99acc839833e4e8af3efc9b7ba8ca, type: 3}, 1
- `State 19`　动作：AudioPlay　参数：{fileID: 8300000, guid: 7ed34efcf1af4cf4188457cae04924f1, type: 3}, 1
- `State 20`　动作：AudioPlay, GetScale, FloatMultiply, SetVector3XYZ, CreateObject　参数：{fileID: 1709254077376921, guid: d09569d2b5cde9342bcba3d3ebb1d251, type: 2}, {fileID: 1689364072382846}, {fileID: 8300000, guid: d3342644abd56d14598ccb4fb39ae829, type: 3}, -90, -55, 90, 1, {x: 0, y: 0, z: 0}, SpawnRotation={x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}, SpawnRotation={x: 0, y: 0, z: 0}
- `State 21`　动作：AudioPlay　参数：{fileID: 8300000, guid: d4fd2664a54edf848a111a1283d10171, type: 3}, 1
- `State 22`　动作：AudioPlay, CreateObject　参数：{fileID: 1709254077376921, guid: d09569d2b5cde9342bcba3d3ebb1d251, type: 2}, {fileID: 1689364072382846}, {fileID: 8300000, guid: b75f70324d11ea241ba754ef74d65db3, type: 3}, 1, {x: 0, y: 0, z: 0}, SpawnRotation={x: 0, y: 0, z: 0}
- `State 23`　动作：AudioPlay, CreateObject　参数：{fileID: 1709254077376921, guid: d09569d2b5cde9342bcba3d3ebb1d251, type: 2}, {fileID: 1689364072382846}, {fileID: 8300000, guid: d6612843858a7f4469d0c222b26488ad, type: 3}, 1, {x: 0, y: 0, z: 0}, SpawnRotation={x: 0, y: 0, z: 0}
- `State 24`　动作：AudioPlay, CreateObject　参数：{fileID: 1709254077376921, guid: d09569d2b5cde9342bcba3d3ebb1d251, type: 2}, {fileID: 1689364072382846}, {fileID: 8300000, guid: 9401196e0d574c642bf78ec44ac46e44, type: 3}, 1, {x: 0, y: 0, z: 0}, SpawnRotation={x: 0, y: 0, z: 0}
- `State 25`　动作：AudioPlay　参数：{fileID: 8300000, guid: e9e044d1f0d61714795beb12c6a52abc, type: 3}, 1
- `State 26`　动作：AudioPlay　参数：{fileID: 8300000, guid: def4c1fee2c269c42b93ca899f08953a, type: 3}, 1
- `State 27`　动作：AudioPlay　参数：{fileID: 8300000, guid: b428ae0e7d1b24d4499fe51b0ddde37a, type: 3}, 1
- `State 28`

### `_Hyena` / Flinching（起始 State 1，12 状态）

- `State 1`　动作：SetBoolValue, SetFloatValue, FloatCompare　跳转：CanFlinch → State 4 ； Up → State 2 ； Down → State 3　参数：1
- `State 2`　动作：AnimatorPlay, SetBoolValue, SendEventByName　跳转：FINISHED → State 11　参数：Hit_Up1, FLINCH, 1
- `State 3`　动作：AnimatorPlay, SetBoolValue, SendEventByName　跳转：FINISHED → State 11　参数：Hit_Down1, FLINCH, 1
- `State 4`　动作：ChronosWait　跳转：Up → State 2 ； Down → State 3 ； FINISHED → State 7　参数：0.1
- `State 5`　跳转：ArmorEnd → State 1 ； ArmorBreak → State 6
- `State 6`
- `State 7`　动作：FloatSubtract, FloatCompare　跳转：Zero → State 8 ； FINISHED → State 4　参数：1
- `State 8`　动作：BoolTest　跳转：GoArmor → State 9
- `State 9`　动作：CreateObject, SetAnimatorLayerWeight, AnimatorPlay, AnimatorPlay, SendEventByName, SendEventByName, AudioPlay, RunFSM, RunFSM　跳转：ColorOff → State 10　参数：{fileID: 1709254077376921, guid: 52f22918d7c9186439587293a58b82bb, type: 2}, {fileID: 1618682377417806}, SuperArmor, Color_Red, FLINCHRESET, Return, {fileID: 8300000, guid: 4a4b0dd430f53cc41a25da973ce38240, type: 3}, 1, 3, 3, 1, {x: 0, y: 2, z: 0}, {x: 0, y: 0, z: 0}
- `State 10`　动作：SetAnimatorLayerWeight　参数：3
- `State 11`　动作：SetFloatValue, FloatMultiply, FloatDivide, FloatAdd　跳转：FINISHED → State 4　参数：5, 2
- `State 13`

### `_Hyena` / TimeControl（起始 State 1，4 状态）

- `State 1`
- `State 2`　动作：ScaleClockTime, Wait　跳转：FINISHED → State 4　参数：0.2, 1
- `State 3`　动作：ScaleClockTime, Wait　跳转：FINISHED → State 4　参数：0.2, 1
- `State 4`　动作：ScaleClockTime　参数：1

### `HitBox` / FSM（起始 State 1，4 状态）

- `State 1`
- `Calculate`　动作：FsmFloatAdd, FsmFloatAdd, FloatAbs, SetFsmBool, RunFSM, RunFSM　跳转：FINISHED → LifeCheck　参数：Life, Life, StunGauge, StunGauge, AI, Detected, 1
- `LifeCheck`　动作：SendEventByName, DOTweenTransformShakeScale　跳转：FINISHED → State 2　参数：LifeCheck, 90, 0.1, 10, 1, {x: 0.05, y: 0.05, z: 0}
- `State 2`　动作：SetScale　参数：1, 1, {x: 0, y: 0, z: 0}

### `HitBox_WeakPoint` / FSM（起始 State 1，4 状态）

- `State 1`
- `Calculate`　动作：FloatMultiply, FloatMultiply, FsmFloatAdd, FsmFloatAdd, FloatAbs, SetFsmBool, AudioPlay, RunFSM　跳转：FINISHED → LifeCheck　参数：Life, Life, StunGauge, StunGauge, AI, Detected, {fileID: 8300000, guid: e9227a0f998f0864ab7e908a107098ee, type: 3}, 1.25, 1.5, 1
- `LifeCheck`　动作：SendEventByName, DOTweenTransformShakeScale　跳转：FINISHED → State 2　参数：LifeCheck, 90, 0.1, 10, 1, {x: 0.05, y: 0.05, z: 0}
- `State 2`　动作：SetScale　参数：1, 1, {x: 0, y: 0, z: 0}

### `Sight` / FSM（起始 Sight，3 状态）

- `Sight`　动作：Trigger2dEvent　跳转：Spot → State 2　参数：PlayerPresence
- `State 2`　动作：SetFsmBool, SetAnimatorBool, AudioPlay　跳转：FINISHED → State 1　参数：AI, Spot, caution, {fileID: 8300000, guid: aa383ae043c57db40baab17be02440ad, type: 3}, 1, 1, 1
- `State 1`

### `BackSight` / FSM（起始 Sight，3 状态）

- `Sight`　动作：Trigger2dEvent　跳转：Spot → State 2　参数：PlayerPresence
- `State 2`　动作：SetFsmBool　跳转：FINISHED → State 1　参数：AI, Alert, 1
- `State 1`

### `LedgeChecker` / FSM（起始 Serching，3 状态）

- `Serching`　动作：Trigger2dEvent, Trigger2dEvent, SetFsmGameObject　参数：Untagged, Enemy, Movement, Ledge
- `OnTheLedge`　动作：GetScale, GetScale, FloatCompare, SetFsmGameObject, Trigger2dEvent　跳转：FINISHED → State 1　参数：Movement, Ledge, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}
- `State 1`

### `JumpAttack_Range` / FSM（起始 Wait，2 状态）

- `Wait`　动作：Trigger2dEvent, Trigger2dEvent　跳转：InRange → State 1　参数：PlayerPresence, PlayerPresence
- `State 1`　动作：SendEventByName　参数：Jump_Attack

### `JumpAttack_HitBox` / FSM（起始 Wait，11 状态）

- `Wait`　动作：ScaleGlobalClockTime, Trigger2dEvent, Trigger2dEvent, Trigger2dEvent, Trigger2dEvent　跳转：Dodged → Dodged ； JAttack_End → State 7　参数：Root, Player, Player, PlayerDodge, PlayerDodge, 1
- `Slow`　动作：SetFsmFloat, SendEventByName, Wait　跳转：FINISHED → Detect　参数：FSM, SlowTime, Slow, 0.18, 0.18
- `Detect`　动作：ScaleGlobalClockTime, Trigger2dEvent, Trigger2dEvent, Trigger2dEvent, Trigger2dEvent, Wait　跳转：Dodged → Dodged　参数：Root, Player, Player, PlayerDodge, PlayerDodge, 1, 0.1
- `State 2`　动作：ActivateGameObject, ScaleGlobalClockTime　参数：Root, 1
- `Dodged`　动作：SetFsmGameObject, ActivateGameObject, SendEventByName, SetFsmFloat, AnimatorPlay, GetParent, SendEventByName, RunFSM, SetFsmBool, GetAnimatorCurrentStateInfoIsName, CircleCast2d　跳转：FINISHED → State 1 ； OnGround → State 5　参数：ENEMYCHECKER, ATTACKER, ArmorEnd, Flinching, FlinchTime, Jump_Attack_Dodged, Evade, Life, Jumping, Idle_Caution, 30, 1, 0.5, 1, 9, 1, {x: 0, y: 0}, {x: 0, y: -1}, {x: 0, y: 0}, {x: 0, y: 0}, {r: 1, g: 0.92156863, b: 0.015686275, a: 1}
- `State 1`　动作：ActivateGameObject, ActivateGameObject, SendEventByName　跳转：FINISHED → Wait　参数：RESET, 1, 1
- `State 3`　动作：ScaleGlobalClockTime　参数：Root, 1
- `State 4`　动作：ScaleGlobalClockTime, SetFsmGameObject, SendEventByName, GetParent, SetFsmGameObject, ActivateGameObject　参数：{fileID: 1618682377417806}, Root, AI, playerHitbox, Hit, FSM, LockingObject, 1
- `State 5`　动作：AudioPlay, GetVelocity2d, FloatCompare, GetAnimatorCurrentStateInfoIsName, GetScale, CreateObject, SetScale, Wait　跳转：FINISHED → State 1 ； Stop → State 6　参数：{fileID: 1709254077376921, guid: a83f67ccc06f521449b5c97e8832892b, type: 2}, {fileID: 1618682377417806}, Idle_Caution, {fileID: 8300000, guid: 92d44b9852bc00c4c93685f382303848, type: 3}, 1, 0.2, 1, {x: 0, y: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}, {x: 0, y: 0, z: 0}
- `State 6`　动作：GetAnimatorCurrentStateInfoIsName　跳转：FINISHED → State 1　参数：Idle_Caution
- `State 7`

### `ClawAttack_Range` / FSM（起始 Wait，2 状态）

- `Wait`　动作：Trigger2dEvent, Trigger2dEvent　跳转：InRange → State 1　参数：PlayerPresence, PlayerPresence
- `State 1`　动作：SendEventByName　参数：CLAW

### `ClawAttack_HitBox` / FSM（起始 Wait，7 状态）

- `Wait`　动作：Trigger2dEvent, Trigger2dEvent, Trigger2dEvent, Trigger2dEvent　跳转：Trigger → Triggered ； StopTrigger → State 2 ； Claw_End → State 2　参数：Player, Player, PlayerDodge, PlayerDodge
- `Triggered`　动作：GetParent, GetFsmBool, BoolTest, SetFsmFloat, SendEventByName, Wait　跳转：FINISHED → Detect　参数：FSM, EvadeVertical, FSM, SlowTime, Slow, 0.15, 0.15
- `Detect`　动作：ScaleGlobalClockTime, GetFsmBool, BoolTest, Trigger2dEvent, Trigger2dEvent, Trigger2dEvent, Trigger2dEvent, Wait　跳转：FINISHED → State 2　参数：Root, FSM, EvadeVertical, Player, Player, PlayerDodge, PlayerDodge, 1, 0.1
- `State 1`　动作：GetParent, RunFSM, AudioPlay　参数：{fileID: 8300000, guid: 840b7986f965e7a4ca4d887dee0af200, type: 3}
- `State 2`　动作：ChronosWait　跳转：FINISHED → Wait　参数：1
- `State 3`　动作：ChronosWait, GetParent, RunFSM　参数：1
- `State 4`　动作：SendEventByName　参数：Evade

### `PutMeInCamera` / Camera（起始 Idle，6 状态）

- `Idle`　动作：Trigger2dEvent, Trigger2dEvent　跳转：Detect → State 2　参数：PlayerPresence, PlayerPresence
- `State 2`　动作：RunFSM, Trigger2dEvent　跳转：OutofSight → State 5　参数：PlayerPresence
- `State 1`　动作：RunFSM　跳转：FINISHED → State 4
- `State 3`　动作：PC2DRemoveCameraTarget, RunFSM　参数：0.5
- `State 4`　动作：Wait　跳转：FINISHED → Idle　参数：0.1
- `State 5`　动作：Wait　跳转：FINISHED → State 1　参数：0.1

### `BackSight2` / FSM（起始 Sight，3 状态）

- `Sight`　动作：Trigger2dEvent　跳转：Spot → State 2　参数：Player
- `State 2`　动作：SetFsmBool, SetAnimatorBool　跳转：FINISHED → State 1　参数：AI, Roaming, caution, 1, 1
- `State 1`

