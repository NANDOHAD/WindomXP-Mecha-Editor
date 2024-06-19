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
    public Vector3 minPosition = new Vector3(-5f, 0f, -10f);
    public Vector3 maxPosition = new Vector3(5f, 10f, 10f);

    void Start()
    {
        mousePosition = Input.mousePosition;
        panSpeed.text = (rSpeed * 100).ToString();
        movSpeed.text = (tSpeed * 100).ToString();
    }

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

            if (Input.GetMouseButton(2))
            {
                Vector3 proposedTranslation = new Vector3(diff.x * tSpeed, diff.y * tSpeed, 0);
                Vector3 newPosition = transform.position + transform.TransformDirection(proposedTranslation);
                newPosition.x = Mathf.Clamp(newPosition.x, minPosition.x, maxPosition.x);
                newPosition.y = Mathf.Clamp(newPosition.y, minPosition.y, maxPosition.y);
                newPosition.z = Mathf.Clamp(newPosition.z, minPosition.z, maxPosition.z);
                transform.position = newPosition;
            }

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0.0f)
            {
                Vector3 proposedZoom = Vector3.forward * -scroll * zoomSpeed;
                Vector3 newPosition = transform.position + transform.TransformDirection(proposedZoom);
                newPosition.z = Mathf.Clamp(newPosition.z, minPosition.z, maxPosition.z);
                transform.position = newPosition;
            }
        }
        mousePosition = Input.mousePosition;
    }

    public void updateSpeeds()
    {
        float speed;
        if (float.TryParse(panSpeed.text, out speed))
            rSpeed = speed / 100;

        if (float.TryParse(movSpeed.text, out speed))
            tSpeed = speed / 100;
    }
}
