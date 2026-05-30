using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Attack : MonoBehaviour
{    //攻击脚本以及计算伤害的脚本
 
    public int damage; //伤害值
    public float attackRange; //攻击范围
    public float attackRate; //攻击频率

    private void OnTriggerEnter2D(Collider2D other)
    {
        other.GetComponent<Character>()?.TakeDamage(this); 
        //当攻击碰撞到敌人时，调用敌人的TakeDamage方法，传入伤害值
    }


}




  