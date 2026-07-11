using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GroundDetector : MonoBehaviour
{
    [Header("检测参数")]
    [SerializeField] private float checkRadius = 0.2f;   // 检测半径
    [SerializeField] private LayerMask groundLayer;      // 地面图层
    //封装 对外公开的只读属性（其他脚本只能读，不能写）
    public bool IsGrounded { get; private set; }
    //可选：记录踩到的具体地面，方便做不同材质音效
    //public Collider2D CurrentGround { get; private set; }
    void FixedUpdate()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, checkRadius, groundLayer);//画圆半径为输入的checkRadius的值以及对应的地面层groundLayer，检测是否有碰撞体
        IsGrounded = hit != null;//如果检测到地面，IsGrounded为true，否则为false
            //CurrentGround = hit;//记录踩到的具体地面
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;//设置Gizmos 颜色为绿色
        Gizmos.DrawWireSphere(transform.position, checkRadius);//画一个空心圆，半径为checkRadius
    }




}
