using System.Collections;
using UnityEngine;

public class MenuCharacterReaction : MonoBehaviour
{
    [Header("Character")]
    [SerializeField] Animator characterAnimator;
    [SerializeField] Transform characterRoot;
    [SerializeField] Transform quitSpot;
    [SerializeField] float moveDuration = 0.6f;

    [Header("UI look")]
    [SerializeField] Camera sceneCamera;
    [SerializeField] Canvas menuCanvas;
    [SerializeField] float lookSpeed = 4f;

    RectTransform currentButton;
    float lookWeight;

    Vector3 homePosition;
    Quaternion homeRotation;
    Coroutine moveRoutine;

    void Awake()
    {
        if (characterRoot == null)
            characterRoot = transform;

        homePosition = characterRoot.position;
        homeRotation = characterRoot.rotation;
    }

    public void OnButtonHover(RectTransform button, bool isQuit)
    {
        currentButton = button;

        characterAnimator.SetBool("Happy", !isQuit);
        characterAnimator.SetBool("Sad", isQuit);
        characterAnimator.ResetTrigger("ShakeHead");

        if (isQuit)
        {
            characterAnimator.SetTrigger("ShakeHead");

            if (quitSpot != null)
                MoveCharacter(quitSpot.position, quitSpot.rotation);
            else
                Debug.LogWarning("Chưa gán Quit Spot trong Inspector.");
        }
        else
        {
            MoveCharacter(homePosition, homeRotation);
        }
    }

    public void OnButtonExit(RectTransform button)
    {
        // Không xử lý Exit của nút cũ nếu chuột đã vào nút khác
        if (currentButton != button) return;

        currentButton = null;
        characterAnimator.SetBool("Happy", false);
        characterAnimator.SetBool("Sad", false);

        MoveCharacter(homePosition, homeRotation);
    }

    void MoveCharacter(Vector3 targetPosition, Quaternion targetRotation)
    {
        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        moveRoutine = StartCoroutine(
            MoveRoutine(targetPosition, targetRotation)
        );
    }

    IEnumerator MoveRoutine(Vector3 targetPosition, Quaternion targetRotation)
    {
        Vector3 startPosition = characterRoot.position;
        Quaternion startRotation = characterRoot.rotation;
        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);
            float smoothT = t * t * (3f - 2f * t);

            characterRoot.position =
                Vector3.Lerp(startPosition, targetPosition, smoothT);

            characterRoot.rotation =
                Quaternion.Slerp(startRotation, targetRotation, smoothT);

            yield return null;
        }

        characterRoot.position = targetPosition;
        characterRoot.rotation = targetRotation;
        moveRoutine = null;
    }

    void Update()
    {
        float targetWeight = currentButton != null ? 1f : 0f;
        lookWeight = Mathf.MoveTowards(
            lookWeight,
            targetWeight,
            Time.unscaledDeltaTime * lookSpeed
        );
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (characterAnimator == null ||
            !characterAnimator.isHuman ||
            sceneCamera == null ||
            menuCanvas == null)
            return;

        characterAnimator.SetLookAtWeight(
            lookWeight, 0.1f, 0.8f, 0.5f, 0.5f
        );

        if (currentButton == null) return;

        Camera canvasCamera =
            menuCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : menuCanvas.worldCamera;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            canvasCamera,
            currentButton.position
        );

        Ray ray = sceneCamera.ScreenPointToRay(screenPoint);
        Transform head =
            characterAnimator.GetBoneTransform(HumanBodyBones.Head);

        Vector3 origin = head != null
            ? head.position
            : characterRoot.position + Vector3.up * 1.5f;

        float depth = Vector3.Dot(
            origin - sceneCamera.transform.position,
            sceneCamera.transform.forward
        );

        float rayDepth = depth / Mathf.Max(
            0.01f,
            Vector3.Dot(ray.direction, sceneCamera.transform.forward)
        );

        characterAnimator.SetLookAtPosition(ray.GetPoint(rayDepth));
    }
}