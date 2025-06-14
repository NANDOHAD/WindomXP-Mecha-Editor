using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public delegate void call(string rtext);
public class UI_InputBox : MonoBehaviour
{
    public Text text;
    public InputField input;
    public call _callBack;
    public Dropdown addPartsList;
    private bool isDropdownMode = false;

    // Start is called before the first frame update
    void Start()
    {
        //gameObject.SetActive(false);
    }

    public void openDialog(string message, string defaultText, call callback)
    {
        isDropdownMode = false;
        text.text = message;
        input.text = defaultText;
        _callBack = callback;
        //input.gameObject.SetActive(true);
        //addPartsList.gameObject.SetActive(false);
        gameObject.SetActive(true);
    }

    public void openSelectDialog(string message, string defaultText, call callback)
    {
        isDropdownMode = true;
        text.text = message;
        addPartsList.ClearOptions();
        addPartsList.AddOptions(new List<string> { defaultText });
        addPartsList.value = 0;
        _callBack = callback;
        input.gameObject.SetActive(false);
        addPartsList.gameObject.SetActive(true);
        gameObject.SetActive(true);
    }

    public void openSelectDialog(string message, List<string> options, call callback)
    {
        isDropdownMode = true;
        text.text = message;
        addPartsList.ClearOptions();
        addPartsList.AddOptions(options);
        addPartsList.value = 0;
        _callBack = callback;  
        input.gameObject.SetActive(false);
        addPartsList.gameObject.SetActive(true);
        gameObject.SetActive(true);
    }

    public void openNoTextBoxDialog(string message, call callback)
    {
        isDropdownMode = false;
        text.text = message;
        _callBack = callback;
        gameObject.SetActive(true);
    }

    public void Ok()
    {
        if (isDropdownMode)
        {
            // ドロップダウンモードの場合は選択された値を返す
            _callBack(addPartsList.options[addPartsList.value].text);
        }
        else
        {
            // 通常モードの場合は入力フィールドの値を返す
            _callBack(input.text);
        }
        gameObject.SetActive(false);
    }

    public void cancel()
    {
        gameObject.SetActive(false);
    }
}
