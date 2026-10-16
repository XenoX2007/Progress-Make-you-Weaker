using UnityEngine;

public class ShowTextAtAngle : MonoBehaviour
{
    [SerializeField] Camera targetCamera;
    [SerializeField] GameObject textObject;
    [SerializeField] float showAtAngle = 19.44f;

    bool shown;

    void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (textObject != null)
            textObject.SetActive(false);
    }

    void Update()
    {
        if (shown || targetCamera == null || textObject == null) return;

        float angle = Mathf.DeltaAngle(0f, targetCamera.transform.eulerAngles.x);

        if (angle <= showAtAngle)
        {
            textObject.SetActive(true);
            shown = true;
            Debug.Log($"Đã hiện Text. Góc X camera: {angle:F3}°");
        }
    }
}