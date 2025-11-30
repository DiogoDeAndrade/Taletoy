using System.Collections.Generic;
using UC;
using UnityEngine;

public class Player : MonoBehaviour
{
    public struct Action
    {
        public GridActionContainer.NamedAction  action;
        public KeyCode                          keyCode;
    }

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
    private GridSystem      gridSystem;
    private List<Action>    availableActions;
    private ActionsPanel    actionsPanel;

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

        gridSystem = GetComponentInParent<GridSystem>();

        spriteRenderer = GetComponent<SpriteRenderer>();

        actionsPanel = FindFirstObjectByType<ActionsPanel>();
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

        actionsPanel?.RefreshActions();
    }

    public void IncAge(int years)
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

    private void Update()
    {
        HandleOptions();
    }

    void HandleOptions()
    {
        availableActions = new();
        if (isDead) return;

        var position = gridObject.GetPositionFacing();

        var actions = gridSystem.GetActions(gridObject, position);

        int cIndex = -1;

        bool[] keys = new bool[26];
        foreach (var action in actions)
        {
            // Assign a key
            var verb = action.name.ToUpper();
            char c = '\0';
            for (int i = 0; i < verb.Length; i++)
            {
                char cc = verb[i];
                cIndex = ((int)cc) - 'A';
                if (!keys[cIndex])
                {
                    c = cc;
                    break;
                }
            }
            if (c == '\0')
            {
                // Select the first unused letter
                for (int i = 0; i < keys.Length; i++)
                {
                    if (!keys[i])
                    {
                        c = (char)(i + 'A');
                        break;
                    }
                }
            }
            cIndex = ((int)c) - 'A';
            keys[cIndex] = true;
            availableActions.Add(new Action()
            {
                keyCode = (KeyCode)(KeyCode.A + cIndex),
                action = action
            });
        }
    }

    public List<Action> GetActions() => availableActions;

    public void AddEvent(IconDef iconDef, IconAction action)
    {
        float f = Random.Range(0.0f, 1.0f) * action.dangerMultiplier;
        if (f < deathProbability)
        {
            lifeEvents.Add(new LifeEvent(LifeEvent.Type.ActionDeath, _age)
            {
                action = action,
                iconDef = iconDef
            });
        }
        else
        {
            lifeEvents.Add(new LifeEvent(LifeEvent.Type.Action, _age)
            {
                action = action,
                iconDef = iconDef
            });
        }

        deathProbability += action.deltaDanger;
        deathProbabilityWalk += action.deltaDanger;
    }

    public void CompleteAction()
    {
        HandleOptions();
        actionsPanel?.RefreshActions();
    }

    public bool HasEventWithCategories(Hypertag[] categories)
    {
        foreach (var evt in lifeEvents)
        {
            switch (evt.type)
            {
                case LifeEvent.Type.Action:
                case LifeEvent.Type.ActionDeath:
                    if (evt.iconDef.HasCategory(categories)) return true;
                    break;
                default:
                    break;
            }
        }

        return false;
    }
}
