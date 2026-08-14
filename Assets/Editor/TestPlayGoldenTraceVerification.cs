using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class TestPlayGoldenTraceVerification
{
    const string MechId = "ガンダムTR-1ヘイズル改";
    static bool running;

    sealed class RunCapture
    {
        public TestPlayGoldenTraceSession session;
        public readonly HashSet<int> observedActions = new HashSet<int>();
    }

    [MenuItem("Tools/WindomXP/Test Play/Run Real-Mech Golden Traces")]
    public static async void RunFromMenu()
    {
        if (running)
        {
            Debug.LogWarning("[TestPlayGolden] A real-mech golden trace run is already active.");
            return;
        }

        running = true;
        try
        {
            await RunAllAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError("[TestPlayGolden] Failed: " + ex);
        }
        finally
        {
            running = false;
        }
    }

    static async Task RunAllAsync()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string mechFolder = Path.Combine(projectRoot, "Windom_Data", "Robo", MechId);
        string aniPath = Path.Combine(mechFolder, "Script.ani");
        string sptPath = Path.Combine(mechFolder, "Script.spt");
        if (!File.Exists(aniPath) || !File.Exists(sptPath))
            throw new FileNotFoundException("Real-mech Script.ani/Script.spt is missing: " + mechFolder);

        string aniHash = ComputeFileSha256(aniPath);
        string sptHash = ComputeFileSha256(sptPath);
        Debug.Log("[TestPlayGolden] Loading real ANI: " + MechId + " sha256=" + aniHash);

        ani2 data = new ani2();
        if (!await data.load(aniPath))
            throw new InvalidDataException("ani2.load returned false for " + aniPath);
        if (data.animations == null || data.animations.Count < 200)
            throw new InvalidDataException("Expected the original 200-slot ANI table.");

        CypherTranscoder transcoder = new CypherTranscoder();
        string sptText = USEncoder.ToEncoding.ToUnicode(transcoder.Transcode(sptPath));
        SptRuntimeData sptData = SptParser.Parse(sptText);
        string outputFolder = Path.Combine(projectRoot, "Logs", "TestPlayGolden");
        Directory.CreateDirectory(outputFolder);

        int passed = 0;
        IReadOnlyList<TestPlayGoldenScenarioDefinition> definitions = TestPlayGoldenScenarioCatalog.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            TestPlayGoldenScenarioDefinition definition = definitions[i];
            ValidateRealDataRequirements(data, definition);
            RunCapture first = RunScenario(data, sptData, definition, aniHash, sptHash);
            RunCapture second = RunScenario(data, sptData, definition, aniHash, sptHash);
            int mismatch = TestPlayGoldenTraceSession.FindFirstMismatch(first.session, second.session);
            if (mismatch >= 0)
                throw new InvalidOperationException(definition.id + " diverged at tick index " + mismatch);
            ValidateObservedActions(definition, first.observedActions);

            string firstHash = first.session.ComputeTraceHash();
            string secondHash = second.session.ComputeTraceHash();
            if (!string.Equals(firstHash, secondHash, StringComparison.Ordinal))
                throw new InvalidOperationException(definition.id + " session hashes differ.");

            File.WriteAllText(
                Path.Combine(outputFolder, definition.id + ".unity-reference.jsonl"),
                first.session.SerializeJsonLines(),
                new UTF8Encoding(false));
            Debug.Log("[TestPlayGolden] " + definition.id + " passed ticks=" +
                      first.session.TickCount + " sha256=" + firstHash);
            passed++;
            await Task.Yield();
        }

        Debug.Log("[TestPlayGolden] Passed " + passed + "/" + definitions.Count +
                  " real-mech scenarios twice with exact tick-trace equality. Output=" + outputFolder);
    }

    static RunCapture RunScenario(
        ani2 data,
        SptRuntimeData sptData,
        TestPlayGoldenScenarioDefinition definition,
        string aniHash,
        string sptHash)
    {
        GameObject host = new GameObject("TestPlayGolden_" + definition.id);
        GameObject root = new GameObject("TestPlayGoldenRoot");
        GameObject targetObject = new GameObject("TestPlayGoldenTarget");
        try
        {
            RoboStructure robo = host.AddComponent<RoboStructure>();
            robo.root = root;
            robo.parts = new List<GameObject> { root };
            robo.ani = data;
            robo.folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "Windom_Data", "Robo", MechId);
            robo.transcoder = new CypherTranscoder();

            UI_SPT spt = host.AddComponent<UI_SPT>();
            spt.robo = robo;
            PropertyInfo property = typeof(UI_SPT).GetProperty(
                "LastSptData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            property.SetValue(spt, sptData, null);

            TestPlayTargetDummy target = targetObject.AddComponent<TestPlayTargetDummy>();
            target.logHits = false;
            targetObject.transform.position = Vector3.forward * 2f;

            TestPlayController controller = host.AddComponent<TestPlayController>();
            controller.robo = robo;
            controller.sptSource = spt;
            controller.target = target;
            controller.useColliderGrounding = false;
            controller.logUnhandledCommands = false;
            controller.logMotionAssignments = false;
            controller.blendActionTransitions = false;
            controller.BeginDeterministicTraceSession(definition.setup);

            RunCapture capture = new RunCapture
            {
                session = new TestPlayGoldenTraceSession(new TestPlayGoldenSessionHeader
                {
                    schemaVersion = 1,
                    source = "unity-core",
                    scenarioId = definition.id,
                    mechId = MechId,
                    aniHash = aniHash,
                    sptHash = sptHash,
                    tickRate = Mathf.RoundToInt(controller.originalTickRate),
                    baseline = definition.baseline
                })
            };

            TestPlayGoldenInputFrame[] frames = definition.ExpandInputFrames();
            for (int i = 0; i < frames.Length; i++)
            {
                capture.session.AddTick(controller.SimulateDeterministicTraceTick(frames[i]));
                capture.observedActions.Add(controller.currentAnimationIndex);
                capture.observedActions.Add(controller.CurrentActionSelection.logicalActionId);
            }
            controller.EndDeterministicTraceSession();
            return capture;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(targetObject);
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    static void ValidateRealDataRequirements(ani2 data, TestPlayGoldenScenarioDefinition definition)
    {
        for (int i = 0; i < definition.requiredActionIds.Length; i++)
        {
            int actionId = definition.requiredActionIds[i];
            if (!HasUsableAction(data, actionId))
                throw new InvalidDataException(definition.id + " requires missing ANI action " + actionId);
        }

        for (int i = 0; i < definition.requiredCommands.Length; i++)
        {
            string command = definition.requiredCommands[i];
            if (!ContainsCommand(data, command))
                throw new InvalidDataException(definition.id + " requires missing ANI command " + command);
        }
    }

    static void ValidateObservedActions(
        TestPlayGoldenScenarioDefinition definition,
        HashSet<int> observedActions)
    {
        for (int i = 0; i < definition.requiredActionIds.Length; i++)
        {
            int actionId = definition.requiredActionIds[i];
            if (!observedActions.Contains(actionId))
                throw new InvalidOperationException(definition.id + " did not observe action " + actionId);
        }
    }

    static bool HasUsableAction(ani2 data, int actionId)
    {
        if (data == null || data.animations == null || actionId < 0 || actionId >= data.animations.Count)
            return false;
        animation candidate = data.animations[actionId];
        return candidate != null &&
               (!string.IsNullOrWhiteSpace(candidate.squirrelInit) ||
                (candidate.scripts != null && candidate.scripts.Count > 0) ||
                (candidate.frames != null && candidate.frames.Count > 0));
    }

    static bool ContainsCommand(ani2 data, string command)
    {
        if (data == null || data.animations == null || string.IsNullOrWhiteSpace(command))
            return false;
        for (int i = 0; i < data.animations.Count; i++)
        {
            animation candidate = data.animations[i];
            if (candidate == null)
                continue;
            if (!string.IsNullOrEmpty(candidate.squirrelInit) &&
                candidate.squirrelInit.IndexOf(command, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (candidate.scripts == null)
                continue;
            for (int scriptIndex = 0; scriptIndex < candidate.scripts.Count; scriptIndex++)
            {
                if (!string.IsNullOrEmpty(candidate.scripts[scriptIndex].squirrel) &&
                    candidate.scripts[scriptIndex].squirrel.IndexOf(command, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }
        return false;
    }

    static string ComputeFileSha256(string path)
    {
        using (FileStream stream = File.OpenRead(path))
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(stream);
            StringBuilder builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }
}
