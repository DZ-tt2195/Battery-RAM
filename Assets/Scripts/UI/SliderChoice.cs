using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MyBox;

public class SliderChoice : MonoBehaviour
{
    Player decidingPlayer;
    [SerializeField] TMP_Text textbox;
    [SerializeField] Button confirmButton;

    [SerializeField] Slider slider;
    int currentSliderValue = 0;

    [SerializeField] TMP_Text minimumText;
    [SerializeField] TMP_Text maximumText;
    [SerializeField] TMP_Text currentText;

    private void Awake()
    {
        slider.onValueChanged.AddListener(UpdateText);
        confirmButton.onClick.AddListener(DecisionMade);
    }

    internal void StatsSetup(Player player, string header, int min, int max, Vector3 position)
    {
        decidingPlayer = player;
        this.textbox.text = header;
        this.transform.SetParent(Manager.instance.canvas.transform);
        this.transform.localPosition = position;
        this.transform.localScale = Vector3.Lerp(Vector3.one, Manager.instance.canvas.transform.localScale, 0.5f);

        minimumText.text = min.ToString();
   	    slider.minValue = min;

        maximumText.text = max.ToString();
        slider.maxValue = max;

        slider.value = min;
        UpdateText(slider.minValue);
    }

    void UpdateText(float value)
    {
        currentText.text = $"{(int)value}";
        currentSliderValue = (int)value;
    }

    void DecisionMade()
    {
        decidingPlayer.DecisionMade(currentSliderValue);
    }
}
