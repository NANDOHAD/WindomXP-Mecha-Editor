using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TestPlayAudioBinding
{
    public string key;
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
}

[Serializable]
public class TestPlayEffectBinding
{
    public string key;
    public GameObject prefab;
}

[Serializable]
public class TestPlayTextureBinding
{
    public int loadSequence = -1;
    public int scriptTextureId = -1;
    public string originalFileName;
    public Texture2D texture;
}

public class TestPlayPresentationRuntime : MonoBehaviour
{
    [Header("References")]
    public TestPlayController controller;
    public AudioSource soundSource;
    public AudioSource voiceSource;

    [Header("Original resource mappings")]
    public List<TestPlayAudioBinding> sounds = new List<TestPlayAudioBinding>();
    public List<TestPlayAudioBinding> voices = new List<TestPlayAudioBinding>();
    public List<TestPlayEffectBinding> effects = new List<TestPlayEffectBinding>();
    public List<TestPlayTextureBinding> originalTextures = new List<TestPlayTextureBinding>();
    public Shader originalEffectShader;
    public bool logMissingAudio = true;
    public bool logMissingTextures = true;
    public bool preloadMappedAudio = true;
    public bool useSpatialAudio = true;
    public float audioMaxDistance = 40f;

    readonly HashSet<string> missingAudioKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> missingTextureKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    void Awake()
    {
        EnsureAudioSources();
        if (preloadMappedAudio)
        {
            PreloadBindings(sounds);
            PreloadBindings(voices);
        }
        if (controller == null)
            controller = GetComponent<TestPlayController>();
    }

    void OnEnable()
    {
        Bind(controller != null ? controller : GetComponent<TestPlayController>());
    }

    void OnDisable()
    {
        Unbind();
    }

    public void Bind(TestPlayController source)
    {
        if (controller == source && controller != null)
        {
            controller.PresentationEventRaised -= HandlePresentationEvent;
            controller.PresentationEventRaised += HandlePresentationEvent;
            return;
        }

        Unbind();
        controller = source;
        if (controller != null)
            controller.PresentationEventRaised += HandlePresentationEvent;
    }

    public void StopPresentation()
    {
        if (soundSource != null)
            soundSource.Stop();
        if (voiceSource != null)
            voiceSource.Stop();
    }

    public void AttachEmitters(Transform sourceRoot)
    {
        EnsureAudioSources();
        if (sourceRoot == null)
            return;

        AttachEmitter(soundSource, sourceRoot);
        AttachEmitter(voiceSource, sourceRoot);
    }

    public GameObject CreateMappedEffect(string key, Vector3 position, Quaternion rotation)
    {
        if (effects == null)
            return null;

        for (int i = 0; i < effects.Count; i++)
        {
            TestPlayEffectBinding binding = effects[i];
            if (binding != null && binding.prefab != null && string.Equals(binding.key, key, StringComparison.OrdinalIgnoreCase))
                return Instantiate(binding.prefab, position, rotation);
        }
        return null;
    }

    public GameObject CreateOriginalTextureEffect(int textureId, Vector3 position, Quaternion rotation, Vector2 size, float life, Color tint, bool billboard = true)
    {
        TestPlayTextureBinding binding = FindOriginalTexture(originalTextures, textureId);
        return CreateOriginalTextureEffect(binding, textureId.ToString(), position, rotation, size, life, tint, billboard);
    }

    public GameObject CreateOriginalNamedTextureEffect(string fileName, Vector3 position, Quaternion rotation, Vector2 size, float life, Color tint, bool billboard = true)
    {
        TestPlayTextureBinding binding = FindOriginalTextureByName(originalTextures, fileName);
        return CreateOriginalTextureEffect(binding, fileName, position, rotation, size, life, tint, billboard);
    }

    public GameObject CreateOriginalSwordBeamEffect(
        TestPlaySwordBeamParameters parameters,
        Transform anchor,
        bool reverseDirection,
        out TestPlaySwordBeamEffect swordBeam,
        out bool primaryLayerCreated,
        out bool lineLayerCreated)
    {
        swordBeam = null;
        primaryLayerCreated = false;
        lineLayerCreated = false;
        if (anchor == null)
            return null;

        GameObject root = new GameObject("TestPlayOriginalSwordBeam");
        Transform[] primaryPlanes = CreateSwordBeamPlanes(
            root.transform,
            parameters.primaryTextureId,
            "Primary");
        Transform[] linePlanes = CreateSwordBeamPlanes(
            root.transform,
            parameters.lineTextureId,
            "Line");
        primaryLayerCreated = primaryPlanes.Length > 0;
        lineLayerCreated = linePlanes.Length > 0;
        if (!primaryLayerCreated && !lineLayerCreated)
        {
            DestroyRuntimeObject(root);
            return null;
        }

        swordBeam = root.AddComponent<TestPlaySwordBeamEffect>();
        swordBeam.Initialize(
            anchor,
            primaryPlanes,
            linePlanes,
            parameters.primaryTextureId,
            parameters.lineTextureId,
            reverseDirection,
            parameters.initialLength,
            parameters.targetLength);
        return root;
    }

    GameObject CreateOriginalTextureEffect(TestPlayTextureBinding binding, string missingKey, Vector3 position, Quaternion rotation, Vector2 size, float life, Color tint, bool billboard)
    {
        if (binding == null || binding.texture == null || originalEffectShader == null)
        {
            if (logMissingTextures && missingTextureKeys.Add(missingKey ?? ""))
                Debug.LogWarning("[TestPlay][Effect] Missing original texture mapping: " + missingKey);
            return null;
        }

        GameObject effectObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Collider collider = effectObject.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying)
                Destroy(collider);
            else
                DestroyImmediate(collider);
        }

        effectObject.name = "TestPlayOriginalEffect_" + missingKey + "_" + binding.texture.name;
        effectObject.transform.SetPositionAndRotation(position, rotation);
        TestPlayOriginalEffect effect = effectObject.AddComponent<TestPlayOriginalEffect>();
        effect.Initialize(binding.texture, originalEffectShader, size, life, tint, billboard);
        return effectObject;
    }

    Transform[] CreateSwordBeamPlanes(Transform parent, int textureId, string layerName)
    {
        if (parent == null || textureId < 0)
            return new Transform[0];

        GameObject first = CreateOriginalTextureEffect(
            textureId,
            Vector3.zero,
            Quaternion.identity,
            Vector2.one,
            0f,
            Color.white,
            false);
        if (first == null)
            return new Transform[0];

        GameObject second = CreateOriginalTextureEffect(
            textureId,
            Vector3.zero,
            Quaternion.identity,
            Vector2.one,
            0f,
            Color.white,
            false);
        first.name = "TestPlaySwordBeam_" + layerName + "_0";
        first.transform.SetParent(parent, false);
        if (second == null)
            return new[] { first.transform };

        second.name = "TestPlaySwordBeam_" + layerName + "_1";
        second.transform.SetParent(parent, false);
        return new[] { first.transform, second.transform };
    }

    static void DestroyRuntimeObject(GameObject target)
    {
        if (target == null)
            return;
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    public bool HasOriginalTexture(int textureId)
    {
        TestPlayTextureBinding binding = FindOriginalTexture(originalTextures, textureId);
        return binding != null && binding.texture != null;
    }

    public Texture2D GetOriginalTexture(int textureId)
    {
        TestPlayTextureBinding binding = FindOriginalTexture(originalTextures, textureId);
        return binding != null ? binding.texture : null;
    }

    public bool HasMappedEffect(string key)
    {
        if (effects == null)
            return false;

        for (int i = 0; i < effects.Count; i++)
        {
            TestPlayEffectBinding binding = effects[i];
            if (binding != null && binding.prefab != null &&
                string.Equals(binding.key, key, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public TestPlayPresentationAdapterKind ResolveAudioAdapter(TestPlayPresentationEventType type, string key)
    {
        List<TestPlayAudioBinding> bindings = type == TestPlayPresentationEventType.Voice
            ? voices
            : sounds;
        TestPlayAudioBinding binding = FindBinding(bindings, key);
        return binding != null && binding.clip != null
            ? TestPlayPresentationAdapterKind.AudioClip
            : TestPlayPresentationAdapterKind.None;
    }

    void Unbind()
    {
        if (controller != null)
            controller.PresentationEventRaised -= HandlePresentationEvent;
    }

    void HandlePresentationEvent(TestPlayPresentationEvent presentationEvent)
    {
        switch (presentationEvent.type)
        {
            case TestPlayPresentationEventType.Sound:
                PlayBinding(sounds, presentationEvent.symbol, soundSource, "Snd");
                break;
            case TestPlayPresentationEventType.Voice:
                PlayBinding(voices, presentationEvent.symbol, voiceSource, "Voice");
                break;
        }
    }

    void PlayBinding(List<TestPlayAudioBinding> bindings, string key, AudioSource source, string command)
    {
        if (source == null)
            return;

        TestPlayAudioBinding binding = FindBinding(bindings, key);
        if (binding == null || binding.clip == null)
        {
            string missingKey = command + ":" + (key ?? "");
            if (logMissingAudio && missingAudioKeys.Add(missingKey))
                Debug.LogWarning("[TestPlay][Audio] Missing original resource mapping: " + missingKey);
            return;
        }

        source.PlayOneShot(binding.clip, Mathf.Clamp01(binding.volume));
    }

    public static TestPlayAudioBinding FindBinding(List<TestPlayAudioBinding> bindings, string key)
    {
        if (bindings == null)
            return null;

        for (int i = 0; i < bindings.Count; i++)
        {
            TestPlayAudioBinding binding = bindings[i];
            if (binding != null && string.Equals(binding.key, key, StringComparison.OrdinalIgnoreCase))
                return binding;
        }
        return null;
    }

    public static TestPlayTextureBinding FindOriginalTexture(List<TestPlayTextureBinding> bindings, int id)
    {
        if (bindings == null)
            return null;

        for (int i = 0; i < bindings.Count; i++)
        {
            TestPlayTextureBinding binding = bindings[i];
            if (binding != null && binding.scriptTextureId == id)
                return binding;
        }
        return null;
    }

    public static TestPlayTextureBinding FindOriginalTextureByName(List<TestPlayTextureBinding> bindings, string fileName)
    {
        if (bindings == null)
            return null;

        for (int i = 0; i < bindings.Count; i++)
        {
            TestPlayTextureBinding binding = bindings[i];
            if (binding != null && string.Equals(binding.originalFileName, fileName, StringComparison.OrdinalIgnoreCase))
                return binding;
        }
        return null;
    }

    static void PreloadBindings(List<TestPlayAudioBinding> bindings)
    {
        if (bindings == null)
            return;

        for (int i = 0; i < bindings.Count; i++)
        {
            AudioClip clip = bindings[i] != null ? bindings[i].clip : null;
            if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();
        }
    }

    void EnsureAudioSources()
    {
        AudioSource[] existing = GetComponents<AudioSource>();
        if (soundSource == null && existing.Length > 0)
            soundSource = existing[0];
        if (voiceSource == null && existing.Length > 1)
            voiceSource = existing[1];

        if (soundSource == null)
            soundSource = CreateAudioSource("TestPlayAudio_Snd");
        if (voiceSource == null)
            voiceSource = CreateAudioSource("TestPlayAudio_Voice");

        ConfigureAudioSource(soundSource);
        ConfigureAudioSource(voiceSource);
    }

    AudioSource CreateAudioSource(string objectName)
    {
        GameObject emitter = new GameObject(objectName);
        emitter.transform.SetParent(transform, false);
        return emitter.AddComponent<AudioSource>();
    }

    void ConfigureAudioSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.spatialBlend = useSpatialAudio ? 1f : 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1f;
        source.maxDistance = Mathf.Max(1f, audioMaxDistance);
    }

    void AttachEmitter(AudioSource source, Transform sourceRoot)
    {
        if (source == null || source.gameObject == gameObject)
            return;

        source.transform.SetParent(sourceRoot, false);
        source.transform.localPosition = Vector3.zero;
    }
}
