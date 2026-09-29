using System.Collections.Generic;
using System.Text;
using GAToolkit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GAToolkit.EditorTools
{
    /// <summary>
    /// Draws a ScreenInputZone so the list reads at a glance, and warns about the ways a zone
    /// can silently never fire.
    ///
    /// Three things the default drawer gets wrong for this type. Every entry is labelled
    /// "Element N", so you cannot tell zones apart without expanding them. Both event slots
    /// show, though only the one matching Detect Outside can ever fire. And nothing surfaces
    /// the setup mistakes that stop a zone working at all.
    ///
    /// That last one matters most with Detect Outside on: a zone that can never be hit reads
    /// every interaction as outside it, so a popup dismisses itself the moment you touch it.
    /// The warnings render inside the zone they describe rather than in a pile at the top of
    /// the component, so with several zones you can see which one is wrong.
    /// </summary>
    [CustomPropertyDrawer(typeof(ScreenInputZone))]
    public class ScreenInputZoneDrawer : PropertyDrawer
    {
        private const int NamesInHeader = 2;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty objects = property.FindPropertyRelative("objects");
            SerializedProperty detectOutside = property.FindPropertyRelative("detectOutside");
            SerializedProperty respondTo = property.FindPropertyRelative("respondTo");
            SerializedProperty activeEvent = property.FindPropertyRelative(
                detectOutside.boolValue ? "onInputOutside" : "onInputInside");

            label = new GUIContent(BuildHeader(objects));

            EditorGUI.BeginProperty(position, label, property);

            Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float y = line.yMax + EditorGUIUtility.standardVerticalSpacing;

                foreach (string warning in CollectWarnings(objects))
                {
                    GUIContent content = new GUIContent(warning);
                    float height = EditorStyles.helpBox.CalcHeight(content, HelpBoxWidth(position.width));
                    EditorGUI.HelpBox(new Rect(position.x, y, position.width, height), warning, MessageType.Warning);
                    y += height + EditorGUIUtility.standardVerticalSpacing;
                }

                DrawField(position, ref y, objects);
                DrawField(position, ref y, detectOutside);
                DrawField(position, ref y, respondTo);
                DrawField(position, ref y, activeEvent);

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;

            if (!property.isExpanded)
            {
                return height;
            }

            SerializedProperty objects = property.FindPropertyRelative("objects");
            SerializedProperty detectOutside = property.FindPropertyRelative("detectOutside");

            foreach (string warning in CollectWarnings(objects))
            {
                height += EditorStyles.helpBox.CalcHeight(new GUIContent(warning), HelpBoxWidth(ViewWidth()))
                          + EditorGUIUtility.standardVerticalSpacing;
            }

            height += FieldHeight(objects);
            height += FieldHeight(detectOutside);
            height += FieldHeight(property.FindPropertyRelative("respondTo"));
            height += FieldHeight(property.FindPropertyRelative(
                detectOutside.boolValue ? "onInputOutside" : "onInputInside"));

            return height;
        }

        #region Validation

        /// <summary>
        /// Every way a zone silently never fires comes down to the interaction not reaching it
        /// through uGUI's raycast.
        /// </summary>
        private static IEnumerable<string> CollectWarnings(SerializedProperty objects)
        {
            if (objects == null || objects.arraySize == 0)
            {
                yield return "This zone has no objects, so it can never be hit and will never fire. " +
                             "For \"every interaction\", use On Input Anywhere instead.";
                yield break;
            }

            for (int i = 0; i < objects.arraySize; i++)
            {
                GameObject obj = objects.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;

                if (obj == null)
                {
                    yield return $"Element {i} is empty.";
                    continue;
                }

                if (!HasRaycastableGraphic(obj))
                {
                    yield return $"'{obj.name}' has no Graphic with Raycast Target enabled, on itself or any " +
                                 "child, so it can never be hit.";
                }

                if (obj.GetComponentInParent<GraphicRaycaster>(true) == null)
                {
                    yield return $"'{obj.name}' is not under a Canvas with a GraphicRaycaster, so it can " +
                                 "never be hit.";
                }
            }
        }

        /// <summary>
        /// True if the object, or any child, can be returned by a uGUI raycast. Children count
        /// because zone matching walks up from the hit object to its listed parent.
        /// </summary>
        private static bool HasRaycastableGraphic(GameObject obj)
        {
            foreach (Graphic graphic in obj.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic.raycastTarget)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Layout

        private static void DrawField(Rect position, ref float y, SerializedProperty property)
        {
            float height = EditorGUI.GetPropertyHeight(property, true);
            EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), property, true);
            y += height + EditorGUIUtility.standardVerticalSpacing;
        }

        private static float FieldHeight(SerializedProperty property)
        {
            return EditorGUI.GetPropertyHeight(property, true) + EditorGUIUtility.standardVerticalSpacing;
        }

        private static float ViewWidth()
        {
            // GetPropertyHeight runs before OnGUI hands us a Rect, so the inspector's own width
            // is the closest thing to the width the help box will actually wrap at.
            return EditorGUIUtility.currentViewWidth - 40f;
        }

        private static float HelpBoxWidth(float available)
        {
            return Mathf.Max(80f, available - EditorGUI.indentLevel * 15f);
        }

        /// <summary>
        /// Names the zone after its contents, since a zone has no name of its own.
        /// "Popup_PhotoCredits, CloseButton_X" or "Red, Pink +2".
        /// </summary>
        private static string BuildHeader(SerializedProperty objects)
        {
            if (objects == null || objects.arraySize == 0)
            {
                return "(no objects)";
            }

            StringBuilder header = new StringBuilder();
            int named = 0;

            for (int i = 0; i < objects.arraySize && named < NamesInHeader; i++)
            {
                Object obj = objects.GetArrayElementAtIndex(i).objectReferenceValue;
                if (obj == null)
                {
                    continue;
                }

                if (named > 0)
                {
                    header.Append(", ");
                }

                header.Append(obj.name);
                named++;
            }

            if (named == 0)
            {
                return "(empty slots)";
            }

            int remaining = objects.arraySize - named;
            if (remaining > 0)
            {
                header.Append(" +").Append(remaining);
            }

            return header.ToString();
        }

        #endregion
    }
}
