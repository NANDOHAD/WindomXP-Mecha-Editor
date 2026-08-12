using System;
using System.Collections.Generic;
using UnityEngine;

public delegate void TestPlayCommandHandler(string name, List<TestPlayScriptValue> args, string rawLine);
public delegate void TestPlayAssignmentHandler(string name, string op, List<TestPlayScriptValue> values, string rawLine);

public class TestPlayScriptVM
{
    public TestPlayStateTable state;
    public TestPlayCommandHandler commandHandler;
    public TestPlayAssignmentHandler assignmentHandler;
    public Action<string> unhandledLineHandler;
    public Func<bool> shouldStopExecution;

    readonly Dictionary<string, string[]> compiledScripts = new Dictionary<string, string[]>();
    string[] statements = new string[0];
    int statementIndex;

    public TestPlayScriptVM(TestPlayStateTable state)
    {
        this.state = state;
    }

    public void Execute(string scriptText)
    {
        if (string.IsNullOrEmpty(scriptText))
            return;

        Compile(scriptText);
        statements = compiledScripts[scriptText];
        statementIndex = 0;
        ExecuteBlock(true);
    }

    public void Compile(string scriptText)
    {
        if (string.IsNullOrEmpty(scriptText) || compiledScripts.ContainsKey(scriptText))
            return;

        List<string> parsed = new List<string>();
        SplitStatements(scriptText, parsed);
        compiledScripts.Add(scriptText, parsed.ToArray());
    }

    public void ClearCompiledScripts()
    {
        compiledScripts.Clear();
        statements = new string[0];
    }

    BlockTerminator ExecuteBlock(bool execute)
    {
        while (statementIndex < statements.Length)
        {
            string statement = CleanLine(statements[statementIndex]);
            if (string.Equals(statement, "ELSE", StringComparison.OrdinalIgnoreCase))
            {
                statementIndex++;
                return BlockTerminator.Else;
            }

            if (string.Equals(statement, "ENDIF", StringComparison.OrdinalIgnoreCase))
            {
                statementIndex++;
                return BlockTerminator.EndIf;
            }

            if (StartsWithCommand(statement, "IF"))
            {
                bool condition = execute && EvaluateIf(statement);
                statementIndex++;

                BlockTerminator firstTerminator = ExecuteBlock(condition);
                if (firstTerminator == BlockTerminator.Aborted)
                    return firstTerminator;

                if (firstTerminator == BlockTerminator.Else)
                {
                    BlockTerminator secondTerminator = ExecuteBlock(execute && !condition);
                    if (secondTerminator == BlockTerminator.Aborted)
                        return secondTerminator;
                }

                continue;
            }

            statementIndex++;
            if (!execute || string.IsNullOrEmpty(statement))
                continue;

            ExecuteLine(statement);
            if (shouldStopExecution != null && shouldStopExecution())
                return BlockTerminator.Aborted;
        }

        return BlockTerminator.EndOfScript;
    }

    void ExecuteLine(string rawLine)
    {
        string line = CleanLine(rawLine);
        if (string.IsNullOrEmpty(line))
            return;

        string op;
        int opIndex;
        if (TryFindAssignment(line, out op, out opIndex))
        {
            string left = line.Substring(0, opIndex).Trim();
            string right = line.Substring(opIndex + op.Length).Trim();
            if (right.StartsWith("(", StringComparison.Ordinal) && right.EndsWith(")", StringComparison.Ordinal))
                right = right.Substring(1, right.Length - 2);

            List<TestPlayScriptValue> values = ParseArgs(right);
            float numberValue = values.Count > 0 ? values[0].AsFloat() : 0f;
            if (state != null && state.TryApplyReferenceAssignment(left, op, numberValue))
                return;

            assignmentHandler?.Invoke(left, op, values, rawLine);
            return;
        }

        int paren = line.IndexOf('(');
        int close = line.LastIndexOf(')');
        if (paren > 0 && close > paren)
        {
            string name = line.Substring(0, paren).Trim();
            string argsText = line.Substring(paren + 1, close - paren - 1);
            commandHandler?.Invoke(name, ParseArgs(argsText), rawLine);
            return;
        }

        commandHandler?.Invoke(line, new List<TestPlayScriptValue>(), rawLine);
    }

    bool EvaluateIf(string ifLine)
    {
        int paren = ifLine.IndexOf('(');
        int close = ifLine.LastIndexOf(')');
        if (paren < 0 || close <= paren)
        {
            unhandledLineHandler?.Invoke(ifLine);
            return false;
        }

        List<string> tokens = SplitArgs(ifLine.Substring(paren + 1, close - paren - 1));
        if (tokens.Count < 3)
        {
            unhandledLineHandler?.Invoke(ifLine);
            return false;
        }

        return EvaluateCondition(tokens[0], tokens[1], tokens[2]);
    }

    bool EvaluateCondition(string left, string op, string right)
    {
        TestPlayScriptValue leftValue;
        TestPlayScriptValue rightValue;
        TestPlayScriptValue.TryParse(left, state, out leftValue);
        TestPlayScriptValue.TryParse(right, state, out rightValue);

        float a = leftValue.AsFloat();
        float b = rightValue.AsFloat();
        switch ((op ?? "").Trim())
        {
            case "==": return Mathf.Approximately(a, b);
            case "!=": return !Mathf.Approximately(a, b);
            case ">=": return a >= b;
            case "<=": return a <= b;
            case ">": return a > b;
            case "<": return a < b;
            default:
                unhandledLineHandler?.Invoke("IF operator: " + op);
                return false;
        }
    }

    List<TestPlayScriptValue> ParseArgs(string argsText)
    {
        List<string> rawArgs = SplitArgs(argsText);
        List<TestPlayScriptValue> values = new List<TestPlayScriptValue>();
        for (int i = 0; i < rawArgs.Count; i++)
        {
            TestPlayScriptValue value;
            TestPlayScriptValue.TryParse(rawArgs[i], state, out value);
            values.Add(value);
        }
        return values;
    }

    static List<string> SplitArgs(string argsText)
    {
        List<string> args = new List<string>();
        if (string.IsNullOrWhiteSpace(argsText))
            return args;

        int start = 0;
        bool inQuote = false;
        char quote = '\0';
        for (int i = 0; i < argsText.Length; i++)
        {
            char c = argsText[i];
            if ((c == '"' || c == '\'') && (i == 0 || argsText[i - 1] != '\\'))
            {
                if (!inQuote)
                {
                    inQuote = true;
                    quote = c;
                }
                else if (quote == c)
                {
                    inQuote = false;
                }
            }
            else if (c == ',' && !inQuote)
            {
                args.Add(argsText.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }

        args.Add(argsText.Substring(start).Trim());
        return args;
    }

    static string CleanLine(string rawLine)
    {
        string line = (rawLine ?? "").Trim();
        if (line.Length == 0)
            return "";
        if (line.StartsWith("'", StringComparison.Ordinal))
            return "";
        if (line.EndsWith(";", StringComparison.Ordinal))
            line = line.Substring(0, line.Length - 1).Trim();
        return line;
    }

    static void SplitStatements(string scriptText, List<string> output)
    {
        if (string.IsNullOrEmpty(scriptText))
            return;

        int start = 0;
        bool inDoubleQuote = false;
        bool inSingleQuote = false;
        bool inComment = false;
        int parenthesisDepth = 0;

        for (int i = 0; i < scriptText.Length; i++)
        {
            char c = scriptText[i];

            if (inComment)
            {
                if (c == '\r' || c == '\n')
                {
                    inComment = false;
                    start = i + 1;
                }
                continue;
            }

            if (c == '"' && !inSingleQuote && (i == 0 || scriptText[i - 1] != '\\'))
            {
                inDoubleQuote = !inDoubleQuote;
                continue;
            }

            if (c == '\'' && !inDoubleQuote && (i == 0 || scriptText[i - 1] != '\\'))
            {
                // The original parser treats an apostrophe outside a quoted argument as
                // a comment marker through the end of the physical line.
                if (!inSingleQuote && parenthesisDepth == 0)
                {
                    AddStatement(scriptText, start, i, output);
                    inComment = true;
                    start = i + 1;
                    continue;
                }

                inSingleQuote = !inSingleQuote;
                continue;
            }

            if (inDoubleQuote || inSingleQuote)
                continue;

            if (c == '(')
                parenthesisDepth++;
            else if (c == ')' && parenthesisDepth > 0)
                parenthesisDepth--;

            if (c == ';' || c == '\r' || c == '\n')
            {
                AddStatement(scriptText, start, i, output);
                start = i + 1;
            }
        }

        if (!inComment)
            AddStatement(scriptText, start, scriptText.Length, output);
    }

    static void AddStatement(string text, int start, int end, List<string> output)
    {
        if (end <= start)
            return;

        string statement = text.Substring(start, end - start).Trim();
        if (!string.IsNullOrEmpty(statement))
            output.Add(statement);
    }

    enum BlockTerminator
    {
        EndOfScript,
        Else,
        EndIf,
        Aborted
    }

    static bool StartsWithCommand(string line, string command)
    {
        if (!line.StartsWith(command, StringComparison.OrdinalIgnoreCase))
            return false;

        int index = command.Length;
        while (index < line.Length && char.IsWhiteSpace(line[index]))
            index++;
        return index < line.Length && line[index] == '(';
    }

    static bool TryFindAssignment(string line, out string op, out int index)
    {
        int firstParen = line.IndexOf('(');
        string[] ops = { "+=", "-=", "*=", "/=", "=" };
        for (int i = 0; i < ops.Length; i++)
        {
            index = line.IndexOf(ops[i], StringComparison.Ordinal);
            if (index >= 0)
            {
                if (firstParen >= 0 && index > firstParen)
                    continue;
                if (ops[i] == "=" && IsComparisonEquals(line, index))
                    continue;
                op = ops[i];
                return true;
            }
        }

        op = "";
        index = -1;
        return false;
    }

    static bool IsComparisonEquals(string line, int index)
    {
        if (index > 0 && (line[index - 1] == '=' || line[index - 1] == '!' || line[index - 1] == '<' || line[index - 1] == '>'))
            return true;
        return index + 1 < line.Length && line[index + 1] == '=';
    }
}
