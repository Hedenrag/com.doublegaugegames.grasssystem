using log4net.Util;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(GrassRenderer))]
public class GrassRendererEditor : Editor
{

    static Vector3 lastPos = new();
    static float distance = 1f;
    static float radius = 1.5f;
    static float density = 0.2f;

    static bool painting = false;
    static bool erease = false;

    static float grassDensity = 0.2f;
    static Vector2Int grassFieldSize = new Vector2Int(20, 20);

    private void OnDisable()
    {
        painting = false;
    }


    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SerializedProperty materialProp = serializedObject.FindProperty("material");
        SerializedProperty meshProp = serializedObject.FindProperty("mesh");

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.FloatField("Blades Count", ((GrassRenderer)target).GetGrassPositions.Count);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.PropertyField(materialProp);
        EditorGUILayout.PropertyField(meshProp);
        if (GUILayout.Button("RecalculateBounds"))
        {
            var t = (GrassRenderer)target;
            float[] bounds = new float[6]{ // right, left, up, down, forward, back
                t.GetGrassPositions[0].x,
                t.GetGrassPositions[0].x,
                t.GetGrassPositions[0].y,
                t.GetGrassPositions[0].y,
                t.GetGrassPositions[0].z,
                t.GetGrassPositions[0].z
            };
            foreach (var v in t.GetGrassPositions)
            {
                if (v.x > bounds[0]) bounds[0] = v.x;
                else if (v.x < bounds[1]) bounds[1] = v.x;
                if (v.y > bounds[2]) bounds[2] = v.y;
                else if (v.y < bounds[3]) bounds[3] = v.y;
                if(v.z > bounds[4]) bounds[4] = v.z;
                else if(v.z < bounds[5]) bounds[5] = v.z;
            }
            Vector3 size = new Vector3(bounds[0] - bounds[1], bounds[2] - bounds[3], bounds[4] - bounds[5]) + t.mesh.bounds.size;
            Vector3 center = new Vector3(bounds[0] + bounds[1], bounds[2] + bounds[3], bounds[4] + bounds[5])/2f + t.mesh.bounds.center;
            t.grassBounds = new Bounds(center,size);
        }
        EditorGUILayout.LabelField("Create grass field");
        EditorGUI.indentLevel++;
        grassDensity = EditorGUILayout.FloatField("Grass density", grassDensity);
        grassFieldSize = EditorGUILayout.Vector2IntField("Grass field size", grassFieldSize);
        if (GUILayout.Button("Generate"))
        {
            ((GrassRenderer)target).CreateGrassArray(grassFieldSize, grassDensity);
            ((GrassRenderer)target).RegenerateGrassField();
        }
        EditorGUI.indentLevel--;
        EditorGUILayout.LabelField("Painting");
        EditorGUI.indentLevel++;
        painting = GUILayout.Toggle(painting, "Brush", new GUIStyle("Button"));
        erease = GUILayout.Toggle(erease, "Ereaser", new GUIStyle("Button"));
        radius = EditorGUILayout.Slider("Brush radius", radius, 0.1f, 10f);
        density = EditorGUILayout.Slider("Brush density", density, 0.05f, 0.7f);
        distance = EditorGUILayout.Slider("Paint distance", distance, 0.1f, 10f);
        EditorGUI.indentLevel--;
        serializedObject.ApplyModifiedProperties();
    }

    private void OnSceneGUI()
    {
        GrassRenderer t = target as GrassRenderer;
        Handles.color = Color.yellow;
        Handles.DrawWireCube(t.transform.localToWorldMatrix.MultiplyPoint(t.grassBounds.center), t.grassBounds.size);

        if (!painting) return;
        Ray cursorRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

        int controlId = GUIUtility.GetControlID(FocusType.Passive);

        if (Event.current.type == EventType.Repaint)
        {
            Physics.Raycast(cursorRay, out var hit);
            Handles.color = Color.yellow;
            Handles.DrawWireDisc(hit.point, hit.normal, radius, 1f);
            Handles.DrawWireDisc(hit.point, hit.normal, density, 0.5f);
            if (erease) MarkToErease(hit.point, hit.normal);
        }
        var currentEvent = Event.current.type;
        if (currentEvent == EventType.MouseDown || currentEvent == EventType.MouseDrag || currentEvent == EventType.MouseUp || currentEvent == EventType.DragPerform || currentEvent == EventType.DragUpdated || currentEvent == EventType.DragExited)
        {
            if (Physics.Raycast(cursorRay, out var hit))
            {
                if ((Event.current.type == EventType.MouseDown && Event.current.button == 0) || (Event.current.type == EventType.DragPerform && Event.current.button == 0))
                {
                    GUIUtility.hotControl = controlId;
                    Draw(hit.point, hit.normal);

                    Event.current.Use();
                }
                else if ((Event.current.type == EventType.MouseDrag && Event.current.button == 0) || (Event.current.type == EventType.DragUpdated && Event.current.button == 0))
                {
                    GUIUtility.hotControl = controlId;
                    if (distance < Vector3.SqrMagnitude(hit.point - lastPos))
                    {
                        Draw(hit.point, hit.normal);
                    }
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
                {
                    GUIUtility.hotControl = controlId;
                    ((GrassRenderer)target).RegenerateGrassField();
                    Event.current.Use();
                }

            }
        }

        void Draw(Vector3 position, Vector3 normal)
        {
            GrassRenderer t = target as GrassRenderer;

            lastPos = position;
            var transform = t.transform;

            Vector3 right = Vector3.Cross(Camera.current.transform.forward, normal).normalized;
            Vector3 forward = Vector3.Cross(right, normal);
            if (!erease)
            {
                foreach (var pos in Sunflower((int)(radius / density)))
                {
                    if (Physics.Raycast(position + (right * pos.x * radius) + (forward * pos.y * radius) + normal, -normal, out RaycastHit hit, radius))
                    {
                        t.GetGrassPositions.Add(transform.InverseTransformPoint(hit.point));
                    }
                }
            }
            else
            {
                List<int> positions = new List<int>();
                float sqrRad = radius * radius;
                var grassPos = t.GetGrassPositions;
                for (int i = 0; i < t.GetGrassPositions.Count; i++)
                {
                    if (Vector3.SqrMagnitude(transform.TransformPoint(grassPos[i]) - position) < sqrRad)
                        positions.Add(i);
                }
                for (int i = positions.Count - 1; i >= 0; i--)
                {
                    t.GetGrassPositions.RemoveAt(positions[i]);
                }
            }
            ((GrassRenderer)target).RegenerateGrassField();
        }

        Vector2[] Sunflower(int n, float alpha = 0, bool geodesic = false)
        {
            List<Vector2> points = new List<Vector2>();

            for (int i = 0; i < n; i++)
            {
                points.Add(SunflowerPos(i));
            }
            return points.ToArray();

            Vector2 SunflowerPos(int num)
            {
                float angleStride = 360f * 1.61803398875f;
                float r = Mathf.Sqrt(num - 0.5f) / Mathf.Sqrt(n - 0.5f);
                float theta = num * angleStride;
                return new Vector2(r * Mathf.Cos(theta), r * Mathf.Sin(theta));
            }
        }

        void MarkToErease(Vector3 position, Vector3 normal)
        {
            GrassRenderer t = target as GrassRenderer;
            var transform = t.transform;

            List<int> positions = new List<int>();
            float sqrRad = radius * radius;
            var grassPos = t.GetGrassPositions;
            for (int i = 0; i < t.GetGrassPositions.Count; i++)
            {
                if (Vector3.SqrMagnitude(transform.TransformPoint(grassPos[i]) - position) < sqrRad)
                    positions.Add(i);
            }
            Handles.color = Color.red;
            for (int i = positions.Count - 1; i >= 0; i--)
            {
                Handles.DrawSolidDisc(transform.TransformPoint(t.GetGrassPositions[positions[i]]), normal, 0.05f);
            }
        }

    }
}