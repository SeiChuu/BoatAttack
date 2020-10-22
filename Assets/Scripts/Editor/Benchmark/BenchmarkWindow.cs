using System.Collections.Generic;
using System.Linq;
using BoatAttack.Benchmark;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public class BenchmarkWindow : EditorWindow
{
    private static readonly string benchmarkLoaderPath = "Assets/scenes/benchmark/loader.unity";

    [MenuItem("Tools/Benchmark")]
    static void Init()
    {
        var window = (BenchmarkWindow)GetWindow(typeof(BenchmarkWindow));
        window.Show();
    }

    class Styles
    {
        public static readonly GUIContent[] toolbarOptions = {new GUIContent("Tools"), new GUIContent("Results"), };
        public static GUIStyle richDefault;
    }

    private static string assetGuidKey = "boatattack.benchmark.assetguid";
    private static string assetGUID;
    private static BenchmarkConfigData _benchConfigData;

    private int currentRunIndex = 0;

    public int currentToolbar;
    private const int ToolbarWidth = 150;
    private List<PerfResults> PerfResults = new List<PerfResults>();
    private string[] resultFiles;
    private int currentResult;
    private int currentRun = 0;
    private BuildTarget target;

    // TempUI vars
    private bool resultInfoHeader;
    private bool resultDataHeader;
    private bool toolsBuildHeader;
    private bool toolsSettingsHeader;

    private void OnEnable()
    {
        target = EditorUserBuildSettings.activeBuildTarget;
        assetGUID = EditorPrefs.GetString(assetGuidKey);
        _benchConfigData = AssetDatabase.LoadAssetAtPath<BenchmarkConfigData>(AssetDatabase.GUIDToAssetPath(assetGUID));
    }

    private void OnGUI()
    {
        Styles.richDefault ??= new GUIStyle(EditorStyles.label) {richText = true, alignment = TextAnchor.MiddleCenter};

        EditorGUILayout.Space(5);

        var toolbarRect = EditorGUILayout.GetControlRect();
        toolbarRect.position += new Vector2((toolbarRect.width - ToolbarWidth) * 0.5f, 0f);
        toolbarRect.width = ToolbarWidth;

        currentToolbar = GUI.Toolbar(toolbarRect, currentToolbar,
            Styles.toolbarOptions);

        switch (currentToolbar)
        {
            case 0:
                DrawTools();
                break;
            case 1:
                DrawResults();
                break;
        }
    }

    private void DrawTools()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        toolsBuildHeader = EditorGUILayout.BeginFoldoutHeaderGroup(toolsBuildHeader, "Build");
        if (toolsBuildHeader)
        {
            DrawBuildSettings();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        toolsSettingsHeader = EditorGUILayout.BeginFoldoutHeaderGroup(toolsSettingsHeader, "Benchmark Settings");
        if (toolsSettingsHeader)
        {
            DrawBenchmarkSettings();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.EndVertical();
    }

    private void DrawBuildSettings()
    {
        target = (BuildTarget)EditorGUILayout.EnumPopup(target);
        var maskOptions = _benchConfigData.benchmarkData.Select(data => data.benchmarkName).ToArray();

        EditorGUILayout.BeginHorizontal();

        var mask = -1;
        mask = EditorGUILayout.MaskField("Benchmark Suite", mask, maskOptions);
        if (GUILayout.Button($"Build & Run"))
        {
            BuildBenchmark();
        }
        if (GUILayout.Button($"Run in Editor"))
        {
            RunBenchmark();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        currentRunIndex = EditorGUILayout.Popup("Benchmark Scene", currentRunIndex, maskOptions);
        if (GUILayout.Button($"Build & Run"))
        {
            SetupStaticBenchmark(currentRunIndex);
            BuildStaticBenchmark(currentRunIndex);
        }
        if (GUILayout.Button($"Run in Editor"))
        {
            //setup the things
            SetupStaticBenchmark(currentRunIndex);
            RunBenchmark();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawBenchmarkSettings()
    {
        EditorGUI.BeginChangeCheck();
        _benchConfigData = (BenchmarkConfigData) EditorGUILayout.ObjectField(new GUIContent("Benchmark Data File"), _benchConfigData,
            typeof(BenchmarkConfigData), false);
        if(EditorGUI.EndChangeCheck())
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(_benchConfigData, out assetGUID, out long _);
            EditorPrefs.SetString(assetGuidKey, assetGUID);
        }

        if (_benchConfigData)
        {
            var editor = Editor.CreateEditor(_benchConfigData);
            editor.DrawDefaultInspector();
        }
    }

    private void SetupStaticBenchmark(int index = -1)
    {
        if (index == -1)
        {
            // setup and run all suite
            EditorSceneManager.playModeStartScene = GetBenchmarkLoader();
        }
        else
        {
            // setup and run single scene
            var benchmarkPrefabPath = "Assets/objects/misc/[benchmark].prefab";

            using (var benchmarkPrefab = new PrefabUtility.EditPrefabContentsScope(benchmarkPrefabPath))
            {
                if (benchmarkPrefab.prefabContentsRoot.TryGetComponent(out Benchmark b))
                {
                    b.simpleRun = true;
                    b.simpleRunScene = index;
                }
            }
        }
    }

    private void RunBenchmark()
    {
        EditorSceneManager.playModeStartScene = GetBenchmarkLoader();
        EditorApplication.delayCall += EditorApplication.EnterPlaymode;
    }

    private void BuildBenchmark()
    {
        var buildOptions = new BuildPlayerOptions();

        var sceneList = new List<string> {"Assets/scenes/menu_benchmark.unity"};
        sceneList.AddRange(_benchConfigData.benchmarkData.Select(benchSettings => benchSettings.scene));
        buildOptions.scenes = sceneList.ToArray();

        Build(ref buildOptions);
    }

    private void BuildStaticBenchmark(int index)
    {
        var buildOptions = new BuildPlayerOptions();
        var sceneList = new List<string>
        {
            benchmarkLoaderPath,
            _benchConfigData.benchmarkData[index].scene
        };
        buildOptions.scenes = sceneList.ToArray();
        Build(ref buildOptions);
    }

    private void Build(ref BuildPlayerOptions options)
    {
        string ext;
        switch (target)
        {
            case BuildTarget.Android:
                ext = ".apk";
                break;
            default:
                ext = "";
                break;
        }

        options.locationPathName = $"Builds/Benchmark/{target:G}/BoatattackBenchmark{ext}";
        options.target = target;
        options.targetGroup = BuildPipeline.GetBuildTargetGroup(target);
        options.options = BuildOptions.Development;
        options.options = BuildOptions.AutoRunPlayer;

        var curTar = EditorUserBuildSettings.activeBuildTarget;
        if (target != curTar)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(options.targetGroup, options.target);
        }
        AutoBuildAddressables.Popup();
        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(curTar), curTar);

        switch (summary.result)
        {
            case BuildResult.Succeeded:
                Debug.Log("Benchmark Build Complete");
                break;
            case BuildResult.Failed:
                Debug.LogError("Benchmark Build Failed");
                break;
        }
    }

    private void DrawResults()
    {
        if (PerfResults == null || PerfResults.Count == 0)
        {
            UpdateFiles();
        }

        if (PerfResults != null && PerfResults.Count > 0)
        {
            EditorGUILayout.BeginHorizontal();
            currentResult = EditorGUILayout.Popup(new GUIContent("File"), currentResult, resultFiles);
            if (GUILayout.Button("reload", GUILayout.Width(100)))
            {
                UpdateFiles();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            DrawPerfInfo(PerfResults[currentResult].perfStats[0].info);

            DrawPerf(PerfResults[currentResult].perfStats[0]);
        }
        else
        {
            GUILayout.Label("No Stats found, please run a benchmark.");
        }
    }

    private void DrawPerfInfo(TestInfo info)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        resultInfoHeader = EditorGUILayout.BeginFoldoutHeaderGroup(resultInfoHeader, "Info");
        if (resultInfoHeader)
        {
            var fields = info.GetType().GetFields();
            var half = fields.Length / 2;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            for (var index = 0; index < fields.Length; index++)
            {
                var prop = fields[index];
                EditorGUILayout.LabelField(prop.Name, prop.GetValue(info).ToString(), EditorStyles.boldLabel);
                if (index == half)
                {
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.BeginVertical();
                }
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.EndVertical();
    }

    private void DrawPerf(PerfBasic perfData)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        var options = new string[perfData.RunData.Length + 1];
        options[0] = "Smooth all runs";
        for (int i = 1; i < perfData.RunData.Length + 1; i++)
        {
            options[i] = $"Run {i.ToString()}";
        }

        resultDataHeader = EditorGUILayout.BeginFoldoutHeaderGroup(resultDataHeader, "Data");
        if (resultDataHeader)
        {
            EditorGUILayout.Space(4);

            RunData runData;
            var runtime = 0.0f;
            var avg = 0.0f;
            var min = new FrameData(-1, 0f);
            var max = new FrameData(-1, 0f);
            if (currentRun > 0)
            {
                runData = perfData.RunData[currentRun - 1];
                runtime = runData.RunTime;
                avg = runData.AvgMs;
                min = runData.MinFrame;
                max = runData.MaxFrame;
            }
            else
            {
                avg += perfData.RunData.Sum(data => data.AvgMs / perfData.RunData.Length);
                runtime += perfData.RunData.Sum(data => data.RunTime / perfData.RunData.Length);
            }

            EditorGUILayout.BeginHorizontal();
            {
                EditorGUI.BeginChangeCheck();
                var index = EditorGUILayout.Popup("Display", currentRun, options);
                if (EditorGUI.EndChangeCheck())
                {
                    currentRun = Mathf.Clamp(index, 0, options.Length + 1);
                }
            }
            EditorGUILayout.EndHorizontal();

            var graphRect = EditorGUILayout.GetControlRect(false, 500f);
            DrawGraph(graphRect, perfData, 0, avg * 2f);

            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField($"<b>Runtime:</b> {runtime:F2}s", Styles.richDefault,
                    GUILayout.MaxWidth(120.0f));
                EditorGUILayout.LabelField($"<b>Average:</b> {avg:F2}ms", Styles.richDefault,
                    GUILayout.MaxWidth(120.0f));
                EditorGUILayout.LabelField($"<b>Minimum(fastest):</b> {min.ms:F2}ms (@frame: {min.frameIndex})",
                    Styles.richDefault);
                EditorGUILayout.LabelField($"<b>Maximum(slowest):</b> {max.ms:F2}ms (@frame: {max.frameIndex})",
                    Styles.richDefault);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.EndVertical();
    }

    #region GraphDrawing

    private void DrawGraph(Rect rect, PerfBasic data, float minMS, float maxMS)
    {
        var values = data.RunData;
        var padding = 20f;
        rect.max -= Vector2.one * padding;
        rect.xMax -= 40f;
        rect.min += Vector2.one * padding;


        //draw value markers
        GUI.backgroundColor = new Color(0f, 0f, 0f, 1f);
        GUI.Box(rect, "");
        //GUI.DrawTexture(rect, Texture2D.grayTexture, ScaleMode.StretchToFill);

        DrawGraphMarkers(rect, minMS, maxMS, 5);

        var c = new Color(0.129f, 0.588f, 0.952f, 1.0f);
        if (currentRun == 0)
        {
            var averageValue = new float[values[0].rawSamples.Length];
            foreach (var frames in values)
            {
                for (int i = 0; i < averageValue.Length; i++)
                {
                    averageValue[i] += frames.rawSamples[i] / values.Length;
                }
            }
            DrawGraphLine(rect, averageValue, minMS, maxMS, c);
        }
        else
        {
            DrawGraphLine(rect, values[currentRun-1].rawSamples, minMS, maxMS, c);
            DrawMinMaxMarkers(rect, values[currentRun-1], data.Frames);
        }
    }

    void DrawGraphLine(Rect rect, float[] points, float min, float max, Color color)
    {
        var graphPoints = new Vector3[points.Length];
        for (var j = 0; j < points.Length; j++)
        {
            var valA = rect.yMax - rect.height * GetGraphLerpValue(points[j], min, max);

            var xLerp = new Vector2(j, j + 1) / (points.Length - 1);
            var xA = Mathf.Lerp(rect.xMin, rect.xMax, xLerp.x);
            var posA = new Vector2(xA, valA);
            graphPoints[j] = posA;
        }
        Handles.color = color;
        Handles.DrawAAPolyLine(graphPoints);
    }

    private static void DrawMinMaxMarkers(Rect rect, RunData data, int frames)
    {
        var c = Color.red;
        c.a = 0.5f;
        Handles.color = c;
        var maxX = Mathf.Lerp(rect.xMin, rect.xMax, (float)data.MaxFrame.frameIndex / frames);
        Handles.DrawLine(new Vector2(maxX, rect.yMin), new Vector2(maxX, rect.yMax));

        c = Color.green;
        c.a = 0.5f;
        Handles.color = c;
        var minX = Mathf.Lerp(rect.xMin, rect.xMax, (float)data.MinFrame.frameIndex / frames);
        Handles.DrawLine(new Vector2(minX, rect.yMin), new Vector2(minX, rect.yMax));
    }

    private static void DrawGraphMarkers(Rect rect, float min, float max, int count)
    {
        count--;
        for (int i = 0; i <= count; i++)
        {
            var y = Mathf.Lerp(rect.yMax, rect.yMin, (float)i / count);
            Handles.color = new Color(1f, 1f, 1f, 0.5f);
            Handles.DrawDottedLine(new Vector2(rect.xMin, y), new Vector2(rect.xMax, y), 4);
            y -= EditorGUIUtility.singleLineHeight * 0.5f;
            var val = Mathf.Lerp(min, max, (float) i / count);
            GUI.Label(new Rect(new Vector2(rect.xMax, y), new Vector2(80, EditorGUIUtility.singleLineHeight)), $"{val:F1}ms");
        }
    }

    private float GetGraphLerpValue(float ms)
    {
        return GetGraphLerpValue(ms, 0f, 33.33f);
    }

    private static float GetGraphLerpValue(float ms, float msMin, float msMax)
    {
        var msA = ms;
        return Mathf.InverseLerp(msMin, msMax, msA);
    }

    #endregion

    #region Utilitiess

    private void UpdateFiles()
    {
        PerfResults = Benchmark.LoadAllBenchmarkStats();
        resultFiles = new string[PerfResults.Count];
        for (var index = 0; index < PerfResults.Count; index++)
        {
            resultFiles[index] = PerfResults[index].fileName;
        }
    }

    private static SceneAsset GetBenchmarkLoader()
    {
        return (SceneAsset)AssetDatabase.LoadAssetAtPath(benchmarkLoaderPath, typeof(SceneAsset));
    }

    #endregion
}
