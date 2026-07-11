using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStateMachine : MonoBehaviour
{
    // 当前状态
    private PlayerState currentState;
    // 所有状态
    public PlayerIdleState IdleState { get; private set; }
    public PlayerRunState RunState { get; private set; }
    public PlayerAttackState AttackState { get; private set; }
    public PlayerJumpState JumpState { get; private set; }
    public Rigidbody2D rb;
    public Moren inputActions;
    public SkeletonAnimation skeletonAnim;
    public bool isRunningJump = false;     // 是否跑步起跳
    public bool playLandingToRun = false;  // 是否播放 Landing_to_Run
    public float moveSpeed = 5f;// 移动速度
    public float jumpForce = 8f;// 跳跃力度
    public int maxJumpCount = 1;  // 最大跳跃次数
    public int currentJumpCount  = 0;
    public int facingDirection = 1;   // 1=右，-1=左 角色翻转


    [Header("地面检测")]
    public Transform groundCheck; // 地面检测点
    public LayerMask groundLayer;   // 地面图层
    public float checkRadius = 0.2f;
    public bool IsGrounded()
    {
        return Physics2D.OverlapCircle(groundCheck.position, checkRadius, groundLayer);
    }


    public void Awake()
    {
        // 游戏启动时运行一次
        IdleState = new PlayerIdleState(this);
        RunState = new PlayerRunState(this);
        AttackState= new PlayerAttackState(this);
        JumpState = new PlayerJumpState(this);
        inputActions = new Moren();
        rb = GetComponent<Rigidbody2D>();
        skeletonAnim = GetComponent<SkeletonAnimation>();
    }

    private void Start()
    {
        maxJumpCount = 1;
        var animStateData = skeletonAnim.AnimationState.Data;

        float mixTime = 0.1f;

        // 跑步 ↔ 跳跃起跳
        animStateData.SetMix("Run", "Jump/Jump_Forward", mixTime);
        animStateData.SetMix("Run", "Jump/Jump_Backward", mixTime);
        animStateData.SetMix("Jump/Jump_Forward", "Run", mixTime);
        animStateData.SetMix("Jump/Jump_Backward", "Run", mixTime);

        // 跑步 ↔ 落地
        animStateData.SetMix("Run", "Landing", mixTime);
        animStateData.SetMix("Landing", "Run", mixTime);
        animStateData.SetMix("Landing", "Idle", mixTime);
        animStateData.SetMix("Run", "Idle", mixTime);

        // 空中循环 ↔ 跑步 / 落地
        animStateData.SetMix("Jump/Jump_OnAir_Forward", "Run", mixTime);
        animStateData.SetMix("Jump/Jump_OnAir_Backward", "Run", mixTime);
        animStateData.SetMix("Jump/Jump_OnAir_Forward", "Landing", mixTime);
        animStateData.SetMix("Jump/Jump_OnAir_Backward", "Landing", mixTime);

        // 跳跃相关
        animStateData.SetMix("Jump/Jump", "Jump/Jump_OnAir", mixTime);
        animStateData.SetMix("Jump/Jump_Forward", "Jump/Jump_OnAir_Forward", mixTime);
        animStateData.SetMix("Jump/Jump_Backward", "Jump/Jump_OnAir_Backward", mixTime);
        animStateData.SetMix("Jump/Jump_OnAir_Forward", "Jump/Jump_OnAir_Backward", mixTime);
        animStateData.SetMix("Jump/Jump_OnAir_Forward", "Jump/Jump_OnAir", mixTime);
        animStateData.SetMix("Jump/Jump_OnAir_Backward", "Jump/Jump_OnAir", mixTime);

        // 跑步启动与循环
        animStateData.SetMix("Run_Start", "Run", mixTime);
        animStateData.SetMix("Run", "Run_Start", mixTime);

        // 转向
        animStateData.SetMix("Run_Turning", "Run", mixTime);
        animStateData.SetMix("Run_Turning", "Run_Start", mixTime);

        // ★ 默认混合（兜底）
        animStateData.DefaultMix = mixTime;



        ChangeState(IdleState); // 游戏对象激活时运行一次
    }

    private void Update()
    {
        // 每帧运行一次
        currentState?.Update();
    }

    void OnEnable() => inputActions.Enable();//角色激活时监听输入
    void OnDisable() => inputActions.Disable();//角色禁用时停止监听输入

    public void ChangeState(PlayerState newState)
    {
        // 切换到状态
        if (currentState == newState)
            return;

        currentState?.Exit();  // 退出当前状态
        currentState = newState;
        currentState.Enter();  // 进入新状态
    }
    public float GetAnimationDuration(string animName)
    {
        var anim = skeletonAnim.Skeleton.Data.FindAnimation(animName);
        return anim != null ? anim.Duration : 0.5f; // 找不到则默认 0.5 秒
    }
}
