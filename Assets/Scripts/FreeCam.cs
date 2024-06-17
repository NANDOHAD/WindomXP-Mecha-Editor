using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public class FreeCam : MonoBehaviour
{
    public bool canMove = true;
    public Vector3 mousePosition = Vector3.zero;
    public float rSpeed = 1;
    public float tSpeed = 0.1f;
    public EventSystem es;
    public InputField panSpeed;
    public InputField movSpeed;
    public float zoomSpeed = 10.0f;
    // Start is called before the first frame update
    void Start()
    {
        mousePosition = Input.mousePosition;
        panSpeed.text = (rSpeed * 100).ToString();
        movSpeed.text = (tSpeed * 100).ToString();
    }

    // Update is called once per frame
void FixedUpdate()
{
    Vector3 diff = mousePosition - Input.mousePosition;
    if (canMove)
    {
        if (Input.GetMouseButton(1) && !es.IsPointerOverGameObject())
        {
            transform.Rotate(new Vector3(diff.y * rSpeed, -diff.x * rSpeed, 0), Space.Self);
            transform.eulerAngles = new Vector3(transform.eulerAngles.x, transform.eulerAngles.y, 0);
        }

        // マウスの中ボタンでカメラの上下左右移動
        if (Input.GetMouseButton(2))
        {
            transform.Translate(new Vector3(diff.x * tSpeed, diff.y * tSpeed, 0), Space.Self);
        }

        // マウスホイールによるズームインとズームアウト
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0.0f)
        {
            transform.Translate(Vector3.forward * scroll * zoomSpeed, Space.Self);
        }
    }
    mousePosition = Input.mousePosition;
}

    public void updateSpeeds()
    {
        float speed;
        if (float.TryParse(panSpeed.text,out speed))
            rSpeed = speed / 100;

        if (float.TryParse(movSpeed.text,out speed))
            tSpeed = speed / 100;
    }
}
