using NaughtyAttributes;
using System.Collections.Generic;
using UC;
using UnityEngine;

[CreateAssetMenu(fileName = "IconDef", menuName = "Taletoy/Icon Def")]
public class IconDef : ScriptableObject
{
    public Sprite           sprite;
    public List<Hypertag>   categories;
    public Color            color = Color.white;
    public bool             deathTouch;
    [ShowIf(nameof(deathTouch))]
    public Hypertag         deathTouchType;
    public List<IconAction> actions;

    public bool HasCategory(Hypertag[] categories)
    {
        if ((this.categories == null) || (this.categories.Count == 0)) return false;

        foreach (Hypertag category in categories)
        {
            if (!HasCategory(category)) return false;
        }

        return true;
    }

    public bool HasCategory(Hypertag category)
    {
        if ((categories == null) || (categories.Count == 0)) return false;

        return categories.IndexOf(category) >= 0;
    }
}
