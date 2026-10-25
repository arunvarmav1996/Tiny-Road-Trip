
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ResponsiveCamera : MonoBehaviour
{
    [Header("Reference Game Resolution")]
    public float referenceWidth = 1080f;
    public float referenceHeight = 1920f;

    [Header("Original Camera Size")]
    public float referenceOrthographicSize = 5f;

    private Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        UpdateCamera();
    }

    void Update()
    {
        UpdateCamera();
    }

    void UpdateCamera()
    {
        if (!cam.orthographic)
            return;

        float referenceAspect =
            referenceWidth / referenceHeight;

        float currentAspect =
            (float)Screen.width / Screen.height;

        cam.orthographicSize =
            referenceOrthographicSize *
            (referenceAspect / currentAspect);
    }
}
