using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PropTilemapSpawner))]
public class PropTilemapSpawnerEditor : Editor
{
    private SerializedProperty _overrides;

    private void OnEnable() => _overrides = serializedObject.FindProperty("cellOverrides");

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Prop Tilemap Spawner", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Paint tiles, then right-click this component → Sync Cell List. Set matching Connection IDs on props you want linked.", MessageType.Info);
        EditorGUILayout.Space(4);

        if (_overrides.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No cells synced yet. Right-click → Sync Cell List after painting tiles.", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.LabelField($"Cell Overrides ({_overrides.arraySize})", EditorStyles.boldLabel);

            for (int i = 0; i < _overrides.arraySize; i++)
            {
                var element    = _overrides.GetArrayElementAtIndex(i);
                var propName   = element.FindPropertyRelative("propName").stringValue;
                var cell       = element.FindPropertyRelative("cell").vector3IntValue;
                var connIdProp = element.FindPropertyRelative("connectionId");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Header row: prop name + cell coords
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    $"{propName}  ({cell.x}, {cell.y})",
                    EditorStyles.miniBoldLabel);
                EditorGUILayout.EndHorizontal();

                // Connection ID field
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(connIdProp, new GUIContent("Connection ID"));
                EditorGUI.indentLevel--;

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
