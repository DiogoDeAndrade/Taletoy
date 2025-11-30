using NaughtyAttributes;
using UC;
using UC.Interaction;
using UnityEngine;

[System.Serializable]
public class IconAction
{
    public Hypertag         actionTag;
    public Vector2Int       actionDuration = Vector2Int.one;
    public IconCondition[]  conditions;
    public float            dangerMultiplier = 1.0f;
    public float            deltaDanger = 0.0f;

    public bool Evaluate(Player player)
    {
        if ((conditions == null) || (conditions.Length == 0)) return true;

        foreach (var condition in conditions)
        {
            if (!condition.Evaluate(player)) return false;
        }

        return true;
    }
}

[System.Serializable]
public class IconCondition
{
    public enum Type { RequireAge, RequireCategory };

    public Type         type;
    [ShowIf(nameof(needsAge))]
    public int          age;
    [ShowIf(nameof(needsCategories))]
    public Hypertag[]   categories;

    bool needsAge => type == Type.RequireAge;
    bool needsCategories => type == Type.RequireCategory;

    public bool Evaluate(Player player)
    {
        switch (type)
        {
            case Type.RequireAge:
                return (player.age >= age);
            case Type.RequireCategory:
                return player.HasEventWithCategories(categories);
            default:
                break;
        }

        return true;
    }
}
