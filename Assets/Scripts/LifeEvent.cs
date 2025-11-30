using System;
using UnityEngine;
using UC;

[Serializable]
public class LifeEvent
{
    public enum Type { Death };

    public Type         type;
    public int          age;
    public Hypertag     deathReason;

    public LifeEvent(Type type, int age)
    {
        this.type = type;
        this.age = age;
    }

    public string GetString()
    {
        switch (type)
        {
            case Type.Death:
                return $"- Died {deathReason.displayName} at {age} years";
            default:
                break;
        }

        throw new System.NotImplementedException();
    }
}
