using System;
using UnityEngine;

[Serializable]
public class TestPlayStateTable
{
    public int[] ints = new int[256];
    public float[] floats = new float[256];

    public void ResetDefaults()
    {
        Array.Clear(ints, 0, ints.Length);
        Array.Clear(floats, 0, floats.Length);

        floats[100] = 1000f; // Energy candidate.
        ints[150] = 1;       // Grounded candidate.
        ints[152] = 0;       // 0 = gun, 1 = sword.
        ints[156] = 1;       // Self state: standing candidate.
    }

    public int GetInt(int index)
    {
        return IsValid(index, ints.Length) ? ints[index] : 0;
    }

    public void SetInt(int index, int value)
    {
        if (IsValid(index, ints.Length))
            ints[index] = value;
    }

    public float GetFloat(int index)
    {
        return IsValid(index, floats.Length) ? floats[index] : 0f;
    }

    public void SetFloat(int index, float value)
    {
        if (IsValid(index, floats.Length))
            floats[index] = value;
    }

    public bool TryReadReference(string token, out float value)
    {
        value = 0f;
        int index;
        if (TryParseReference(token, "@int", out index))
        {
            value = GetInt(index);
            return true;
        }

        if (TryParseReference(token, "@float", out index))
        {
            value = GetFloat(index);
            return true;
        }

        return false;
    }

    public bool TryApplyReferenceAssignment(string token, string op, float value)
    {
        int index;
        if (TryParseReference(token, "@int", out index))
        {
            SetInt(index, Mathf.RoundToInt(Apply(GetInt(index), op, value)));
            return true;
        }

        if (TryParseReference(token, "@float", out index))
        {
            SetFloat(index, Apply(GetFloat(index), op, value));
            return true;
        }

        return false;
    }

    static float Apply(float current, string op, float value)
    {
        switch (op)
        {
            case "+=": return current + value;
            case "-=": return current - value;
            case "*=": return current * value;
            case "/=": return Mathf.Abs(value) > 0.00001f ? current / value : current;
            default: return value;
        }
    }

    static bool TryParseReference(string token, string prefix, out int index)
    {
        index = -1;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        string t = token.Trim();
        if (!t.StartsWith(prefix + "[", StringComparison.Ordinal) || !t.EndsWith("]", StringComparison.Ordinal))
            return false;

        string raw = t.Substring(prefix.Length + 1, t.Length - prefix.Length - 2);
        return int.TryParse(raw, out index);
    }

    static bool IsValid(int index, int length)
    {
        return index >= 0 && index < length;
    }
}
