using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// Sao băng ngẫu nhiên trên bầu trời, luôn xuất hiện trong khung hình của camera.
public class ShootingStars : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Camera targetCamera;
    [SerializeField] Material trailMaterial;

    [Header("Timing")]
    [SerializeField] Vector2 spawnInterval = new Vector2(1.5f, 5f);
    [SerializeField] Vector2 flightDuration = new Vector2(0.45f, 1f);

    [Header("Shape")]
    [SerializeField] Vector2 arcDegrees = new Vector2(10f, 25f);
    [SerializeField, Range(0.05f, 0.9f)] float tailLength = 0.35f;
    [SerializeField] float skyDistance = 600f;
    [SerializeField] float headWidth = 2.2f;
    [SerializeField] Color starColor = new Color(0.85f, 0.92f, 1f, 1f);
    [SerializeField, Range(0f, 0.95f)] float minViewportHeight = 0.6f;

    [Header("Pool")]
    [SerializeField] int poolSize = 4;
    [SerializeField] int trailPoints = 12;

    LineRenderer[] lines;
    bool[] busy;

    void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
        {
            Debug.LogError("ShootingStars: chưa gán Camera trong Inspector.");
            enabled = false;
            return;
        }

        if (trailMaterial == null)
        {
            Debug.LogError("ShootingStars: chưa gán Trail Material trong Inspector.");
            enabled = false;
            return;
        }

        trailPoints = Mathf.Max(2, trailPoints);
        lines = new LineRenderer[poolSize];
        busy = new bool[poolSize];

        AnimationCurve widthCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(1f, 1f)
        );

        for (int i = 0; i < poolSize; i++)
        {
            GameObject star = new GameObject("Shooting Star " + i);
            star.transform.SetParent(transform, false);

            LineRenderer line = star.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = trailPoints;
            line.sharedMaterial = trailMaterial;
            line.widthCurve = widthCurve;
            line.widthMultiplier = headWidth;
            line.numCapVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;

            lines[i] = line;
        }
    }

    void OnEnable()
    {
        if (lines != null)
            StartCoroutine(SpawnLoop());
    }

    void OnDisable()
    {
        if (lines == null) return;

        for (int i = 0; i < lines.Length; i++)
        {
            lines[i].enabled = false;
            busy[i] = false;
        }
    }

    IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(
                Random.Range(spawnInterval.x, spawnInterval.y)
            );

            int index = System.Array.IndexOf(busy, false);
            if (index >= 0)
                StartCoroutine(Fly(index));
        }
    }

    IEnumerator Fly(int index)
    {
        busy[index] = true;
        LineRenderer line = lines[index];
        Transform cam = targetCamera.transform;
        Vector3 origin = cam.position;

        // Điểm bắt đầu nằm ở nửa trên khung hình hiện tại
        Vector3 viewport = new Vector3(
            Random.Range(0.1f, 0.9f),
            Random.Range(minViewportHeight, 0.95f),
            0f
        );
        Vector3 startDir = targetCamera.ViewportPointToRay(viewport).direction;

        // Bay chéo xuống, sang trái hoặc phải
        float side = Random.value < 0.5f ? -1f : 1f;
        Vector3 travel = cam.right * side * Random.Range(0.6f, 1f)
                       - cam.up * Random.Range(0.25f, 0.7f);
        travel = Vector3.ProjectOnPlane(travel, startDir).normalized;

        float arc = Random.Range(arcDegrees.x, arcDegrees.y) * Mathf.Deg2Rad;
        Vector3 endDir = (startDir * Mathf.Cos(arc) + travel * Mathf.Sin(arc)).normalized;

        float duration = Random.Range(flightDuration.x, flightDuration.y);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(elapsed / duration);

            // Đầu chạy trước, đuôi đuổi theo rồi thu lại ở cuối
            float head = Mathf.Clamp01(p * (1f + tailLength));
            float tail = Mathf.Clamp01(p * (1f + tailLength) - tailLength);
            float fade = Mathf.Clamp01(p * 8f) * Mathf.Clamp01((1f - p) * 4f);

            for (int i = 0; i < trailPoints; i++)
            {
                float s = Mathf.Lerp(tail, head, i / (float)(trailPoints - 1));
                line.SetPosition(i, origin + Vector3.Slerp(startDir, endDir, s) * skyDistance);
            }

            line.startColor = new Color(starColor.r, starColor.g, starColor.b, 0f);
            line.endColor = new Color(starColor.r, starColor.g, starColor.b, fade);
            line.enabled = true;

            yield return null;
        }

        line.enabled = false;
        busy[index] = false;
    }
}
