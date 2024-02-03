using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(VoxelizeMesh))]
public class VoxelizeMeshEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Voxelize"))
        {
            foreach (VoxelizeMesh script in targets) // 'targets' is an array of all selected objects that this editor can edit.
            {
                script.Test();
            }

        }
    }
}