using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
public class Character : MonoBehaviour
{    //人物属性和受伤
    [Header("基本属性")] 

    public float maxHealth;
    //最大生命值

    //当前生命值
    public float currentHealth;
    [Header("受伤无敌")]
    public float invincuibleDuration;//无敌持续时间
    private float invulnerableCounter;//无敌计时器
    public bool invulnerable;//是否无敌

    public UnityEvent<Transform> OnTakeDamaqe;//受伤事件，可以在Inspector中添加响应函数

    public UnityEvent OnDie; //死亡事件，可以在Inspector中添加响应函数
    private void Start()
    {
        currentHealth = maxHealth; //初始化当前生命值为最大生命值
    }
    private void Update()
    {
        if (invulnerable)
        {   invulnerableCounter -= Time.deltaTime; //无敌计时器递减
            if (invulnerableCounter <= 0)
            {   invulnerable = false; //无敌结束
            }
        }
    }
    public void TakeDamage(Attack attacker)
    {
        if (invulnerable)
            return; //如果无敌，直接返回，不受伤
                    //Debug.Log(attacker.damage);
        if (currentHealth - attacker.damage > 0)
        {
            currentHealth -= attacker.damage; //满血-受到的伤害=当前生命值
            TriggerInvulnerable(); //触发无敌状态
            //执行受伤的动画和效果
            OnTakeDamaqe?.Invoke(attacker.transform); //触发受伤事件，传递角色的Transform作为参数
        }
        else
        {
            currentHealth = 0; //如果受到的伤害超过当前生命值，当前生命值设为0
                               //角色死亡的处理逻辑可以在这里添加
            OnDie?.Invoke(); //触发死亡事件

        }


    }
    private void TriggerInvulnerable()
    {
        if (!invulnerable)
        {  invulnerable = true;
            invulnerableCounter = invincuibleDuration; //无敌计时器重置为无敌持续时间
        
        
        
        }

    }

}
