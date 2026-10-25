using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ScoreBridge : MonoBehaviour
{

 public static ScoreBridge Instance;

    public float score = 0f; // Visual script will update this

    // Name of the float object variable the ScoreCount graph accumulates and displays.
    [SerializeField] private string scoreVariable = "Score";

    void Awake()
    {
        Instance = this;
    }

    // Adds bonus points (near-miss combos, Nitro) to the Visual Scripting score without editing the graph.
    public void AddPoints(float points)
    {
        if (points == 0f) return;
        VariableDeclarations variables = Variables.Object(gameObject);
        if (!variables.IsDefined(scoreVariable)) return;

        float current = System.Convert.ToSingle(variables.Get(scoreVariable));
        variables.Set(scoreVariable, current + points);
    }

}
