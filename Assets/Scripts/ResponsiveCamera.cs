
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ResponsiveCamera : MonoBehaviour
{
    [Header("Reference Game Resolution")]
    public float referenceWidth = 1080f;
    public float referenceHeight = 1920f;

    [Header("Original Camera Size")]
    public float referenceOrthographicSize = 5f;

    [Header("Safe Gameplay Area")]
    // Half-width (world units) that must always be visible: all lanes plus most of the
    // road's white edge lines (road spans ±2.81). Larger values leave a gap above the
    // road on 20:9 phones, because the road sprite only covers y <= 4.875 while scrolling.
    public float safeHalfWidth = 2.75f;

    private Camera cam;
    private int lastScreenWidth;
    private int lastScreenHeight;

    void Awake()
    {
        cam = GetComponent<Camera>();
        UpdateCamera();
    }

    void Update()
    {
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
            UpdateCamera();
    }

    void UpdateCamera()
    {
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        if (!cam.orthographic || Screen.height == 0)
            return;

        float currentAspect =
            (float)Screen.width / Screen.height;

        // Never show less than the original view (keeps the vertical play area on
        // wide screens like iPad), and widen only when the screen is too narrow
        // to fit the lanes (tall phones).
        cam.orthographicSize = Mathf.Max(
            referenceOrthographicSize,
            safeHalfWidth / currentAspect);
    }
}
