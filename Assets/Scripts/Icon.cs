using UC;
using UnityEngine;

public class Icon : MonoBehaviour
{
    [SerializeField] private IconDef def;

    SpriteRenderer  spriteRenderer;
    GridCollider    gridCollider;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.color = def.color;
        spriteRenderer.sprite = def.sprite;

        gridCollider = GetComponent<GridCollider>();

        if (def.deathTouch)
        {
            var deathTouch = gameObject.AddComponent<KillPlayerOnTouch>();
            deathTouch.playerTag = Globals.playerTag;
            deathTouch.deathReason = def.deathTouchType;
            gridCollider.enabled = false;
        }
        if ((def.actions != null) && (def.actions.Count > 0))
        {
            var action = gameObject.AddComponent<GridAction_Icon>();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if ((def) && (def.sprite != null))
        {
            if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.color = def.color;
            spriteRenderer.sprite = def.sprite;
        }
    }
#endif

    public IconDef GetDef() => def;
}
