using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MyBox;

public class LogText : MonoBehaviour
{
    public TMP_Text textBox;
    public Image undoBar;
    public Button button;
    public NextStep step;

    private void FixedUpdate()
    {
        undoBar.SetAlpha(Manager.instance.opacity);
    }
}
