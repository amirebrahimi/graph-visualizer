#if UNITY_2018_1_OR_NEWER
#define USE_GRAPHVIEW
#endif
using System;
using System.Collections.Generic;
using GraphVisualizer;
using UnityEditor;
#if USE_GRAPHVIEW
using UnityEditor.Experimental.UIElements;
using UnityEditor.Experimental.UIElements.GraphView;
#endif
using UnityEngine;
#if USE_GRAPHVIEW
using UnityEngine.Experimental.UIElements;
#endif
using UnityEngine.Playables;


public class PlayableGraphVisualizerWindow : EditorWindow, IHasCustomMenu
{
#if USE_GRAPHVIEW
    class PlayableGraphView : GraphView
    {
        public PlayableGraphView ()
        {
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            //this.AddManipulator(new RectangleSelector());
            //this.AddManipulator(new FreehandSelector());

            //Insert(0, new GridBackground());
        }
    }
#endif

    private struct PlayableGraphInfo
    {
        public PlayableGraph graph;
        public string name;
    }

    private IGraphRenderer m_Renderer;
    private IGraphLayout m_Layout;

#if USE_GRAPHVIEW
    GraphView m_GraphView;
    IMGUIContainer m_GraphOnGUI;
#endif

    IList<PlayableGraphInfo> m_GraphInfos = new List<PlayableGraphInfo>();
    private PlayableGraphInfo m_CurrentGraphInfo;
    private GraphSettings m_GraphSettings;
    private bool m_AutoScanScene = true;
    PlayableGraphVisualizer m_Visualizer;

    #region Configuration

    private static readonly float s_ToolbarHeight = 17f;
    private static readonly float s_DefaultMaximumNormalizedNodeSize = 0.8f;
    private static readonly float s_DefaultMaximumNodeSizeInPixels = 100.0f;
    private static readonly float s_DefaultAspectRatio = 1.5f;

    #endregion
    private PlayableGraphVisualizerWindow()
    {
        m_GraphSettings.maximumNormalizedNodeSize = s_DefaultMaximumNormalizedNodeSize;
        m_GraphSettings.maximumNodeSizeInPixels = s_DefaultMaximumNodeSizeInPixels;
        m_GraphSettings.aspectRatio = s_DefaultAspectRatio;
        m_GraphSettings.showLegend = true;
        m_AutoScanScene = true;
    }

    [MenuItem("Window/PlayableGraph Visualizer")]
    public static void ShowWindow()
    {
        GetWindow<PlayableGraphVisualizerWindow>("Playable Graph Visualizer");
    }

    void OnEnable()
    {
#if USE_GRAPHVIEW
        var root = this.GetRootVisualContainer();

        m_GraphOnGUI = new IMGUIContainer(GraphOnGUI);
        m_GraphOnGUI.StretchToParentSize();
        m_GraphOnGUI.name = "Graph";
        root.Add(m_GraphOnGUI);
#endif
    }

    private PlayableGraphInfo GetSelectedGraphInToolBar(IList<PlayableGraphInfo> graphs, PlayableGraphInfo currentGraph)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Width(position.width));

        List<string> options = new List<string>(graphs.Count);// = graphs.Select(d => d.ToString()).ToArray();
        foreach (var g in graphs)
        {
            options.Add(g.name);
        }

        int currentSelection = graphs.IndexOf(currentGraph);
        int newSelection = EditorGUILayout.Popup(currentSelection != -1 ? currentSelection : 0, options.ToArray(), GUILayout.Width(200));

        PlayableGraphInfo selectedDirector = new PlayableGraphInfo();
        if (newSelection != -1)
            selectedDirector = graphs[newSelection];

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        return selectedDirector;
    }

    void ShowMessage(string msg)
    {
#if USE_GRAPHVIEW
        GUILayout.BeginArea(this.GetRootVisualContainer().contentRect);
#endif

        GUILayout.BeginVertical();
        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        GUILayout.Label(msg);

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();
#if USE_GRAPHVIEW
        GUILayout.EndArea();
#endif
    }

    void Update()
    {
#if USE_GRAPHVIEW
        EnumerateGraphs();
        DrawGraph();
#else
        // If in Play mode, refresh the graph each update.
        if (EditorApplication.isPlaying)
            Repaint();
#endif
    }

    void OnInspectorUpdate()
    {
        // If not in Play mode, refresh the graph less frequently.
        if (!EditorApplication.isPlaying)
            Repaint();
    }

#if !USE_GRAPHVIEW
    void OnGUI()
    {
        EnumerateGraphs();
        GraphOnGUI();
        DrawGraph();
    }
#endif

    void EnumerateGraphs()
    {
        m_GraphInfos.Clear();

        PlayableGraphInfo info;

        // If we requested, we extract automatically the PlayableGraphs from all the components
        // that are in the current scene.
        if (m_AutoScanScene)
        {
            // This code could be generalized, maybe if we added a IHasPlayableGraph Interface.
            IList<PlayableDirector> directors = FindObjectsOfType<PlayableDirector>();
            if (directors != null)
            {
                foreach (var director in directors)
                {
                    var graph = director.playableGraph;
                    if (graph.IsValid() && graph.GetPlayableCount() > 0)
                    {
                        info.name = director.name;
                        info.graph = graph;
                        m_GraphInfos.Add(info);
                    }
                }
            }

            IList<Animator> animators = FindObjectsOfType<Animator>();
            if (animators != null)
            {
                foreach (var animator in animators)
                {
                    var graph = animator.playableGraph;
                    if (graph.IsValid() && graph.GetPlayableCount() > 0)
                    {
                        info.name = animator.name;
                        info.graph = graph;
                        m_GraphInfos.Add(info);
                    }
                }
            }
        }

        if (GraphVisualizerClient.GetGraphs() != null)
        {
            foreach (var clientGraph in GraphVisualizerClient.GetGraphs())
            {
                var graph = clientGraph.Key;
                if (graph.IsValid() && graph.GetPlayableCount() > 0)
                {
                    info.name = clientGraph.Value;
                    info.graph = graph;
                    m_GraphInfos.Add(info);
                }
            }
        }
    }

    void DrawGraph()
    {
        if (m_Visualizer == null)
            return;

        m_Visualizer.Refresh();

#if USE_GRAPHVIEW
        var root = this.GetRootVisualContainer();

        if (m_Visualizer.IsEmpty())
        {
            if (m_GraphView != null)
            {
                root.Remove(m_GraphView);
                m_GraphView = null;

                while (root.childCount != 1)
                {
                    foreach (var child in root.Children())
                    {
                        if (child != m_GraphOnGUI)
                        {
                            root.Remove(child);
                            break;
                        }
                    }
                }
            }

            return;
        }

        if (m_GraphView == null)
        {
            m_GraphView = new PlayableGraphView();
            m_GraphSettings.customData = m_GraphView;

            root.Add(m_GraphView);
        }
#endif

        if (m_Layout == null)
            m_Layout = new ReingoldTilford();

        m_Layout.CalculateLayout(m_Visualizer);

        var graphRect = new Rect(0, s_ToolbarHeight, position.width, position.height - s_ToolbarHeight);

        if (m_Renderer == null)
        {
#if USE_GRAPHVIEW
            m_Renderer = new GraphViewRenderer();
#else
            m_Renderer = new DefaultGraphRenderer();
#endif
        }

#if USE_GRAPHVIEW
        m_GraphView.layout = graphRect;
        m_Renderer.Draw(m_Layout, graphRect, m_GraphSettings);
#else
        m_Renderer.Draw(m_Layout, graphRect, m_GraphSettings);
#endif

    }

    void GraphOnGUI()
    {
        // Early out if there is no graphs.
        if (m_GraphInfos.Count == 0)
        {
            ShowMessage("No PlayableGraph in the scene");
            return;
        }

        GUILayout.BeginVertical();
        EditorGUI.BeginChangeCheck();
        m_CurrentGraphInfo = GetSelectedGraphInToolBar(m_GraphInfos, m_CurrentGraphInfo);
        if (EditorGUI.EndChangeCheck() || m_Visualizer == null)
            m_Visualizer = new PlayableGraphVisualizer(m_CurrentGraphInfo.graph);
        GUILayout.EndVertical();

        if (!m_CurrentGraphInfo.graph.IsValid())
        {
            ShowMessage("Selected PlayableGraph is invalid");
            return;
        }

        if (m_Visualizer.IsEmpty())
        {
            ShowMessage("Selected PlayableGraph is empty");
            return;
        }
    }

#region Custom_Menu

    public virtual void AddItemsToMenu(GenericMenu menu)
    {
        menu.AddItem(new GUIContent("Legend"), m_GraphSettings.showLegend, ToggleLegend);
        menu.AddItem(new GUIContent("Auto Scan Scene"), m_AutoScanScene, ToggleAutoScanScene);
    }
    void ToggleLegend()
    {
        m_GraphSettings.showLegend = !m_GraphSettings.showLegend;
    }
    void ToggleAutoScanScene()
    {
        m_AutoScanScene = !m_AutoScanScene;
    }

#endregion
}
