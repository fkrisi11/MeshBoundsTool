#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class MeshBoundsTool : EditorWindow
{
    private SkinnedMeshRenderer targetSMR;
    private Vector3 worldBoundsCenter;
    private Vector3 worldBoundsSize;
    private Transform newRootBone;

    private static Vector3 copiedCenter;
    private static Vector3 copiedSize;
    private static bool hasCopiedBounds = false;

    [MenuItem("TohruTheDragon/Mesh Bounds Tool")]
    public static void ShowWindow()
    {
        GetWindow<MeshBoundsTool>("Mesh Bounds Tool");
    }

    private void OnEnable()
    {
        Undo.undoRedoPerformed += OnUndoRedo;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
    }

    private void OnUndoRedo()
    {
        if (targetSMR != null)
        {
            UpdateWorldBounds();
            Repaint();
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Mesh Bounds Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        targetSMR = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Skinned Mesh Renderer", targetSMR, typeof(SkinnedMeshRenderer), true);

        if (EditorGUI.EndChangeCheck() && targetSMR != null)
        {
            UpdateWorldBounds();
        }

        if (targetSMR == null)
        {
            EditorGUILayout.HelpBox("Assign a Skinned Mesh Renderer", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(20);
        GUILayout.Label("World Space Bounds", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        worldBoundsCenter = EditorGUILayout.Vector3Field("Center (World)", worldBoundsCenter);
        worldBoundsSize = EditorGUILayout.Vector3Field("Size", worldBoundsSize);

        if (EditorGUI.EndChangeCheck())
        {
            ApplyWorldBoundsToSMR();
        }

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Copy Bounds"))
        {
            CopyBounds();
        }

        GUI.enabled = hasCopiedBounds;
        if (GUILayout.Button("Paste Bounds"))
        {
            PasteBounds();
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(30);

        GUILayout.Label("Root Bone Management", EditorStyles.boldLabel);

        GUI.enabled = false;
        EditorGUILayout.ObjectField("Current Root:", targetSMR.rootBone, typeof(Transform), true);
        GUI.enabled = true;

        newRootBone = (Transform)EditorGUILayout.ObjectField("New Root Bone", newRootBone, typeof(Transform), true);

        GUI.enabled = newRootBone != null;
        if (GUILayout.Button("Apply New Root (Preserve World Bounds)"))
        {
            ApplyNewRootPreserveBounds();
        }
        GUI.enabled = true;

        EditorGUILayout.Space(30);

        GUILayout.Label("Bounds Calculation", EditorStyles.boldLabel);
        if (GUILayout.Button("Recalculate Bounds"))
        {
            RecalculateBounds();
        }
    }

    private void UpdateWorldBounds()
    {
        if (targetSMR == null) return;

        Bounds localBounds = targetSMR.localBounds;
        Transform rootTransform = targetSMR.rootBone != null ? targetSMR.rootBone : targetSMR.transform;

        // Convert local bounds to world space
        Vector3 worldCenter = rootTransform.TransformPoint(localBounds.center);
        Vector3 worldSize = Vector3.Scale(localBounds.size, rootTransform.lossyScale);

        worldBoundsCenter = worldCenter;
        worldBoundsSize = worldSize;
    }

    private void ApplyWorldBoundsToSMR()
    {
        if (targetSMR == null) return;

        Undo.RecordObject(targetSMR, "Modify SMR Bounds");

        Transform rootTransform = targetSMR.rootBone != null ? targetSMR.rootBone : targetSMR.transform;

        // Convert world bounds back to local space
        Vector3 localCenter = rootTransform.InverseTransformPoint(worldBoundsCenter);
        Vector3 localSize = new Vector3(
            Mathf.Abs(worldBoundsSize.x / rootTransform.lossyScale.x),
            Mathf.Abs(worldBoundsSize.y / rootTransform.lossyScale.y),
            Mathf.Abs(worldBoundsSize.z / rootTransform.lossyScale.z)
        );

        targetSMR.localBounds = new Bounds(localCenter, localSize);
        EditorUtility.SetDirty(targetSMR);
    }

    private void CopyBounds()
    {
        if (targetSMR == null) return;

        copiedCenter = worldBoundsCenter;
        copiedSize = worldBoundsSize;
        hasCopiedBounds = true;
    }

    private void PasteBounds()
    {
        if (!hasCopiedBounds) return;

        worldBoundsCenter = copiedCenter;
        worldBoundsSize = copiedSize;
        ApplyWorldBoundsToSMR();
    }

    private void ApplyNewRootPreserveBounds()
    {
        if (targetSMR == null || newRootBone == null) return;

        Undo.RecordObject(targetSMR, "Apply New Root Bone");

        Vector3 currentWorldCenter = worldBoundsCenter;
        Vector3 currentWorldSize = worldBoundsSize;

        targetSMR.rootBone = newRootBone;

        // Recalculate local bounds to maintain the same world bounds
        Vector3 localCenter = newRootBone.InverseTransformPoint(currentWorldCenter);
        Vector3 localSize = new Vector3(
            Mathf.Abs(currentWorldSize.x / newRootBone.lossyScale.x),
            Mathf.Abs(currentWorldSize.y / newRootBone.lossyScale.y),
            Mathf.Abs(currentWorldSize.z / newRootBone.lossyScale.z)
        );

        targetSMR.localBounds = new Bounds(localCenter, localSize);

        UpdateWorldBounds();

        EditorUtility.SetDirty(targetSMR);
        newRootBone = null;
    }

    private void RecalculateBounds()
    {
        if (targetSMR == null || targetSMR.sharedMesh == null) return;

        Undo.RecordObject(targetSMR, "Recalculate Default SMR Bounds");

        // Try to get bounds from the original asset
        string assetPath = AssetDatabase.GetAssetPath(targetSMR.sharedMesh);

        if (!string.IsNullOrEmpty(assetPath))
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

            foreach (var asset in assets)
            {
                if (asset is GameObject prefab)
                {
                    SkinnedMeshRenderer[] smrs = prefab.GetComponentsInChildren<SkinnedMeshRenderer>();
                    foreach (var smr in smrs)
                    {
                        if (smr.sharedMesh == targetSMR.sharedMesh)
                        {
                            targetSMR.localBounds = smr.localBounds;
                            UpdateWorldBounds();
                            EditorUtility.SetDirty(targetSMR);

                            return;
                        }
                    }
                }
            }
        }

        RecalculateDefaultBoundsManual();
    }

    private void RecalculateDefaultBoundsManual()
    {
        Mesh mesh = targetSMR.sharedMesh;

        if (mesh != null)
        {
            targetSMR.localBounds = mesh.bounds;
            UpdateWorldBounds();
            EditorUtility.SetDirty(targetSMR);

            Debug.LogWarning($"<color=green>[Mesh Bounds Tool]</color> Used mesh.bounds as fallback, as the original Skinned Mesh Renderer couldn't be found in the source file.");
        }
        else
        {
            Debug.LogError("<color=green>[Mesh Bounds Tool]</color> No mesh found to calculate bounds from.");
        }
    }
}
#endif