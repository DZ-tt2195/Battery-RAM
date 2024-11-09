using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RightClickMe : MonoBehaviour, IPointerClickHandler
{
    CanvasGroup cg;
    string artCredit;

    internal void AssignInfo(CanvasGroup cg, string artCredit)
    {
        this.cg = cg;
        this.artCredit = artCredit.Replace("/", "\n").Trim();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            CarryVariables.instance.RightClickDisplay(cg, artCredit);
        }
    }
}
