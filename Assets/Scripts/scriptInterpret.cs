using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.Text.RegularExpressions;

public enum scriptVarType {EMPTY,STR, NUM }
public struct scriptVar
{
    public scriptVarType type;
    public string str;
    public float num;
}

public delegate void scriptFunc(scriptVar[] values);
public delegate void scriptSetVar(scriptVar value);
public delegate scriptVar scriptGetVar();


public class scriptInterpreter
{
    string[] lines;
    int lineLoc = 0;
    int invalidLine = 0;
    List<string> invalidLines = new List<string> ();
    public Dictionary<string, scriptFunc> registeredFunc = new Dictionary<string, scriptFunc>();
    public Dictionary<string, scriptSetVar> registeredSetVar = new Dictionary<string, scriptSetVar>();
    public Dictionary<string, scriptGetVar> registeredGetVar = new Dictionary<string,scriptGetVar>();
    public Dictionary<string, scriptVar> registeredStaticVar = new Dictionary<string, scriptVar> ();

    // --- symbol collection ---
    static readonly Regex IdentifierRegex = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    public readonly Dictionary<string, int> calledFunctions = new Dictionary<string, int>();
    public readonly Dictionary<string, int> unknownFunctions = new Dictionary<string, int>();
    public readonly Dictionary<string, int> setVariables = new Dictionary<string, int>();
    public readonly Dictionary<string, int> unknownSetVariables = new Dictionary<string, int>();
    public readonly Dictionary<string, int> getVariables = new Dictionary<string, int>();
    public readonly Dictionary<string, int> staticVariables = new Dictionary<string, int>();
    public readonly Dictionary<string, int> unknownIdentifiers = new Dictionary<string, int>();
    bool hasNewSymbols = false;
    public bool HasNewSymbols => hasNewSymbols;
    public bool LogLines { get; set; } = true;

    static void Inc(Dictionary<string, int> dict, string key, ref bool changedFlag)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;
        if (dict.TryGetValue(key, out var v))
            dict[key] = v + 1;
        else
        {
            dict[key] = 1;
            changedFlag = true;
        }
    }
    
    /// <summary>
    /// スクリプトを解析して（必要なら）登録済み関数/変数setterを実行します。
    /// invokeCallbacks=false の場合は実行せず、シンボル収集のみ行います。
    /// </summary>
    public void runScript(string script, bool invokeCallbacks = true)
    {
        invalidLine = 0;
        if (string.IsNullOrEmpty(script))
            return;
        lines = script.Split((char)0x0A);
        lineLoc = 0;
        for (; lineLoc < lines.Length; lineLoc++)
        {
            string line = lines[lineLoc].Trim();
            InterpretLine(line, invokeCallbacks);
        }
    }

    public void InterpretLine(string line, bool invokeCallbacks = true)
    { 
        if (string.IsNullOrWhiteSpace(line))
            return;

        if (LogLines)
            Debug.Log(line);
        string[] s = line.Split(';');
        if (line.Length > 0 && line[0] == '\'')
            return;

        // 括弧も代入もない「命令単体」(例: ELSE; / ENDIF; 等)を拾う
        // 末尾の ; 以降は既にsplitされるので s[0] を使う
        var head = (s.Length > 0 ? s[0] : "").Trim();
        if (!string.IsNullOrEmpty(head) && !head.Contains("(") && !head.Contains(")") && !head.Contains("="))
        {
            var fName0 = head;
            Inc(calledFunctions, fName0, ref hasNewSymbols);
            if (registeredFunc.TryGetValue(fName0, out var func0))
            {
                if (invokeCallbacks)
                    func0(new scriptVar[0]);
            }
            else
            {
                invalidLine++;
                invalidLines.Add(line);
                Inc(unknownFunctions, fName0, ref hasNewSymbols);
                Debug.LogWarning($"[scriptInterpreter] Unknown function: {fName0}");
            }
            return;
        }

        if (line.Contains("(") && line.Contains(")"))
        {
            int pLeftLoc = line.IndexOf("(");
            int pRightLoc = line.IndexOf(")");
            if (pLeftLoc < pRightLoc)
            {
                
                s = s[0].Split("(".ToCharArray());
                string fName = s[0].Trim();
                Inc(calledFunctions, fName, ref hasNewSymbols);

                s = s[1].Split(")".ToCharArray());
                s = s[0].Split(",".ToCharArray());
                if (fName.Trim() == "IF")
                {
                    interpretIFStatement(s, invokeCallbacks);
                }
                else
                {
                    scriptVar[] v = new scriptVar[s.Length];
                    for (int i = 0; i < s.Length; i++)
                    {
                        v[i] = interpretVariable(s[i]);

                    }
                    if (registeredFunc.TryGetValue(fName, out var func))
                    {
                        if (invokeCallbacks)
                            func(v);
                    }
                    else
                    {
                        invalidLine++;
                        invalidLines.Add(line);
                        Inc(unknownFunctions, fName, ref hasNewSymbols);
                        Debug.LogWarning($"[scriptInterpreter] Unknown function: {fName}");
                    }
                }
            }
        }
        else if (line.Contains("="))
        {
            s = s[0].Split('=');
            var varName = s[0].Trim();
            Inc(setVariables, varName, ref hasNewSymbols);
            if (registeredSetVar.TryGetValue(varName, out var setter))
            {
                if (invokeCallbacks)
                    setter(interpretVariable(s[1]));
                else
                    interpretVariable(s[1]);
            }
            else
            {
                invalidLine++;
                invalidLines.Add(line);
                Inc(unknownSetVariables, varName, ref hasNewSymbols);
                Debug.LogWarning($"[scriptInterpreter] Unknown variable setter: {varName}");
            }
        }
    }
    
    public void interpretIFStatement(string[] s, bool invokeCallbacks = true)
    {
        scriptVar[] v = new scriptVar[2];
        v[0] = interpretVariable(s[0]);
        v[1] = interpretVariable(s[2]);
        bool conditional = false;
        if (v[0].type == scriptVarType.NUM && v[1].type == scriptVarType.NUM)
        {
            switch(s[1].Trim())
            {
                case "==":
                    conditional = v[0].num == v[1].num;
                    break;
                case ">=":
                    conditional = v[0].num >= v[1].num;
                    break;
                case "<=":
                    conditional = v[0].num <= v[1].num;
                    break;
            }
        }
        lineLoc++;
        for (; lineLoc < lines.Length; lineLoc++)
        {
            string line = lines[lineLoc].Trim();
            if (line.Contains("ELSE;"))
                conditional = !conditional;

            if (line.Contains("ENDIF;"))
                return;
            
            if (conditional)
                InterpretLine(line, invokeCallbacks);
        }
    }

    public void registerFunction(string name, scriptFunc f)
    {
        registeredFunc.Add(name, f);
    }

    public void registerVariable(string name, scriptSetVar sv, scriptGetVar gv)
    {
        registeredSetVar.Add(name, sv);
        registeredGetVar.Add(name, gv);
    } 

    public void registerStaticVariable(string name, scriptVar value)
    {
        if (registeredStaticVar.ContainsKey(name))
            registeredStaticVar[name] = value;
        else
            registeredStaticVar.Add(name, value);
    }

    public scriptVar interpretVariable(string text)
    {
        scriptVar v = new scriptVar();
        v.num = 0;
        v.str = "";
        float f;
        var t = (text ?? "").Trim();
        if (t.Length == 0)
        {
            v.type = scriptVarType.EMPTY;
            return v;
        }

        // 文字列リテラルっぽいものはそのまま扱う（識別子収集から除外）
        if ((t.StartsWith("\"") && t.EndsWith("\"")) || (t.StartsWith("'") && t.EndsWith("'")))
        {
            v.type = scriptVarType.STR;
            v.str = t;
            return v;
        }

        if (float.TryParse(t, out f))
        {
            v.type = scriptVarType.NUM;
            v.num = f;
        }
        else if (registeredGetVar.ContainsKey(t))
        {
            Inc(getVariables, t, ref hasNewSymbols);
            v = registeredGetVar[t]();
        }
        else if (registeredStaticVar.ContainsKey(t))
        {
            Inc(staticVariables, t, ref hasNewSymbols);
            v = registeredStaticVar[t];
        }
        else
        {
            v.type = scriptVarType.STR;
            v.str = t;
            if (IdentifierRegex.IsMatch(t))
                Inc(unknownIdentifiers, t, ref hasNewSymbols);
        }

        return v;
    }

    public string BuildReport(bool resetNewFlag = true)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== ANI Script Symbols Report ===");
        AppendDict(sb, "Functions (called)", calledFunctions);
        AppendDict(sb, "Functions (unknown)", unknownFunctions);
        AppendDict(sb, "Variables (set LHS)", setVariables);
        AppendDict(sb, "Variables (unknown setters)", unknownSetVariables);
        AppendDict(sb, "Variables (get via registeredGetVar)", getVariables);
        AppendDict(sb, "Variables (static via registeredStaticVar)", staticVariables);
        AppendDict(sb, "Identifiers (unclassified)", unknownIdentifiers);
        if (resetNewFlag)
            hasNewSymbols = false;
        return sb.ToString();
    }

    static void AppendDict(StringBuilder sb, string title, Dictionary<string, int> dict)
    {
        sb.AppendLine();
        sb.AppendLine($"## {title} ({dict.Count})");
        foreach (var kv in dict)
            sb.AppendLine($"{kv.Key}\t{kv.Value}");
    }

    public scriptVar convertFloat(float f)
    {
        scriptVar v = new scriptVar();
        v.type = scriptVarType.NUM;
        v.num = f;
        v.str = "";
        return v;
    }

    public scriptVar convertString(string s)
    {
        scriptVar v = new scriptVar();
        v.type = scriptVarType.STR;
        v.str = s;
        v.num = 0;
        return v;
    }

    
}
