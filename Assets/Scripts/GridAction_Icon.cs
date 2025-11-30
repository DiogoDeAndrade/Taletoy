using System;
using System.Collections.Generic;
using UC;
using UnityEngine;

public class GridAction_Icon : GridActionContainer
{
    IconDef iconDef;

    protected override void Start()
    {
        base.Start();
        
        var icon = GetComponent<Icon>();
        iconDef = icon.GetDef();
    }

    public override void ActualGatherActions(GridObject subject, Vector2Int position, List<NamedAction> retActions)
    {
        var player = Globals.playerTag.FindFirst<Player>();

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
        IconAction iconAction = null;

        foreach (var a in iconDef.actions)
        {
            if (a.actionTag.name == action.name)
            {
                iconAction = a;
                break;
            }
        }

        if (iconAction == null) return false;

        var player = Globals.playerTag.FindFirst<Player>();
        player.IncAge(iconAction.actionDuration.Random());
        player.AddEvent(iconDef, iconAction);

        Destroy(gameObject);

        player.CompleteAction();

        return true;
    }
}
