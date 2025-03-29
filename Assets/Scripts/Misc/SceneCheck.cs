using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using MyBox;

public class SceneCheck : MonoBehaviour
{
    Button button;
    [Scene] [SerializeField] string toLoad;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(() => SceneManager.LoadScene(toLoad));
    }
}
