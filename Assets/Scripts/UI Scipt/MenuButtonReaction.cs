using UnityEngine;
using UnityEngine.EventSystems;

public class MenuButtonReaction : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] MenuCharacterReaction character;
    [SerializeField] bool isQuitButton;

    RectTransform buttonRect;

    void Awake()
    {
        buttonRect = GetComponent<RectTransform>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        character.OnButtonHover(buttonRect, isQuitButton);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        character.OnButtonExit(buttonRect);
    }
}