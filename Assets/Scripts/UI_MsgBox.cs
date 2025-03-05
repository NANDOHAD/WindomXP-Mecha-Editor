using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class UI_MsgBox : MonoBehaviour
{
    public Text text;
    // インスペクターから Masuku オブジェクトを割り当てるための変数
    public GameObject masukuObject;

    public void Start()
    {
        //gameObject.SetActive(false);
        if (masukuObject == null)
        {
            Debug.LogError("Masukuオブジェクトがインスペクターで設定されていません。");
        }
        else
        {
            // ゲームオブジェクトがアクティブなときにmasukuObjectをアクティブにする
            masukuObject.SetActive(gameObject.activeSelf);
        }
    }

    private void OnEnable()
    {
        // ゲームオブジェクトがアクティブになったときにmasukuObjectをアクティブにする
        if (masukuObject != null)
        {
            masukuObject.SetActive(true);
        }
    }

    public void Show(string message)
    {
        text.text = message;
        gameObject.SetActive(true);
        // masukuObjectをアクティブにする
        if (masukuObject != null)
        {
            masukuObject.SetActive(true);
        }
    }
    
    public void Hide()
    {
        gameObject.SetActive(false);
        // Masukuオブジェクトを非表示
        if (masukuObject != null)
        {
            masukuObject.SetActive(false);
        }
    }

    private void Update()
    {
        // ゲームオブジェクトがアクティブなときはmasukuObjectもアクティブにする
        if (gameObject.activeSelf && masukuObject != null)
        {
            masukuObject.SetActive(true);
        }
        else if (masukuObject != null)
        {
            masukuObject.SetActive(false);
        }
    }
}
