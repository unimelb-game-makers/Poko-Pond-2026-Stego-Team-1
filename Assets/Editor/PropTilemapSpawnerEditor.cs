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
                var connectionModeProp = element.FindPropertyRelative("connectionMode");
                var initialActiveProp = element.FindPropertyRelative("initialActive");
                var requirePlayerStateProp = element.FindPropertyRelative("requirePlayerState");
                var requiredPlayerStateProp = element.FindPropertyRelative("requiredPlayerState");
                var overrideBlowerProp = element.FindPropertyRelative("overrideBlowerSettings");
                var blowerDirectionProp = element.FindPropertyRelative("blowerDirection");
                var blowerStrengthProp = element.FindPropertyRelative("blowerStrength");
                var blowerRangeProp = element.FindPropertyRelative("blowerRange");
                var blowerWidthProp = element.FindPropertyRelative("blowerWidth");

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
                EditorGUILayout.PropertyField(connectionModeProp, new GUIContent("Connection Mode"));
                EditorGUILayout.PropertyField(initialActiveProp, new GUIContent("Initial Active"));

                if (propName == nameof(PressurePlate))
                {
                    EditorGUILayout.PropertyField(
                        requirePlayerStateProp,
                        new GUIContent("Require Player State"));
                    if (requirePlayerStateProp.boolValue)
                    {
                        EditorGUILayout.PropertyField(
                            requiredPlayerStateProp,
                            new GUIContent("Required State"));
                    }
                }

                if (propName == "Blower")
                {
                    EditorGUILayout.PropertyField(overrideBlowerProp, new GUIContent("Override Blower Settings"));
                    if (overrideBlowerProp.boolValue)
                    {
                        EditorGUILayout.PropertyField(blowerDirectionProp, new GUIContent("Blow Direction"));
                        EditorGUILayout.PropertyField(blowerStrengthProp, new GUIContent("Blow Strength"));
                        EditorGUILayout.PropertyField(blowerRangeProp, new GUIContent("Range"));
                        EditorGUILayout.PropertyField(blowerWidthProp, new GUIContent("Width"));
                    }
                }
                EditorGUI.indentLevel--;

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
