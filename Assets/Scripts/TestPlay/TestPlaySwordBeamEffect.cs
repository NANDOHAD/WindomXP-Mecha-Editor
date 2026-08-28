using UnityEngine;

public class TestPlaySwordBeamEffect : MonoBehaviour
{
    Transform anchor;
    Transform[] primaryPlanes;
    Transform[] linePlanes;

    public Transform Anchor => anchor;
    public float CurrentLength { get; private set; }
    public float TargetLength { get; private set; }
    public float LineWidth => TestPlayPresentationCore.OriginalSwordBeamWidth;
    public int PrimaryTextureId { get; private set; }
    public int LineTextureId { get; private set; }
    public bool HasPrimaryLayer => primaryPlanes != null && primaryPlanes.Length > 0;
    public bool HasLineLayer => linePlanes != null && linePlanes.Length > 0;

    public void Initialize(
        Transform sourceAnchor,
        Transform[] primaryLayerPlanes,
        Transform[] lineLayerPlanes,
        int primaryTextureId,
        int lineTextureId,
        bool reverseDirection,
        float initialLength,
        float targetLength)
    {
        anchor = sourceAnchor;
        primaryPlanes = primaryLayerPlanes ?? new Transform[0];
        linePlanes = lineLayerPlanes ?? new Transform[0];
        PrimaryTextureId = primaryTextureId;
        LineTextureId = lineTextureId;
        TargetLength = targetLength;

        if (anchor != null)
        {
            transform.SetParent(anchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = reverseDirection
                ? Quaternion.Euler(0f, 180f, 0f)
                : Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        SetLength(initialLength);
    }

    public void SetLength(float length)
    {
        CurrentLength = length;
        float displayLength = Mathf.Max(0.0001f, Mathf.Abs(length));
        ConfigureCrossedPlanes(
            primaryPlanes,
            displayLength,
            TestPlayPresentationCore.OriginalSwordBeamWidth * 2f);
        ConfigureCrossedPlanes(
            linePlanes,
            displayLength,
            TestPlayPresentationCore.OriginalSwordBeamWidth);
    }

    static void ConfigureCrossedPlanes(Transform[] planes, float length, float width)
    {
        if (planes == null)
            return;

        for (int i = 0; i < planes.Length; i++)
        {
            Transform plane = planes[i];
            if (plane == null)
                continue;

            plane.localPosition = Vector3.forward * (length * 0.5f);
            if ((i & 1) == 0)
            {
                plane.localRotation = Quaternion.Euler(90f, 0f, 0f);
                plane.localScale = new Vector3(width, length, 1f);
            }
            else
            {
                plane.localRotation = Quaternion.Euler(0f, -90f, 0f);
                plane.localScale = new Vector3(length, width, 1f);
            }
        }
    }
}
