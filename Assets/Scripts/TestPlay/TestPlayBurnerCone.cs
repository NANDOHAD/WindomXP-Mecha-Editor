using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TestPlayBurnerCone : MonoBehaviour
{
    const int SegmentCount = 18;

    static Mesh sharedMesh;

    MeshRenderer meshRenderer;
    Material material;
    float currentLength;
    float targetLength;
    float radius = 0.2f;
    float fadeSpeed = 18f;

    public void SetTarget(bool requested, float length, float coneRadius, Color color, float transitionSpeed)
    {
        EnsureInitialized();

        radius = Mathf.Max(0.001f, coneRadius);
        fadeSpeed = Mathf.Max(0f, transitionSpeed);
        targetLength = requested ? Mathf.Max(0f, length) : 0f;

        SetMaterialColor(color);

        if (requested && !gameObject.activeSelf)
            gameObject.SetActive(true);

        if (fadeSpeed <= 0f)
        {
            currentLength = targetLength;
            ApplyScale();
        }
    }

    public void HideImmediate()
    {
        currentLength = 0f;
        targetLength = 0f;
        ApplyScale();
        gameObject.SetActive(false);
    }

    void Awake()
    {
        EnsureInitialized();
    }

    void LateUpdate()
    {
        if (!gameObject.activeSelf)
            return;

        float step = fadeSpeed <= 0f ? 1f : Mathf.Clamp01(fadeSpeed * Time.deltaTime);
        currentLength = Mathf.Lerp(currentLength, targetLength, step);

        if (targetLength <= 0f && currentLength <= 0.01f)
        {
            currentLength = 0f;
            ApplyScale();
            gameObject.SetActive(false);
            return;
        }

        ApplyScale();
    }

    void EnsureInitialized()
    {
        if (meshRenderer != null)
            return;

        MeshFilter filter = GetComponent<MeshFilter>();
        filter.sharedMesh = GetSharedMesh();

        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        material = CreateMaterial();
        meshRenderer.sharedMaterial = material;

        ApplyScale();
    }

    void ApplyScale()
    {
        transform.localScale = new Vector3(radius, radius, Mathf.Max(0f, currentLength));
    }

    static Mesh GetSharedMesh()
    {
        if (sharedMesh != null)
            return sharedMesh;

        Vector3[] vertices = new Vector3[SegmentCount + 1];
        int[] triangles = new int[SegmentCount * 3];

        vertices[0] = Vector3.zero;
        for (int i = 0; i < SegmentCount; i++)
        {
            float angle = Mathf.PI * 2f * i / SegmentCount;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 1f);
        }

        for (int i = 0; i < SegmentCount; i++)
        {
            int next = (i + 1) % SegmentCount;
            int tri = i * 3;
            triangles[tri] = 0;
            triangles[tri + 1] = next + 1;
            triangles[tri + 2] = i + 1;
        }

        sharedMesh = new Mesh();
        sharedMesh.name = "TestPlayBurnerCone";
        sharedMesh.vertices = vertices;
        sharedMesh.triangles = triangles;
        sharedMesh.RecalculateNormals();
        sharedMesh.RecalculateBounds();
        return sharedMesh;
    }

    static Material CreateMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.name = "TestPlayBurnerConeMaterial";
        SetMaterialColor(mat, new Color(0.35f, 0.85f, 1f, 0.65f));
        mat.renderQueue = (int)RenderQueue.Transparent;

        if (mat.HasProperty("_Surface"))
            mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_SrcBlend"))
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend"))
            mat.SetInt("_DstBlend", (int)BlendMode.One);
        if (mat.HasProperty("_ZWrite"))
            mat.SetInt("_ZWrite", 0);
        if (mat.HasProperty("_Cull"))
            mat.SetInt("_Cull", (int)CullMode.Off);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHABLEND_ON");
        return mat;
    }

    void SetMaterialColor(Color color)
    {
        if (material != null)
            SetMaterialColor(material, color);
    }

    static void SetMaterialColor(Material mat, Color color)
    {
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
    }
}
