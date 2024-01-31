using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(InsideChecker))]
public class VoxelizeMeshEditor : Editor {
    public override void OnInspectorGUI() {
        DrawDefaultInspector();

        if (GUILayout.Button("Test")) {
            foreach (InsideChecker script in targets) // 'targets' is an array of all selected objects that this editor can edit.
            {
                script.Test();
            }


        }
    }
}