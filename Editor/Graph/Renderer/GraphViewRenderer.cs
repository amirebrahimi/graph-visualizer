#if UNITY_2018_1_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GraphVisualizer;
using UnityEditor;
using UnityEditor.Experimental.UIElements.GraphView;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using UnityEngine.Experimental.UIElements.StyleSheets;
using GraphViewNode = UnityEditor.Experimental.UIElements.GraphView.Node;
using GraphViewEdge = UnityEditor.Experimental.UIElements.GraphView.Edge;
using Node = GraphVisualizer.Node;

public class GraphViewRenderer : IGraphRenderer
{
    public event Action<Node> nodeClicked;
    static readonly Color k_EdgeColorMin = new Color(1.0f, 1.0f, 1.0f, 0.1f);
    static readonly Color k_EdgeColorMax = Color.white;
    static readonly Color k_LegendBackground = new Color(0, 0, 0, 0.1f);

    static readonly float k_BorderSize = 15;
    static readonly float k_LegendFixedOverheadWidth = 100;
//    static readonly float k_DefaultMaximumNormalizedNodeSize = 0.8f;
//    static readonly float k_DefaultMaximumNodeSizeInPixels = 100.0f;
//    static readonly float k_DefaultAspectRatio = 1.5f;

    static readonly int s_NodeMaxFontSize = 14;

    GUIStyle m_LegendLabelStyle;
    GUIStyle m_SubTitleStyle;
    GUIStyle m_InspectorStyle;
    GUIStyle m_NodeRectStyle;

    static readonly int k_ActiveNodeThickness = 2;
    static readonly int k_SelectedNodeThickness = 4;
    static readonly Color k_ActiveNodeColor = Color.white;
    static readonly Color k_SelectedNodeColor = Color.yellow;

    readonly Dictionary<string, NodeTypeLegend> m_LegendForType = new Dictionary<string, NodeTypeLegend>();

    Node m_SelectedNode;
    VisualElement m_LegendContainer;

    Texture2D m_ColorBar;
    Vector2 m_ScrollPos;

    struct NodeTypeLegend
    {
        public Color color;
        public string label;
    }

    public void Reset()
    {
        m_SelectedNode = null;
    }

    public void Draw(IGraphLayout graphLayout, Rect drawingArea)
    {
        throw new NotImplementedException("Must use Draw() with GraphSettings because a graph view is required");
    }

    public void Draw(IGraphLayout graphLayout, Rect totalDrawingArea, GraphSettings graphSettings)
    {
        if (graphSettings.customData == null)
            return;

        var graphView = (GraphView)graphSettings.customData;

        var drawingArea = new Rect(totalDrawingArea);

        PrepareLegend(graphLayout.vertices);

        if (graphSettings.showLegend)
        {
            var kLegendContainer = "Legend";
            var parent = graphView.parent;

            if (m_LegendContainer == null)
            {
                foreach (var child in parent.Children())
                {
                    if (child is IMGUIContainer && child.name == kLegendContainer)
                    {
                        m_LegendContainer = child;
                        break;
                    }
                }

                if (m_LegendContainer == null)
                {
                    m_LegendContainer = new IMGUIContainer(LegendOnGUI) { name = kLegendContainer };
                    parent.Add(m_LegendContainer);
                }
            }
        }
        else if (m_LegendContainer != null)
        {
            m_LegendContainer.parent.Remove(m_LegendContainer);
            m_LegendContainer = null;
        }

        if (m_LegendContainer != null)
        {
            var legendArea = m_LegendContainer.layout;
            drawingArea.width -= legendArea.width;// + s_BorderSize;
            graphView.layout = drawingArea;
        }

        DrawGraph(graphLayout, drawingArea, graphView, graphSettings);
    }

    void InitializeStyles()
    {
        m_LegendLabelStyle = new GUIStyle(GUI.skin.label)
        {
            margin = {top = 0},
            alignment = TextAnchor.UpperLeft
        };

        m_NodeRectStyle = new GUIStyle
        {
            normal =
            {
                background = (Texture2D) Resources.Load("Node"),
                textColor = Color.black,
            },
            border = new RectOffset(10, 10, 10, 10),
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            clipping = TextClipping.Clip

        };

        m_SubTitleStyle = EditorStyles.boldLabel;

        m_InspectorStyle = new GUIStyle
        {
            normal =
            {
                textColor = Color.white,
            },
            richText = true,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = true,
            clipping = TextClipping.Clip

        };
    }

    void PrepareLegend(IEnumerable<Vertex> vertices)
    {
        m_LegendForType.Clear();
        foreach (Vertex v in vertices)
        {
            if (v.node == null)
                continue;

            string nodeType = v.node.GetContentTypeName();

            if (m_LegendForType.ContainsKey(nodeType))
                continue;

            m_LegendForType[nodeType] = new NodeTypeLegend
            {
                label = v.node.GetContentTypeShortName(),
                color = v.node.GetColor()
            };
        }
    }

    float EstimateLegendWidth()
    {
        float legendWidth = 0;
        foreach (NodeTypeLegend legend in m_LegendForType.Values)
        {
            legendWidth = Mathf.Max(legendWidth, GUI.skin.label.CalcSize(new GUIContent(legend.label)).x);
        }
        legendWidth += k_LegendFixedOverheadWidth;
        return legendWidth;
    }

    void DrawRect(Rect rect, Color color, string text, bool active, bool selected = false)
    {
        var originalColor = GUI.color;

        if (selected)
        {
            GUI.color = k_SelectedNodeColor;
            float t = k_SelectedNodeThickness + (active ? k_ActiveNodeThickness : 0.0f);
            GUI.Box(new Rect(rect.x - t, rect.y - t,
                    rect.width + 2 * t, rect.height + 2 * t),
                string.Empty, m_NodeRectStyle);
        }

        if (active)
        {
            GUI.color = k_ActiveNodeColor;
            GUI.Box(new Rect(rect.x - k_ActiveNodeThickness, rect.y - k_ActiveNodeThickness,
                    rect.width + 2 * k_ActiveNodeThickness, rect.height + 2 * k_ActiveNodeThickness),
                string.Empty, m_NodeRectStyle);
        }

        // Body + Text
        GUI.color = color;
        m_NodeRectStyle.fontSize = ComputeFontSize(rect.size, text);
        GUI.Box(rect, text, m_NodeRectStyle);

        GUI.color = originalColor;
    }

    void DrawLegend(Rect legendArea)
    {
        EditorGUI.DrawRect(legendArea, k_LegendBackground);

        // Add a border around legend area
        legendArea.x += k_BorderSize;
        legendArea.width -= k_BorderSize * 2;
        legendArea.y += k_BorderSize;
        legendArea.height -= k_BorderSize * 2;

        GUILayout.BeginArea(legendArea);
        GUILayout.BeginVertical();

        GUILayout.Label("Inspector", m_SubTitleStyle);

        if (m_SelectedNode != null)
        {
            using (var scrollView = new EditorGUILayout.ScrollViewScope(m_ScrollPos))
            {
                m_ScrollPos = scrollView.scrollPosition;
                GUILayout.Label(m_SelectedNode.ToString(), m_InspectorStyle);
            }
        }
        else
        {
            GUILayout.Label("Click on a node\nto display its details.");
        }

        GUILayout.FlexibleSpace();

        GUILayout.Label("Legend", m_SubTitleStyle);

        foreach (var pair in m_LegendForType)
        {
            DrawLegendEntry(pair.Value.color, pair.Value.label, false);
        }

        DrawLegendEntry(Color.gray, "Playing", true);

        GUILayout.Space(20);

        GUILayout.Label("Edge weight", m_SubTitleStyle);
        GUILayout.BeginHorizontal();
        GUILayout.Label("0");
        GUILayout.FlexibleSpace();
        GUILayout.Label("1");
        GUILayout.EndHorizontal();

        DrawEdgeWeightColorBar(legendArea.width);

        GUILayout.Space(20);

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    void DrawLegendEntry(Color color, string label, bool active)
    {
        GUILayout.Space(5);
        GUILayout.BeginHorizontal(GUILayout.Height(20));

        Rect legendIconRect = GUILayoutUtility.GetRect(1, 1, GUILayout.Width(20), GUILayout.Height(20));
        DrawRect(legendIconRect, color, string.Empty, active);

        GUILayout.Label(label, m_LegendLabelStyle);

        GUILayout.EndHorizontal();
    }

    void DrawEdgeWeightColorBar(float width)
    {
        const int nbLevels = 64;

        if (m_ColorBar == null)
        {
            m_ColorBar = new Texture2D(nbLevels, 1)
            {
                wrapMode = TextureWrapMode.Clamp
            };

            var cols = m_ColorBar.GetPixels();
            for (int x = 0; x < nbLevels; x++)
            {
                Color c = Color.Lerp(k_EdgeColorMin, k_EdgeColorMax, (float) x / nbLevels);
                cols[x] = c;
            }

            m_ColorBar.SetPixels(cols);
            m_ColorBar.Apply(false);
        }

        const int colorbarHeight = 20;
        GUI.DrawTexture(GUILayoutUtility.GetRect(width, colorbarHeight), m_ColorBar);
    }

    void UpdateSelection(GraphView graphView)
    {
        var selection = graphView.selection;

        if (selection.Count > 0)
        {
            Node selectedNode = null;
            foreach (var s in selection)
            {
                var gvNode = (GraphViewNode)s;
                var node = (Node)gvNode.userData;
                if (m_SelectedNode == null || !m_SelectedNode.content.Equals(node.content))
                    selectedNode = node;
            }

            var selectionChanged = m_SelectedNode != selectedNode;
            m_SelectedNode = selectedNode;

            if (nodeClicked != null && selectionChanged)
                nodeClicked(m_SelectedNode);
        }
    }

    void LegendOnGUI()
    {
        if (m_LegendLabelStyle == null)
            InitializeStyles();

        var totalDrawingArea = m_LegendContainer.parent.layout;
        var legendArea = new Rect(totalDrawingArea)
        {
            width = EstimateLegendWidth() + k_BorderSize * 2
        };

        legendArea.x = totalDrawingArea.xMax - legendArea.width;

        m_LegendContainer.layout = legendArea;

        legendArea.x = 0f;
        legendArea.y = 0f;
        DrawLegend(legendArea);
    }

    // Draw the graph and returns the selected Node if there's any.
    void DrawGraph(IGraphLayout graphLayout, Rect drawingArea, GraphView graphView, GraphSettings graphSettings)
    {
        // add border, except on right-hand side where the legend will provide necessary padding
        drawingArea = new Rect(drawingArea.x + k_BorderSize,
            drawingArea.y + k_BorderSize,
            drawingArea.width - k_BorderSize * 2,
            drawingArea.height - k_BorderSize * 2);

        UpdateSelection(graphView);

        var b = new Bounds(Vector3.zero, Vector3.zero);
        foreach (Vertex v in graphLayout.vertices)
        {
            b.Encapsulate(new Vector3(v.position.x, v.position.y, 0.0f));
        }

        // Increase b by maximum node size (since b is measured between node centers)
        b.Expand(new Vector3(graphSettings.maximumNormalizedNodeSize, graphSettings.maximumNormalizedNodeSize, 0));

        var scale = new Vector2(drawingArea.width / b.size.x, drawingArea.height / b.size.y);
        var offset = new Vector2(-b.min.x, -b.min.y);

        Vector2 nodeSize = ComputeNodeSize(scale, graphSettings);

        // NOTE: Clear doesn't currently do what you expect, you have to remove the GraphView elements manually
        graphView.graphElements.ForEach(graphView.RemoveElement);

        var nodeLookup = new Dictionary<Vertex, GraphViewNode>();

        foreach (Vertex v in graphLayout.vertices)
        {
            Vector2 nodeCenter = ScaleVertex(v.position, offset, scale) - nodeSize / 2;
            var nodeRect = new Rect(nodeCenter.x, nodeCenter.y, nodeSize.x, nodeSize.y);

            var node = v.node;

            // TODO: Replace w/ TokenNode or the super collapsed node from the VFX editor
            var gvNode = new GraphViewNode();
            gvNode.userData = node;
            nodeLookup[v] = gvNode;

            graphView.AddElement(gvNode);

            bool currentSelection = (m_SelectedNode != null)
                && v.node.content.Equals(m_SelectedNode.content); // Make sure to use Equals() and not == to call any overriden comparison operator in the content type.

            DrawNode(gvNode, nodeRect, node, currentSelection);
        }

        var leftToRight = graphLayout.leftToRight; // AE: this seems to be flipped in concept
        foreach (var e in graphLayout.edges)
        {
            var sourceLayoutNode = leftToRight ? e.destination : e.source;
            var destLayoutNode = leftToRight ? e.source : e.destination;

            var sourceNode = nodeLookup[sourceLayoutNode];
            var destNode = nodeLookup[destLayoutNode];

            var edgeWeight = e.source.node.weight;
            var sourcePort = (Port)sourceNode.outputContainer.FirstOrDefault();
            if (sourcePort == null)
            {
                sourcePort = sourceNode.InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(Node));
                var color = sourcePort.portColor;
                color.a = edgeWeight;
                sourcePort.portColor = color;
                sourcePort.portName = "Out";
                sourceNode.outputContainer.Add(sourcePort);
            }
            var destPort = (Port)destNode.inputContainer.FirstOrDefault();
            if (destPort == null)
            {
                destPort = destNode.InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(Node));
                var color = destPort.portColor;
                color.a = edgeWeight;
                destPort.portColor = color;
                destPort.portName = "In";
                destNode.inputContainer.Add(destPort);
            }

            var edge = new GraphViewEdge();
            edge.input = leftToRight ? destPort : sourcePort;
            edge.output = leftToRight ? sourcePort : destPort;
            sourcePort.Connect(edge);
            destPort.Connect(edge);
            graphView.AddElement(edge);
        }
    }

    // Apply node constraints to node size
    static Vector2 ComputeNodeSize(Vector2 scale, GraphSettings graphSettings)
    {
        var extraThickness = (k_SelectedNodeThickness + k_ActiveNodeThickness) * 2.0f;
        var nodeSize = new Vector2(graphSettings.maximumNormalizedNodeSize * scale.x - extraThickness,
            graphSettings.maximumNormalizedNodeSize * scale.y - extraThickness);

        // Adjust aspect ratio after scaling
        float currentAspectRatio = nodeSize.x / nodeSize.y;

        if (currentAspectRatio > graphSettings.aspectRatio)
        {
            // Shrink x dimension
            nodeSize.x = nodeSize.y * graphSettings.aspectRatio;
        }
        else
        {
            // Shrink y dimension
            nodeSize.y = nodeSize.x / graphSettings.aspectRatio;
        }

        // If node size is still too big, scale down
        if (nodeSize.x > graphSettings.maximumNodeSizeInPixels)
        {
            nodeSize *= graphSettings.maximumNodeSizeInPixels / nodeSize.x;
        }

        if (nodeSize.y > graphSettings.maximumNodeSizeInPixels)
        {
            nodeSize *= graphSettings.maximumNodeSizeInPixels / nodeSize.y;
        }
        return nodeSize;
    }

    static int ComputeFontSize(Vector2 nodeSize, string text)
    {
        if (string.IsNullOrEmpty(text))
            return s_NodeMaxFontSize;

        string[] words = text.Split('\n');
        int nbLignes = words.Length;
        int longuestWord = words.Max(s => s.Length);

        // Approximate the text rectangle size using magic values.
        int width = longuestWord * (int) (0.8f * s_NodeMaxFontSize);
        int height = nbLignes * (int) (1.5f * s_NodeMaxFontSize);

        float factor = Math.Min(nodeSize.x / width, nodeSize.y / height);

        factor = Mathf.Clamp01(factor);

        return Mathf.CeilToInt(s_NodeMaxFontSize * factor);
    }

    // Convert vertex position from normalized layout to render rect
    static Vector2 ScaleVertex(Vector2 v, Vector2 offset, Vector2 scaleFactor)
    {
        return new Vector2((v.x + offset.x) * scaleFactor.x, (v.y + offset.y) * scaleFactor.y);
    }

    void DrawNode(GraphViewNode gvNode, Rect nodeRect, Node node, bool selected)
    {
        var style = gvNode.mainContainer.style;
        string nodeType = node.GetContentTypeName();
        string formattedLabel = nodeType;
        NodeTypeLegend nodeTypeLegend;
        if (m_LegendForType.TryGetValue(nodeType, out nodeTypeLegend))
        {
            formattedLabel = Regex.Replace(nodeTypeLegend.label, "((?<![A-Z])\\B[A-Z])", "\n$1"); // Split into multi-lines
            style.backgroundColor = nodeTypeLegend.color;
        }

        gvNode.title = formattedLabel;
        gvNode.SetPosition(nodeRect);

        if (selected)
        {
            style.borderColor = k_SelectedNodeColor;
        }
        else if (node.active)
        {
            style.borderColor = k_ActiveNodeColor;
        }
    }
}
#endif
