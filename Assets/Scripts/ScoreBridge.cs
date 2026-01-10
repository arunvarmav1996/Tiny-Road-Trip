using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScoreBridge : MonoBehaviour
{
   
 public static ScoreBridge Instance;

    public float score = 0f; // Visual script will update this

    void Awake()
    {
        Instance = this;
    }

}
