using System.Collections.Generic;
using UC;
using UnityEngine;

public class Player : MonoBehaviour
{
    [Header("Death")]
    [SerializeField]
    private Sprite          deathSprite;
    [SerializeField]
    private Color           deathColor = Color.red;
    [SerializeField]
    private Hypertag        deathByWalking;

    private int             _age;
    private float           deathProbabilityWalk;
    private float           deathProbability;
    private float           deathProbabilityModifier;
    private SpriteRenderer  spriteRenderer;
    private List<LifeEvent> lifeEvents = new();
    private bool            _isDead = false;
    private GridObject      gridObject;
    
    public int age => _age;
    public bool isDead => _isDead;
    public Vector2Int gridPosition => gridObject.gridPosition;

    void Start()
    {
        _age = Globals.startAge;
        deathProbability = Globals.startDeathProbability;
        deathProbabilityWalk = Globals.startDeathProbabilityWalk;
        deathProbabilityModifier = Random.Range(0.8f, 1.2f);

        gridObject = GetComponent<GridObject>();
        gridObject.onMoveEnd += GridObject_onMoveEnd;

        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void GridObject_onMoveEnd(Vector2Int sourcePos, Vector2Int destPos, bool success)
    {
        if (success)
        {
            IncAge(1);

            // Check if we died of walking
            float f = Random.Range(0.0f, 1.0f);
            if (f < deathProbabilityWalk)
            {
                Die(deathByWalking);
            }
        }
    }

    void IncAge(int years)
    {
        _age += years;
        deathProbability += Globals.incDeathProbability * years * deathProbabilityModifier;
        deathProbabilityWalk += Globals.incDeathProbabilityWalk * years * deathProbabilityModifier;
    }

    void Die(Hypertag deathReason)
    {
        _isDead = true;

        spriteRenderer.sprite = deathSprite;
        spriteRenderer.color = deathColor;

        var gridMovement = GetComponent<MovementGridXY>();
        gridMovement.enabled = false;

        lifeEvents.Add(new LifeEvent(LifeEvent.Type.Death, _age)
        {
            deathReason = deathReason
        });

        string lifeText = ConvertLifeEventsToText();

        Debug.Log(lifeText);
    }

    string ConvertLifeEventsToText()
    {
        string ret = "";

        foreach (var evt in lifeEvents)
        {
            ret += $"{evt.GetString()}\n";
        }

        return ret;
    }

    public void Kill(Hypertag deathReason)
    {
        Die(deathReason);
    }
}
