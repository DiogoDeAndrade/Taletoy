using TMPro;
using UC;
using UnityEngine;

public class AgeDisplay : MonoBehaviour
{
    [SerializeField] private Hypertag playerTag;

    Player          player;
    TextMeshProUGUI text;
    string          baseText;

    void Start()
    {
        text = GetComponent<TextMeshProUGUI>();
        baseText = text.text;
    }

    void Update()
    {
        if (!player)
        {
            player = playerTag.FindFirst<Player>();
            if (!player) return;
        }

        text.text = string.Format(baseText, player.age);
    }
}
