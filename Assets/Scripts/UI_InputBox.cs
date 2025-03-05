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
    // Start is called before the first frame update
    void Start()
    {
        //gameObject.SetActive(false);
    }

    public void openDialog(string message, string defaultText, call callback)
    {
        text.text = message;
        input.text = defaultText;
        _callBack = callback;
        gameObject.SetActive(true);
    }

    public void openNoTextBoxDialog(string message, call callback)
    {
        text.text = message;
        _callBack = callback;
        gameObject.SetActive(true);
    }


    public void Ok()
    {
        _callBack(input.text);
        gameObject.SetActive(false);
    }

    public void cancel()
    {
        gameObject.SetActive(false);
    }

    


}
