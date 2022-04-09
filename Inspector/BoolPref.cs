// UltEvents // Copyright 2021 Kybernetik //

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UltEvents.Editor
{
    /// <summary>[Editor-Only]
    /// A simple wrapper around <see cref="EditorPrefs"/> to get and set a bool.
    /// <para></para>
    /// If you are interested in a more comprehensive pref wrapper that supports more types, you should check out
    /// <see href="https://kybernetik.com.au/inspector-gadgets">Inspector Gadgets</see>.
    /// </summary>
    public sealed class BoolPref
    {
        /************************************************************************************************************************/

        /// <summary>The text that will be displayed for this item in a context menu.</summary>
        public readonly string Label;

        /// <summary>The identifier with which this pref will be saved.</summary>
        public readonly string Key;

        /// <summary>The starting value to use for this pref if none was previously saved.</summary>
        public readonly bool DefaultValue;

        /// <summary>Called when the value is changed.</summary>
        public readonly Action OnChanged;

        /************************************************************************************************************************/

        private bool _HasValue;
        private bool _Value;

        /// <summary>The current value of this pref.</summary>
        public bool Value
        {
            get
            {
                if (!_HasValue)
                {
                    _HasValue = true;
                    _Value = EditorPrefs.GetBool(Key, DefaultValue);
                }

                return _Value;
            }
            set
            {
                _Value = value;
                EditorPrefs.SetBool(Key, value);
                _HasValue = true;
            }
        }

        /// <summary>Returns the current value of the `pref`.</summary>
        public static implicit operator bool(BoolPref pref)
        {
            return pref.Value;
        }

        /************************************************************************************************************************/

        /// <summary>Constructs a new <see cref="BoolPref"/>.</summary>
        public BoolPref(string label, bool defaultValue, Action onChanged = null)
        {
            Label = label;
            Key = Names.Namespace + "." + label;
            DefaultValue = defaultValue;
            OnChanged = onChanged;
            _Value = EditorPrefs.GetBool(Key, defaultValue);
        }

        /************************************************************************************************************************/

        /// <summary>Adds a menu item to toggle this pref.</summary>
        public void AddToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Display Options ->/" + Label), _Value, () =>
            {
                _Value = !_Value;
                EditorPrefs.SetBool(Key, _Value);
                if (OnChanged != null)
                    OnChanged();
            });
        }

        /************************************************************************************************************************/

        /// <summary>Various settings.</summary>
        public static readonly BoolPref
            //Inspector
            UseIndentation = new BoolPref("Use Indentation", true),
            AutoOpenMenu = new BoolPref("Auto Open Menu", true),
            AutoHideFooter = new BoolPref("Auto Hide Footer", true),

            //MethodSelector
            ShowHeader = new BoolPref("Show Header", true),
            ShowAllComponents = new BoolPref("Show All Components", true),
            GroupMethods = new BoolPref("Group Methods", true),
            GroupProperties = new BoolPref("Group Properties", true),
            ShowNonPublics = new BoolPref("Show Non-Public Methods", true),
            GroupNonPublics = new BoolPref("Group Non-Public Methods", true),
            ShowStaticMethods = new BoolPref("Show Static Methods", true),
            ShowFullTypeNames = new BoolPref("Use Full Type Names", false),
            GroupMethodOverloads = new BoolPref("Sub-Menu for Method Overloads", true),
            SubMenuForEachBaseType = new BoolPref("Base Types ->/Individual Sub-Menus", true, MethodSelectionMenu.ClearMemberCache)
            ;

        /************************************************************************************************************************/

        //UltEvent Options Moved to Unity Preferences
        //BoolPref.AddDisplayOptions() shouldn't be used anymore
        public static void AddDisplayOptions( GenericMenu menu ) {
            Debug.LogError("BoolPref.AddDisplayOptions() shouldn't be used anymore");
            menu.AddItem(new GUIContent( "UltEvent Options Moved to Unity Preferences"), false, null);
        }

        /// <summary>Adds menu items to toggle all prefs.</summary>
        public static void DrawGui()
        {
            GUILayoutOption[] layoutOptions = { GUILayout.ExpandWidth(true), GUILayout.Height(EditorGUIUtility.singleLineHeight) };

            //Stupid way to make it scale like normal inspector
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            GUILayout.Label("INSPECTOR", EditorStyles.boldLabel, layoutOptions);
            GUILayout.Label("Use Indentation", layoutOptions);
            GUILayout.Label("Auto Open Menu", layoutOptions);
            GUILayout.Label("Auto Hide Footer", layoutOptions);

            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            GUILayout.Label("METHOD SELECTOR", EditorStyles.boldLabel, layoutOptions );

            GUILayout.Label("Show Headers", layoutOptions);
            GUILayout.Label("Show All Components", layoutOptions);
            GUILayout.Label("Group Methods", layoutOptions);
            GUILayout.Label("Group Properties", layoutOptions);
            GUILayout.Label("Show Non Public Methods", layoutOptions);
            GUILayout.Label("Group Non Public Methods", layoutOptions);
            GUILayout.Label("Show Static Methods", layoutOptions);
            GUILayout.Label("Show Full Type Names", layoutOptions);
            GUILayout.Label("Group Method Overloads", layoutOptions);
            GUILayout.Label("Sub Menu For BaseTypes", layoutOptions);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();
            GUILayout.Label("", layoutOptions);
            UseIndentation.Value = EditorGUILayout.Toggle(UseIndentation.Value);
            AutoOpenMenu.Value = EditorGUILayout.Toggle(AutoOpenMenu.Value);
            AutoHideFooter.Value = EditorGUILayout.Toggle(AutoHideFooter.Value);

            GUILayout.Space(2 * EditorGUIUtility.singleLineHeight);
            ShowHeader.Value = EditorGUILayout.Toggle(ShowHeader.Value);
            ShowAllComponents.Value = EditorGUILayout.Toggle(ShowAllComponents.Value);
            GroupMethods.Value = EditorGUILayout.Toggle(GroupMethods.Value);
            GroupProperties.Value = EditorGUILayout.Toggle(GroupProperties.Value);
            ShowNonPublics.Value = EditorGUILayout.Toggle(ShowNonPublics.Value);
            GroupNonPublics.Value = EditorGUILayout.Toggle(GroupNonPublics.Value);
            ShowStaticMethods.Value = EditorGUILayout.Toggle(ShowStaticMethods.Value);
            ShowFullTypeNames.Value = EditorGUILayout.Toggle(ShowFullTypeNames.Value);
            GroupMethodOverloads.Value = EditorGUILayout.Toggle(GroupMethodOverloads.Value);
            SubMenuForEachBaseType.Value = EditorGUILayout.Toggle(SubMenuForEachBaseType.Value);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();


            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            GUILayout.Label("Presets", layoutOptions);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Designer Preset", "Button1")) ) {
                ShowAllComponents.Value = false;
                ShowNonPublics.Value = false;
                ShowStaticMethods.Value = false;
            }
            if (GUILayout.Button(new GUIContent("Developer Preset", "Button1")) ) {
                ShowAllComponents.Value = true;
                ShowNonPublics.Value = true;
                ShowStaticMethods.Value = true;
            }
            GUILayout.EndHorizontal();
        }

        public static HashSet<string> GetSearchKeywords() {
            return new HashSet<string>(new[]
            {   "Use Indedentation",
                "GroupNonPublicMethods",
                "Auto Open Menu" ,
                "Auto Hide Footer",

                "Show Headers",
                "Show All Components",
                "Group Methods",
                "Group Properties",
                "Show Non Public Methods",
                "Group Non Public Methods" ,
                "Show Static Methods",
                "Show Full Type Names",
                "Group Method Overloads",
                "Sub Menu For BaseTypes",
            });
        }

        /************************************************************************************************************************/
    }
}

#endif
