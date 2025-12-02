using System;
using System.Collections.Generic;
using UC;
using UnityEngine;

public class GridAction_Icon : GridActionContainer
{
    ConceptDef iconDef;

    protected override void Start()
    {
        base.Start();
        
        var icon = GetComponent<Concept>();
        iconDef = icon.GetDef();
    }

    public override void ActualGatherActions(GridObject subject, Vector2Int position, List<NamedAction> retActions)
    {
        var player = Globals.playerTag.FindFirst<Player>();

        if (iconDef == null)
        {
            // No icon defined for some reason
            var icon = GetComponent<Concept>();
            iconDef = icon?.GetDef() ?? null;
            if (iconDef == null)
            {
                Debug.LogWarning("No concept defined on Concept behaviour!");
                return;
            }
        }

        foreach (var action in iconDef.actions)
        {
            if (action.Evaluate(player))
            {

                var newAction = new NamedAction
                {
                    name = action.actionTag.name,
                    action = RunAction,
                    container = this,
                    combatTextEnable = false,
                };
                retActions.Add(newAction);
            }
        }
    }

    private bool RunAction(NamedAction action, GridObject subject, Vector2Int position)
    {
        ConceptAction conceptAction = null;

        foreach (var a in iconDef.actions)
        {
            if (a.actionTag.name == action.name)
            {
                conceptAction = a;
                break;
            }
        }

        if (conceptAction == null) return false;

        var def = subject.GetComponent<Concept>().GetDef();

        var player = Globals.playerTag.FindFirst<Player>();
        var str = $"{conceptAction.actionTag.displayName} {def.name.ToDisplayName()}";
        str = str.CapitalizeFirstLowerRest();

        var color = Color.white; // def.color

        CombatTextManager.SpawnText(player.gameObject, str, color, color.ChangeAlpha(0.0f), 2.0f);

        player.IncAge(conceptAction.actionDuration.Random());
        player.AddEvent(iconDef, conceptAction);

        Destroy(gameObject);

        player.CompleteAction();

        return true;
    }
}
