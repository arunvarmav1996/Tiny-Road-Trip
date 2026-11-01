using UnityEngine;

// Legacy point score from the Visual Scripting days. The HUD score is distance now (RunStats), and nothing
// wires a ScoreBridge any more; the component stays so older setup tools and serialized references still resolve.
public class ScoreBridge : MonoBehaviour
{

 public static ScoreBridge Instance;

    public float score = 0f;

    void Awake()
    {
        Instance = this;
    }

    public void AddPoints(float points)
    {
        score += points;
    }

}
