using System.Collections.Generic;
using UC;
using UnityEngine;

public class Player : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField]
    private Sprite          deathSprite;
    [SerializeField]
    private Color           deathColor = Color.red;
    
    private int             _age;
    private float           deathProbabilityWalk;
    private float           deathProbability;
    private float           deathProbabilityModifier;
    private SpriteRenderer  spriteRenderer;
    private List<LifeEvent> lifeEvents = new();

    public int age => _age;

    void Start()
    {
        _age = Globals.startAge;
        deathProbability = Globals.startDeathProbability;
        deathProbabilityWalk = Globals.startDeathProbabilityWalk;
        deathProbabilityModifier = Random.Range(0.8f, 1.2f);

        var gridObject = GetComponent<GridObject>();
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
                Die();
            }
        }
    }

    void IncAge(int years)
    {
        _age += years;
        deathProbability += Globals.incDeathProbability * years * deathProbabilityModifier;
        deathProbabilityWalk += Globals.incDeathProbabilityWalk * years * deathProbabilityModifier;
    }

    void Die()
    {
        // Die!
        spriteRenderer.sprite = deathSprite;
        spriteRenderer.color = deathColor;

        var gridMovement = GetComponent<MovementGridXY>();
        gridMovement.enabled = false;

        lifeEvents.Add(new LifeEvent(LifeEvent.Type.DeathOfOldAge, _age));

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
}
