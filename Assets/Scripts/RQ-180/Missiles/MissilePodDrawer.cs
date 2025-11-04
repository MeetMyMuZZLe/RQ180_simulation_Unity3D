// // MissilePodDrawer.cs
// // Place this script in a folder named "Editor" inside your Assets folder.
// using UnityEngine;
// using UnityEditor;
// using HomingMissile; // Make sure to include the namespace where MissileType is defined

// [CustomPropertyDrawer(typeof(MissilePod))]
// public class MissilePodDrawer : PropertyDrawer
// {
//     // This method is called by Unity to draw the property in the Inspector
//     public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
//     {
//         EditorGUI.BeginProperty(position, label, property);

//         // Find all the properties within the MissilePod class
//         var podName = property.FindPropertyRelative("podName");
//         var missileType = property.FindPropertyRelative("missileType");
//         var validTargets = property.FindPropertyRelative("validTargetClasses");
//         var mavericks = property.FindPropertyRelative("mavericksInPod");
//         var brimstones = property.FindPropertyRelative("brimstonesInPod");
//         var jsows = property.FindPropertyRelative("jsowsInPod");

//         // Draw the foldout header for the pod
//         property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), property.isExpanded, label, true);
        
//         if (property.isExpanded)
//         {
//             // Indent the content to look nice
//             EditorGUI.indentLevel++;

//             // --- Calculate Rects for each field ---
//             var currentY = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
//             var fieldRect = new Rect(position.x, currentY, position.width, EditorGUIUtility.singleLineHeight);

//             // Draw Pod Name
//             EditorGUI.PropertyField(fieldRect, podName);
//             currentY += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

//             // Draw Missile Type Enum
//             fieldRect.y = currentY;
//             EditorGUI.PropertyField(fieldRect, missileType);
//             currentY += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            
//             // Draw Valid Target Classes List
//             fieldRect.y = currentY;
//             EditorGUI.PropertyField(fieldRect, validTargets, true);
//             currentY += EditorGUI.GetPropertyHeight(validTargets, true) + EditorGUIUtility.standardVerticalSpacing;

//             // --- Conditional Logic to show the correct missile list ---
//             MissileType selectedType = (MissileType)missileType.enumValueIndex;

//             SerializedProperty selectedList = null;
//             switch (selectedType)
//             {
//                 // ** MAPPING AS PER YOUR REQUEST **
//                 case MissileType.Fast:     // Fast = Brimstone
//                     selectedList = brimstones;
//                     break;
//                 case MissileType.Standard: // Standard = Maverick
//                     selectedList = mavericks;
//                     break;
//                 case MissileType.Heavy:    // Heavy = JSOW
//                     selectedList = jsows;
//                     break;
//             }

//             // Draw the selected missile list if it's not null
//             if (selectedList != null)
//             {
//                 fieldRect.y = currentY;
//                 EditorGUI.PropertyField(fieldRect, selectedList, true);
//             }
            
//             EditorGUI.indentLevel--;
//         }

//         EditorGUI.EndProperty();
//     }

//     // This method tells Unity how much vertical space our custom drawer needs
//     public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
//     {
//         float totalHeight = EditorGUIUtility.singleLineHeight; // for the main foldout label

//         if (property.isExpanded)
//         {
//             // Add height for podName, missileType, and the vertical spacing
//             totalHeight += (EditorGUIUtility.singleLineHeight * 2) + (EditorGUIUtility.standardVerticalSpacing * 3);

//             // Add height for the validTargetClasses list
//             totalHeight += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("validTargetClasses"), true);
            
//             // Add height for the conditionally-displayed missile list
//             var missileType = property.FindPropertyRelative("missileType");
//             MissileType selectedType = (MissileType)missileType.enumValueIndex;
            
//             SerializedProperty selectedList = null;
//             switch (selectedType)
//             {
//                 case MissileType.Fast:
//                     selectedList = property.FindPropertyRelative("brimstonesInPod");
//                     break;
//                 case MissileType.Standard:
//                     selectedList = property.FindPropertyRelative("mavericksInPod");
//                     break;
//                 case MissileType.Heavy:
//                     selectedList = property.FindPropertyRelative("jsowsInPod");
//                     break;
//             }

//             if (selectedList != null)
//             {
//                 totalHeight += EditorGUI.GetPropertyHeight(selectedList, true) + EditorGUIUtility.standardVerticalSpacing;
//             }
//         }
        
//         return totalHeight;
//     }
// }