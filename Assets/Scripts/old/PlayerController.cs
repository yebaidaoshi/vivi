using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("移动参数")]
    [SerializeField] public float moveSpeed = 8f;
    [SerializeField] float jumpForce = 12f;
    [SerializeField] private GroundDetector groundDetector;   //[SerializeField]让private字段在Inspector中可见，方便调试
    private Rigidbody2D rb;
    private float moveInput;
    private bool isJumping;          // 是否正在跳跃（用于变跳）

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        // 如果没有手动拖拽，自动在子物体里找
        if (groundDetector == null)
            groundDetector = GetComponentInChildren<GroundDetector>();
    }

    void Update()
    {
        // ========== 1. 获取水平移动输入（键盘 A/D 或 左右箭头） ==========
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            // 计算水平值：右箭头或D键按了为1，左箭头或A键按了为-1，同时按则抵消为0
            float right = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f;
            float left = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f;
            moveInput = right - left; // 结果为 -1, 0, 或 1（等同于 GetAxisRaw）
        }


        // ----- 输入获取 -----

        if (keyboard != null)
        {
            float right = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f;
            float left = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f;
            moveInput = right - left;
        }

        // ----- 跳跃：按空格且在地面时起跳 -----
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame && groundDetector.IsGrounded)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
        }

        // ========== 3. 角色翻转（根据移动方向） ==========
        if (moveInput > 0.01f)  // 向右移动
        {
            transform.localScale = new Vector3(1, 1, 1);
        }
        else if (moveInput < -0.01f)  // 向左移动
        {
            transform.localScale = new Vector3(-1, 1, 1);
        }
        // 如果 moveInput == 0，保持当前朝向不变（不翻转）
    }


    void FixedUpdate()
    {
        // ----- 水平移动 -----
        rb.velocity = new Vector2(moveInput * moveSpeed, rb.velocity.y);
    }

}
