using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Marble
{
    public class TrackColliderGenerator : EditorWindow
    {
        private GameObject trackObject;
        private float segmentLength = 1f;
        private float trackWidth = 2f;
        private float wallHeight = 0.5f;
        private float wallThickness = 0.1f;
        private float floorThickness = 0.1f;
        private bool generateFloor = true;
        private bool generateWalls = true;
        private bool useLocalSpace = true;

        [MenuItem("Marble/Track Collider Generator")]
        public static void ShowWindow()
        {
            GetWindow<TrackColliderGenerator>("Track Collider Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Track Collider Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            trackObject = (GameObject)EditorGUILayout.ObjectField("Track Object", trackObject, typeof(GameObject), true);

            EditorGUILayout.Space();
            GUILayout.Label("Collider Settings", EditorStyles.boldLabel);

            segmentLength = EditorGUILayout.FloatField("Segment Length", segmentLength);
            trackWidth = EditorGUILayout.FloatField("Track Width", trackWidth);
            wallHeight = EditorGUILayout.FloatField("Wall Height", wallHeight);
            wallThickness = EditorGUILayout.FloatField("Wall Thickness", wallThickness);
            floorThickness = EditorGUILayout.FloatField("Floor Thickness", floorThickness);

            EditorGUILayout.Space();
            generateFloor = EditorGUILayout.Toggle("Generate Floor", generateFloor);
            generateWalls = EditorGUILayout.Toggle("Generate Walls", generateWalls);
            useLocalSpace = EditorGUILayout.Toggle("Use Local Space", useLocalSpace);

            EditorGUILayout.Space();

            if (trackObject == null)
            {
                EditorGUILayout.HelpBox("Assign a track GameObject with a MeshFilter.", MessageType.Info);
                return;
            }

            var meshFilter = trackObject.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                EditorGUILayout.HelpBox("Track object needs a MeshFilter with a mesh.", MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox($"Mesh: {meshFilter.sharedMesh.name}\nVertices: {meshFilter.sharedMesh.vertexCount}", MessageType.None);

            EditorGUILayout.Space();

            if (GUILayout.Button("Generate Colliders", GUILayout.Height(30)))
            {
                GenerateColliders(meshFilter);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Clear Existing Colliders", GUILayout.Height(25)))
            {
                ClearColliders();
            }
        }

        private void GenerateColliders(MeshFilter meshFilter)
        {
            Undo.RegisterCompleteObjectUndo(trackObject, "Generate Track Colliders");

            Mesh mesh = meshFilter.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;

            List<TrackSegment> segments = AnalyzeMeshPath(vertices, triangles, meshFilter.transform);

            if (segments.Count < 2)
            {
                Debug.LogError("Could not find enough track segments. Try adjusting segment length.");
                return;
            }

            Transform colliderContainer = trackObject.transform.Find("TrackColliders");
            if (colliderContainer == null)
            {
                GameObject container = new GameObject("TrackColliders");
                container.transform.SetParent(trackObject.transform);
                container.transform.localPosition = Vector3.zero;
                container.transform.localRotation = Quaternion.identity;
                container.transform.localScale = Vector3.one;
                colliderContainer = container.transform;
                Undo.RegisterCreatedObjectUndo(container, "Create Collider Container");
            }

            int colliderCount = 0;

            for (int i = 0; i < segments.Count - 1; i++)
            {
                TrackSegment current = segments[i];
                TrackSegment next = segments[i + 1];

                Vector3 center = (current.center + next.center) / 2f;
                Vector3 direction = (next.center - current.center).normalized;
                float length = Vector3.Distance(current.center, next.center);

                if (length < 0.01f) continue;

                Quaternion rotation = Quaternion.LookRotation(direction, current.up);

                if (generateFloor)
                {
                    CreateBoxCollider(colliderContainer, $"Floor_{i}",
                        center - current.up * (floorThickness / 2f),
                        rotation,
                        new Vector3(trackWidth, floorThickness, length));
                    colliderCount++;
                }

                if (generateWalls)
                {
                    Vector3 leftOffset = -Vector3.Cross(direction, current.up).normalized * (trackWidth / 2f + wallThickness / 2f);
                    CreateBoxCollider(colliderContainer, $"WallL_{i}",
                        center + leftOffset + current.up * (wallHeight / 2f),
                        rotation,
                        new Vector3(wallThickness, wallHeight, length));
                    colliderCount++;

                    Vector3 rightOffset = Vector3.Cross(direction, current.up).normalized * (trackWidth / 2f + wallThickness / 2f);
                    CreateBoxCollider(colliderContainer, $"WallR_{i}",
                        center + rightOffset + current.up * (wallHeight / 2f),
                        rotation,
                        new Vector3(wallThickness, wallHeight, length));
                    colliderCount++;
                }
            }

            Debug.Log($"Generated {colliderCount} colliders for track.");
        }

        private List<TrackSegment> AnalyzeMeshPath(Vector3[] vertices, int[] triangles, Transform meshTransform)
        {
            List<TrackSegment> segments = new List<TrackSegment>();

            Bounds bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (var v in vertices)
            {
                bounds.Encapsulate(v);
            }

            int longestAxis = GetLongestAxis(bounds.size);

            Dictionary<int, List<Vector3>> vertexGroups = new Dictionary<int, List<Vector3>>();

            float minVal = GetAxisValue(bounds.min, longestAxis);
            float maxVal = GetAxisValue(bounds.max, longestAxis);

            foreach (var vertex in vertices)
            {
                Vector3 worldPos = useLocalSpace ? vertex : meshTransform.TransformPoint(vertex);
                float axisVal = GetAxisValue(vertex, longestAxis);
                int groupIndex = Mathf.FloorToInt((axisVal - minVal) / segmentLength);

                if (!vertexGroups.ContainsKey(groupIndex))
                {
                    vertexGroups[groupIndex] = new List<Vector3>();
                }
                vertexGroups[groupIndex].Add(worldPos);
            }

            List<int> sortedKeys = new List<int>(vertexGroups.Keys);
            sortedKeys.Sort();

            foreach (int key in sortedKeys)
            {
                List<Vector3> groupVerts = vertexGroups[key];
                if (groupVerts.Count < 3) continue;

                Vector3 center = Vector3.zero;
                foreach (var v in groupVerts)
                {
                    center += v;
                }
                center /= groupVerts.Count;

                Vector3 lowest = groupVerts[0];
                foreach (var v in groupVerts)
                {
                    if (v.y < lowest.y) lowest = v;
                }

                center.y = lowest.y;

                Vector3 up = CalculateUpVector(groupVerts, center);

                segments.Add(new TrackSegment
                {
                    center = useLocalSpace ? center : meshTransform.InverseTransformPoint(center),
                    up = up
                });
            }

            return segments;
        }

        private Vector3 CalculateUpVector(List<Vector3> vertices, Vector3 center)
        {
            Vector3 upSum = Vector3.zero;
            int upCount = 0;

            foreach (var v in vertices)
            {
                if (v.y > center.y + 0.1f)
                {
                    upSum += (v - center).normalized;
                    upCount++;
                }
            }

            if (upCount > 0)
            {
                return upSum.normalized;
            }

            return Vector3.up;
        }

        private int GetLongestAxis(Vector3 size)
        {
            if (size.x >= size.y && size.x >= size.z) return 0;
            if (size.y >= size.x && size.y >= size.z) return 1;
            return 2;
        }

        private float GetAxisValue(Vector3 v, int axis)
        {
            switch (axis)
            {
                case 0: return v.x;
                case 1: return v.y;
                case 2: return v.z;
                default: return v.x;
            }
        }

        private void CreateBoxCollider(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 size)
        {
            GameObject colliderObj = new GameObject(name);
            colliderObj.transform.SetParent(parent);
            colliderObj.transform.localPosition = position;
            colliderObj.transform.localRotation = rotation;
            colliderObj.transform.localScale = Vector3.one;

            BoxCollider box = colliderObj.AddComponent<BoxCollider>();
            box.size = size;
            box.center = Vector3.zero;

            Undo.RegisterCreatedObjectUndo(colliderObj, "Create Track Collider");
        }

        private void ClearColliders()
        {
            if (trackObject == null) return;

            Transform colliderContainer = trackObject.transform.Find("TrackColliders");
            if (colliderContainer != null)
            {
                Undo.DestroyObjectImmediate(colliderContainer.gameObject);
                Debug.Log("Cleared track colliders.");
            }
            else
            {
                Debug.Log("No collider container found.");
            }
        }

        private struct TrackSegment
        {
            public Vector3 center;
            public Vector3 up;
        }
    }
}
