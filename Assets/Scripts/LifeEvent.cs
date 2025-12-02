using System;
using UnityEngine;
using UC;

[Serializable]
public class LifeEvent
{
    public enum Type { Death, Action, ActionDeath };

    public Type         type;
    public int          age;
    public Hypertag     deathReason;
    public ConceptDef      iconDef;
    public ConceptAction   action;

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
                return $"- Died {deathReason.displayName} at {age} years".CapitalizeFirstLowerRest();
            case Type.Action:
                return $"- {action.actionTag.displayName} {iconDef.name.ToDisplayName()} at {age} years".CapitalizeFirstLowerRest();
            case Type.ActionDeath:
                return $"- Died while {action.actionTag.displayName} {iconDef.name.ToDisplayName()} at {age} years".CapitalizeFirstLowerRest();
            default:
                break;
        }

        throw new System.NotImplementedException();
    }
}
