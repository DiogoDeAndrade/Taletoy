using System;
using UnityEngine;

[Serializable]
public class LifeEvent
{
    public enum Type { DeathOfOldAge };

    public Type type;
    public int  age;

    public LifeEvent(Type type, int age)
    {
        this.type = type;
        this.age = age;
    }

    public string GetString()
    {
        switch (type)
        {
            case Type.DeathOfOldAge:
                return $"- Died of old age at {age} years";
            default:
                break;
        }

        throw new System.NotImplementedException();
    }
}
