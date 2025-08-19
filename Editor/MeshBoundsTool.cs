#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class MeshBoundsTool : EditorWindow
{
    private SkinnedMeshRenderer targetSMR;
    private Vector3 worldBoundsCenter;
    private Vector3 worldBoundsSize;
    private Transform newRootBone;
    private Transform originalRootBone;
    private Bounds lastKnownBounds;

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
            StoreBoundsState();
            Repaint();
        }
    }

    private void OnGUI()
    {
        CheckForExternalBoundsChanges();

        GUILayout.Label("Mesh Bounds Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        targetSMR = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Skinned Mesh Renderer", targetSMR, typeof(SkinnedMeshRenderer), true);

        if (EditorGUI.EndChangeCheck() && targetSMR != null)
        {
            UpdateWorldBounds();
            FindOriginalRootBone();
            StoreBoundsState();
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

        EditorGUILayout.Space(30);

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
        EditorGUILayout.ObjectField("Original Root:", originalRootBone, typeof(Transform), true);
        GUI.enabled = true;

        newRootBone = (Transform)EditorGUILayout.ObjectField("New Root Bone", newRootBone, typeof(Transform), true);

        GUI.enabled = newRootBone != null;
        if (GUILayout.Button("Apply New Root (Preserve World Bounds)"))
        {
            ApplyNewRootPreserveBounds();
        }
        GUI.enabled = true;

        EditorGUILayout.Space();

        if (GUILayout.Button("Recalculate Bounds"))
        {
            RecalculateBounds();
        }
    }

    private void CheckForExternalBoundsChanges()
    {
        if (targetSMR == null) return;

        if (lastKnownBounds.center != targetSMR.localBounds.center ||
            lastKnownBounds.size != targetSMR.localBounds.size)
        {
            UpdateWorldBounds();
            StoreBoundsState();
            Repaint();
        }
    }

    private void StoreBoundsState()
    {
        if (targetSMR != null)
        {
            lastKnownBounds = targetSMR.localBounds;
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

        StoreBoundsState();
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
        StoreBoundsState();

        EditorUtility.SetDirty(targetSMR);
        newRootBone = null;
    }

    private void FindOriginalRootBone()
    {
        originalRootBone = null;

        if (targetSMR == null || targetSMR.sharedMesh == null) return;

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
                            if (smr.rootBone != null)
                            {
                                // Find the corresponding bone in our current hierarchy
                                originalRootBone = FindCorrespondingBone(smr.rootBone, targetSMR.transform);

                                if (originalRootBone == null)
                                {

                                    // Try a broader search - search from the scene root or the entire hierarchy
                                    Transform[] allTransforms = null;

                                    // First try searching from the root of the GameObject hierarchy
                                    Transform rootTransform = targetSMR.transform;
                                    while (rootTransform.parent != null)
                                    {
                                        rootTransform = rootTransform.parent;
                                    }
                                    allTransforms = rootTransform.GetComponentsInChildren<Transform>();

                                    foreach (Transform t in allTransforms)
                                    {
                                        if (t.name == smr.rootBone.name)
                                        {
                                            originalRootBone = t;
                                            break;
                                        }
                                    }

                                    // If still not found, try searching just the bones array of the current SMR
                                    if (originalRootBone == null && targetSMR.bones != null)
                                    {
                                        foreach (Transform bone in targetSMR.bones)
                                        {
                                            if (bone != null && bone.name == smr.rootBone.name)
                                            {
                                                originalRootBone = bone;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }

                            return;
                        }
                    }
                }
            }
        }
    }

    private void RecalculateBounds()
    {
        if (targetSMR == null || targetSMR.sharedMesh == null) return;

        Undo.RecordObject(targetSMR, "Recalculate Default SMR Bounds");

        Transform savedCurrentRootBone = targetSMR.rootBone;

        if (originalRootBone == null)
        {
            FindOriginalRootBone();
        }

        if (originalRootBone != null)
        {
            targetSMR.rootBone = originalRootBone;
            Debug.Log($"<color=green>[Mesh Bounds Tool]</color> Temporarily set root to original: {originalRootBone.name}");
        }

        Vector3 savedWorldCenter = Vector3.zero;
        Vector3 savedWorldSize = Vector3.zero;
        bool hasSavedBounds = false;

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

                            // Save the world transforms after applying original bounds with original root
                            Transform rootTransform = targetSMR.rootBone != null ? targetSMR.rootBone : targetSMR.transform;
                            savedWorldCenter = rootTransform.TransformPoint(targetSMR.localBounds.center);
                            savedWorldSize = Vector3.Scale(targetSMR.localBounds.size, rootTransform.lossyScale);
                            hasSavedBounds = true;

                            break;
                        }
                    }
                    if (hasSavedBounds) break;
                }
            }
        }

        if (originalRootBone != null && savedCurrentRootBone != originalRootBone)
        {
            targetSMR.rootBone = savedCurrentRootBone;

            if (hasSavedBounds)
            {
                Transform restoredRootTransform = targetSMR.rootBone != null ? targetSMR.rootBone : targetSMR.transform;
                Vector3 localCenter = restoredRootTransform.InverseTransformPoint(savedWorldCenter);
                Vector3 localSize = new Vector3(
                    Mathf.Abs(savedWorldSize.x / restoredRootTransform.lossyScale.x),
                    Mathf.Abs(savedWorldSize.y / restoredRootTransform.lossyScale.y),
                    Mathf.Abs(savedWorldSize.z / restoredRootTransform.lossyScale.z)
                );

                targetSMR.localBounds = new Bounds(localCenter, localSize);
            }
        }

        // If no asset bounds found, fallback to manual method
        if (hasSavedBounds)
        {
            UpdateWorldBounds();
            EditorUtility.SetDirty(targetSMR);
        }
        else
        {
            RecalculateDefaultBoundsManual();
        }
    }

    private Transform FindCorrespondingBone(Transform originalBone, Transform searchRoot)
    {
        Transform[] allTransforms = searchRoot.GetComponentsInChildren<Transform>();

        foreach (Transform t in allTransforms)
        {
            if (t.name == originalBone.name)
            {
                if (DoesHierarchyMatch(originalBone, t))
                {
                    return t;
                }
            }
        }

        return null;
    }

    private bool DoesHierarchyMatch(Transform original, Transform candidate)
    {
        Transform origParent = original.parent;
        Transform candParent = candidate.parent;

        int levelsToCheck = 2;
        for (int i = 0; i < levelsToCheck; i++)
        {
            if (origParent == null && candParent == null)
                return true; // Both reached root

            if (origParent == null || candParent == null)
                return false; // One reached root, other didn't

            if (origParent.name != candParent.name)
                return false; // Names don't match

            origParent = origParent.parent;
            candParent = candParent.parent;
        }

        return true;
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