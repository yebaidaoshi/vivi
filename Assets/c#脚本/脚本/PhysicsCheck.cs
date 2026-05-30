using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PhysicsCheck : MonoBehaviour
{    //限制跳跃次数
    [Header("状态")]
    public bool onGround;
    [Header("检测地面相关参数")]
    public float checkRadius;

    public LayerMask groundLayer;

    public Vector2 bo;
    private void Update()
    {
        Check();
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
