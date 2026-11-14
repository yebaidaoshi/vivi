using System;
using UnityEngine;

namespace Enemy.Fsm
{
    /// <summary>
    /// 原版 FSM 数据（由解包 YAML 自动导出，见 Assets/Resources/EnemyFsm/_HyenaParent.json）。
    /// 这里只做数据承载 + 查询，不含任何行为。
    /// </summary>
    [Serializable]
    public class FsmDatabase
    {
        public string source;
        public int fsmCount;
        public int stateCount;
        public FsmDefinition[] fsms;

        public FsmDefinition Find(string host, string name)
        {
            if (fsms == null) return null;
            for (int i = 0; i < fsms.Length; i++)
            {
                var f = fsms[i];
                if (f == null) continue;
                if (f.name != name) continue;
                if (!string.IsNullOrEmpty(host) && f.host != host) continue;
                return f;
            }
            return null;
        }
    }

    [Serializable]
    public class FsmDefinition
    {
        public string host;
        public string name;
        public string startState;
        public FsmTransitionDefinition[] globalTransitions;
        public FsmStateDefinition[] states;

        /// <summary>按事件名找全局跳转目标；没有返回 null。</summary>
        public string GlobalTargetFor(string eventName)
        {
            if (globalTransitions == null) return null;
            for (int i = 0; i < globalTransitions.Length; i++)
            {
                var t = globalTransitions[i];
                if (t != null && t.evt == eventName) return t.to;
            }
            return null;
        }

        public FsmStateDefinition FindState(string stateName)
        {
            if (states == null) return null;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i] != null && states[i].name == stateName) return states[i];
            }
            return null;
        }

        public int IndexOf(string stateName)
        {
            if (states == null) return -1;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i] != null && states[i].name == stateName) return i;
            }
            return -1;
        }
    }

    [Serializable]
    public class FsmStateDefinition
    {
        public string name;
        public string[] actions;
        public FsmTransitionDefinition[] transitions;
        public string[] parameters;

        /// <summary>按事件名找目标状态；没有该事件返回 null。</summary>
        public string TargetStateFor(string eventName)
        {
            if (transitions == null) return null;
            for (int i = 0; i < transitions.Length; i++)
            {
                var t = transitions[i];
                if (t != null && t.evt == eventName) return t.to;
            }
            return null;
        }

        public bool HasAction(string actionName)
        {
            if (actions == null) return false;
            for (int i = 0; i < actions.Length; i++) if (actions[i] == actionName) return true;
            return false;
        }
    }

    [Serializable]
    public class FsmTransitionDefinition
    {
        public string evt;
        public string to;
    }

    public static class EnemyFsmData
    {
        public const string HyenaResourcePath = "EnemyFsm/_HyenaParent";

        private static FsmDatabase _hyena;

        public static FsmDatabase Hyena
        {
            get
            {
                if (_hyena == null) _hyena = Load(HyenaResourcePath);
                return _hyena;
            }
        }

        public static FsmDatabase Load(string resourcePath)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                Debug.LogError("[EnemyFsmData] 找不到 Resources/" + resourcePath + ".json");
                return null;
            }

            try
            {
                return JsonUtility.FromJson<FsmDatabase>(asset.text);
            }
            catch (Exception e)
            {
                Debug.LogError("[EnemyFsmData] JSON 解析失败：" + e.Message);
                return null;
            }
        }
    }
}
