using NaughtyAttributes;
using UC;
using UnityEngine;

[CreateAssetMenu(fileName = "Globals", menuName = "Taletoy/Globals")]
public class Globals : GlobalsBase
{
    [HorizontalLine(color: EColor.Green)]

    [SerializeField] private int _startAge = 4;


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

}
