using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PhysicsCheck : MonoBehaviour
    //这个脚本名称意为物理检测
    //这个脚本的作用是检测角色是否在地面上，以便限制跳跃次数等功能
{    //限制跳跃次数
    [Header("状态")]
    public bool onGround;
    [Header("检测地面相关参数")]
    public float checkRadius;

    public LayerMask groundLayer;

    private Rigidbody2D rb;

    public Vector2 bo;
    private void Update()
    {
        Check();  // 更新 onGround
        if (anim != null)
            anim.SetBool("onGround", onGround);   // 这行必须有
         // 关键：更新 velocity 参数，通常取水平速度的绝对值
        float horizontalSpeed = Mathf.Abs(rb.velocity.x);
        anim.SetFloat("velocity", horizontalSpeed);
    }
    private Animator anim;

    void Start()
    {
        anim = GetComponentInChildren<Animator>();  // 改为在子物体中查找
        rb = GetComponent<Rigidbody2D>();  // 获取刚体
    }
    public void Check()
    {
        //检测地面
        onGround = Physics2D.OverlapCircle((Vector2)transform.position + bo, checkRadius, groundLayer);
    }
        //当物体被选中时在场景视图中绘制一个圆形来表示检测范围
        private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere((Vector2)transform.position + bo, checkRadius);
    }

}
