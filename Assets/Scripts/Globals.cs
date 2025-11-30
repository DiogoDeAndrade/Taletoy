using NaughtyAttributes;
using UC;
using UnityEngine;

[CreateAssetMenu(fileName = "Globals", menuName = "Taletoy/Globals")]
public class Globals : GlobalsBase
{
    [HorizontalLine(color: EColor.Green)]

    [SerializeField] private int    _startAge = 4;
    [SerializeField] private float  _startDeathProbabilityWalk = -0.3f;
    [SerializeField] private float  _startDeathProbability = -0.2f;
    [SerializeField] private float  _incDeathProbabilityWalk = 0.015f;
    [SerializeField] private float  _incDeathProbability = 0.02f;
    [SerializeField] private Hypertag   _playerTag;


    protected static Globals _instance = null;

    public static Globals instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = instanceBase as Globals;
            }

            return _instance;
        }
    }

    public static int startAge => instance._startAge;
    public static float startDeathProbabilityWalk => instance._startDeathProbabilityWalk;
    public static float startDeathProbability => instance._startDeathProbability;
    public static float incDeathProbabilityWalk => instance._incDeathProbabilityWalk;
    public static float incDeathProbability => instance._incDeathProbability;
    public static Hypertag playerTag => instance._playerTag;

}
