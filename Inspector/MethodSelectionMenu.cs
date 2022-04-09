// UltEvents // Copyright 2021 Kybernetik //

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using static UnityEditor.GenericMenu;
using Object = UnityEngine.Object;

namespace UltEvents.Editor
{
    /// <summary>[Editor-Only]
    /// Manages the construction of menus for selecting methods for <see cref="PersistentCall"/>s.
    /// </summary>
    internal static class MethodSelectionMenu
    {

        public class EventDropDownMenu : AdvancedDropdown
        {
            public CustomDropDownItem root = new CustomDropDownItem("Methods");
            public Vector2 minimumSize
            {
                get
                {
                    return base.minimumSize;
                }
                set
                {
                    base.minimumSize = value;
                }
            }
            public EventDropDownMenu(AdvancedDropdownState state) : base(state)
            {
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem i)
            {
                CustomDropDownItem item = (CustomDropDownItem)i;
                if (item == null)
                {
                    Debug.LogError("UltEvents Select Error");
                    return;
                }

                if (item.func != null)
                {
                    item.func.Invoke();
                }
                if (item.func2 != null)
                {
                    item.func2.Invoke(item.userData);
                }

            }
        }

        public class CustomDropDownItem : AdvancedDropdownItem
        {
            public CustomDropDownItem parent;

            public MenuFunction func;
            public MenuFunction2 func2;
            public object userData;
            public bool subMenuGenerated;//really messy for now

            public CustomDropDownItem(string name, MenuFunction func = null, bool enabled = true) : base(name)
            {
                this.enabled = enabled;
                this.func = func;
            }
            public CustomDropDownItem(string name, MenuFunction2 func2, object userData = null, bool enabled = true) : base(name)
            {
                this.enabled = enabled;
                this.func2 = func2;
                this.userData = userData;
            }

            public void AddChild(CustomDropDownItem child)
            {
                child.parent = this;
                base.AddChild(child);
            }

            /// <summary>
            /// Adds an Item and enables all its parents
            /// </summary>
            /// <param name="child"></param>
            public void AddItem(CustomDropDownItem child)
            {
                AddChild(child);
                CustomDropDownItem parent = this;
                while (parent != null)
                {
                    if (parent.enabled) break;
                    parent.enabled = true;
                    parent = parent.parent;
                }
            }

            public CustomDropDownItem GetOrCreateSubMenu(string name, bool enabled = true)
            {
                foreach (var child in children)
                {
                    if (child.name == name) return (CustomDropDownItem)child;
                }
                CustomDropDownItem subItem = new CustomDropDownItem(name, enabled: enabled);
                AddChild(subItem);
                return subItem;
            }

            public void AddSeperator()
            {
                var last = children.Last();
                if (last.name != "SEPARATOR") AddSeparator();
            }

            public bool HasSubItem(string name)
            {
                foreach (var child in children)
                {
                    if (child.name == name)
                        return true;
                }
                return false;
            }
        }




        /************************************************************************************************************************/
        #region Fields
        /************************************************************************************************************************/

        /// <summary>
        /// The drawer state from when the menu was opened which needs to be restored when a method is selected because
        /// menu items are executed after the frame finishes and the drawer state is cleared.
        /// </summary>
        private static readonly DrawerState
            CachedState = new DrawerState();


        // These fields should really be passed around as parameters, but they make all the method signatures annoyingly long.
        private static MethodBase _CurrentMethod;
        private static BindingFlags _Bindings;
        private static EventDropDownMenu _Menu;

        /************************************************************************************************************************/
        #endregion
        /************************************************************************************************************************/
        #region Entry Point
        /************************************************************************************************************************/

        /// <summary>Opens the menu near the specified `area`.</summary>
        public static void ShowMenu(Rect area)
        {
            CachedState.CopyFrom(DrawerState.Current);

            _CurrentMethod = CachedState.call.GetMethodSafe();
            _Bindings = GetBindingFlags();
            _Menu = new EventDropDownMenu(new AdvancedDropdownState());
            _Menu.minimumSize = new Vector2(300f, 300f);
            //BoolPref.AddDisplayOptions(_Menu);

            Object[] targetObjects;
            var targets = GetObjectReferences(CachedState.TargetProperty, out targetObjects);

            AddCoreItems(targets);

            // Populate the main contents of the menu.
            {
                if (targets == null)
                {
                    var serializedMethodName = CachedState.MethodNameProperty.stringValue;
                    Type declaringType;
                    string methodName;
                    PersistentCall.GetMethodDetails(serializedMethodName, null, out declaringType, out methodName);

                    // If we have no target, but do have a type, populate the menu with that type's statics.
                    if (declaringType != null)
                    {
                        PopulateMenuWithStatics(targetObjects, declaringType);

                        goto ShowMenu;
                    }
                    else// If we have no type either, pretend the inspected objects are the targets.
                    {
                        targets = targetObjects;
                    }
                }

                // Ensure that all targets share the same type.
                var firstTarget = ValidateTargetsAndGetFirst(targets);
                if (firstTarget == null)
                {
                    targets = targetObjects;
                    firstTarget = targets[0];
                }

                // Add menu items according to the type of the target.
                if (firstTarget is GameObject)
                    PopulateMenuForGameObject(false, targets);
                else if (firstTarget is Component)
                    PopulateMenuForComponent(targets);
                else
                    PopulateMenuForObject(targets);
            }

        ShowMenu:


            _Menu.Show(area);

            GC.Collect();
        }

        /************************************************************************************************************************/

        private static BindingFlags GetBindingFlags()
        {
            var bindings = BindingFlags.Public | BindingFlags.Instance;

            if (BoolPref.ShowNonPublics)
                bindings |= BindingFlags.NonPublic;

            if (BoolPref.ShowStaticMethods)
                bindings |= BindingFlags.Static;

            return bindings;
        }

        /************************************************************************************************************************/

        private static void AddCoreItems(Object[] targets)
        {
            _Menu.root.AddItem(new CustomDropDownItem("Null", () =>
            {
                DrawerState.Current.CopyFrom(CachedState);

                if (targets != null)
                {
                    PersistentCallDrawer.SetMethod(null);
                }
                else
                {
                    // For a static method, remove the method name but keep the declaring type.
                    var methodName = CachedState.MethodNameProperty.stringValue;
                    var lastDot = methodName.LastIndexOf('.');
                    if (lastDot < 0)
                        CachedState.MethodNameProperty.stringValue = null;
                    else
                        CachedState.MethodNameProperty.stringValue = methodName.Substring(0, lastDot + 1);

                    CachedState.PersistentArgumentsProperty.arraySize = 0;

                    CachedState.MethodNameProperty.serializedObject.ApplyModifiedProperties();
                }

                DrawerState.Current.Clear();
            }, _CurrentMethod == null));

            var isStatic = _CurrentMethod != null && _CurrentMethod.IsStatic;
            if (targets != null && !isStatic)
            {
                _Menu.root.AddItem(new CustomDropDownItem("Static Method", () =>
                {
                    DrawerState.Current.CopyFrom(CachedState);

                    PersistentCallDrawer.SetTarget(null);

                    DrawerState.Current.Clear();
                }, isStatic));
            }
        }

        /************************************************************************************************************************/

        private static Object[] GetObjectReferences(SerializedProperty property, out Object[] targetObjects)
        {
            targetObjects = property.serializedObject.targetObjects;

            if (property.hasMultipleDifferentValues)
            {
                var references = new Object[targetObjects.Length];
                for (int i = 0; i < references.Length; i++)
                {
                    using (var serializedObject = new SerializedObject(targetObjects[i]))
                    {
                        references[i] = serializedObject.FindProperty(property.propertyPath).objectReferenceValue;
                    }
                }
                return references;
            }
            else
            {
                var target = property.objectReferenceValue;
                if (target != null)
                    return new Object[] { target };
                else
                    return null;
            }

        }

        /************************************************************************************************************************/

        private static Object ValidateTargetsAndGetFirst(Object[] targets)
        {
            var firstTarget = targets[0];
            if (firstTarget == null)
                return null;

            var targetType = firstTarget.GetType();

            // Make sure all targets have the exact same type.
            // Unfortunately supporting inheritance would be more complicated.

            var i = 1;
            for (; i < targets.Length; i++)
            {
                var obj = targets[i];
                if (obj == null || obj.GetType() != targetType)
                {
                    return null;
                }
            }

            return firstTarget;
        }

        /************************************************************************************************************************/

        private static T[] GetRelatedObjects<T>(Object[] objects, Func<Object, T> getRelatedObject)
        {
            var relatedObjects = new T[objects.Length];

            for (int i = 0; i < relatedObjects.Length; i++)
            {
                relatedObjects[i] = getRelatedObject(objects[i]);
            }

            return relatedObjects;
        }

        /************************************************************************************************************************/
        #endregion
        /************************************************************************************************************************/
        #region Populate for Objects

        /************************************************************************************************************************/

        private static void PopulateMenuWithStatics(Object[] targets, Type type)
        {
            var firstTarget = targets[0];
            var component = firstTarget as Component;
            if (!ReferenceEquals(component, null))
            {
                var gameObjects = GetRelatedObjects(targets, (target) => (target as Component).gameObject);
                PopulateMenuForGameObject(true, gameObjects);
            }
            else
            {
                PopulateMenuForObject(targets);
            }

            var bindings = BindingFlags.Static | BindingFlags.Public;
            if (BoolPref.ShowNonPublics)
                bindings |= BindingFlags.NonPublic;

            PopulateMenuWithMembers(type, bindings, null);
        }

        /************************************************************************************************************************/

        private static void PopulateMenuForGameObject(bool putGameObjectInSubMenu, Object[] targets)
        {

            PopulateMenuForObject(targets);

            if (BoolPref.ShowAllComponents)
            {
                var gameObjects = GetRelatedObjects(targets, (target) => target as GameObject);
                PopulateMenuForComponents(gameObjects);
            }
        }

        /************************************************************************************************************************/

        private static void PopulateMenuForComponents(GameObject[] gameObjects)
        {
            var firstGameObject = gameObjects[0];
            var components = firstGameObject.GetComponents<Component>();

            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];

                var targets = new Object[gameObjects.Length];
                targets[0] = component;

                Type type;
                var typeIndex = GetComponentTypeIndex(component, components, out type);

                int minTypeCount;
                Component unused;
                GetComponent(firstGameObject, type, typeIndex, out minTypeCount, out unused);

                var j = 1;
                for (; j < gameObjects.Length; j++)
                {
                    int typeCount;
                    Component targetComponent;
                    GetComponent(gameObjects[j], type, typeIndex, out typeCount, out targetComponent);
                    if (typeCount <= typeIndex)
                        goto NextComponent;

                    targets[j] = targetComponent;

                    if (minTypeCount > typeCount)
                        minTypeCount = typeCount;
                }

                if (minTypeCount > 1)
                {
                    PopulateMenuForObject(targets, typeIndex);
                }
                else
                {
                    PopulateMenuForObject(targets);
                }
            }

        NextComponent:;
        }

        private static int GetComponentTypeIndex(Component component, Component[] components, out Type type)
        {
            type = component.GetType();

            var count = 0;

            for (int i = 0; i < components.Length; i++)
            {
                var c = components[i];
                if (c == component)
                    break;
                else if (c.GetType() == type)
                    count++;
            }

            return count;
        }

        private static void GetComponent(GameObject gameObject, Type type, int targetIndex, out int numberOfComponentsOfType, out Component targetComponent)
        {
            numberOfComponentsOfType = 0;
            targetComponent = null;

            var components = gameObject.GetComponents(type);
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component.GetType() == type)
                {
                    if (numberOfComponentsOfType == targetIndex)
                        targetComponent = component;

                    numberOfComponentsOfType++;
                }
            }
        }

        /************************************************************************************************************************/

        private static void PopulateMenuForComponent(Object[] targets)
        {
            var gameObjects = GetRelatedObjects(targets, (target) => (target as Component).gameObject);

            PopulateMenuForGameObject(true, gameObjects);

            PopulateMenuForObject(targets);
        }

        /************************************************************************************************************************/

        private static void PopulateMenuForObject(Object[] targets, int index = -1)
        {
            PopulateMenuWithMembers(targets[0].GetType(), _Bindings, targets, index);
        }

        /************************************************************************************************************************/
        #endregion
        /************************************************************************************************************************/
        #region Populate for Types
        /************************************************************************************************************************/

        private static void PopulateMenuWithMembers(Type type, BindingFlags bindings, Object[] targets, int typeIndex = -1)
        {
            var members = GetSortedMembers(type, bindings);
            var previousDeclaringType = type;

            var nameMatchesNextMethod = false;

            var i = 0;
            while (i < members.Count)
            {
                ParameterInfo[] parameters;
                MethodInfo getter;
                var member = GetNextSupportedMember(members, ref i, out parameters, out getter);

            GotMember:

                if (member == null)
                    return;


                i++;

                var property = member as PropertyInfo;
                if (property != null)
                {

                    string label = property.PropertyType.GetNameCS(BoolPref.ShowFullTypeNames);
                    label += ' ';
                    label += property.Name;

                    var defaultMethod = getter;

                    MethodInfo setter = null;
                    if (IsSupported(property.PropertyType))
                    {
                        setter = property.GetSetMethod(true);
                        if (setter != null)
                            defaultMethod = setter;
                    }

                    // Get and Set.
                    label += " { ";
                    if (getter != null) label += "get; ";
                    if (setter != null) label += "set; ";
                    label += '}';

                    //Header
                    if (BoolPref.ShowHeader) GetDropDownParent(type, property, typeIndex).GetOrCreateSubMenu("Properties", false);

                    AddSetCallItem(GetDropDownParent(type, property, typeIndex), defaultMethod, targets, label);

                    //Add seperator after last Property
                    ParameterInfo[] nextParameters;
                    MethodInfo nextGetter;
                    var nextMember = GetNextSupportedMember(members, ref i, out nextParameters, out nextGetter);
                    if (!BoolPref.GroupMethods && !BoolPref.GroupProperties &&
                        (nextMember.MemberType != MemberTypes.Property ||
                        (BoolPref.ShowNonPublics && BoolPref.GroupNonPublics && !IsPublic(nextMember))))
                    {
                        GetDropDownParent(type, property, typeIndex).AddSeperator();
                    }

                    continue;
                }

                var method = member as MethodBase;
                if (method != null)
                {

                    string methodSignature = GetMethodSignature(method, parameters, true);

                    // Check if the method name matched the previous or next method to group them.
                    if (BoolPref.GroupMethodOverloads)
                    {
                        var nameMatchedPreviousMethod = nameMatchesNextMethod;

                        ParameterInfo[] nextParameters;
                        MethodInfo nextGetter;
                        var nextMember = GetNextSupportedMember(members, ref i, out nextParameters, out nextGetter);

                        nameMatchesNextMethod = nextMember != null && method.Name == nextMember.Name;

                        if (nameMatchedPreviousMethod || nameMatchesNextMethod)
                        {
                            //Header
                            if (BoolPref.ShowHeader) GetDropDownParent(type, method, typeIndex).GetOrCreateSubMenu("Methods", false);

                            AddSetCallItem(GetDropDownParent(type, method, typeIndex, true), method, targets, methodSignature);

                            if (i < members.Count)
                            {
                                member = nextMember;
                                parameters = nextParameters;
                                getter = nextGetter;
                                goto GotMember;
                            }
                            else
                            {
                                return;
                            }
                        }
                    }

                    // Otherwise just build the label normally.

                    //Header
                    if (BoolPref.ShowHeader) GetDropDownParent(type, method, typeIndex).GetOrCreateSubMenu("Methods", false);

                    AddSetCallItem(GetDropDownParent(type, method, typeIndex), method, targets, methodSignature);
                }
            }
        }

        /************************************************************************************************************************/

        private static CustomDropDownItem GetDropDownParent(Type type, MethodBase method, int typeIndex, bool hasOverloads = false)
        {

            CustomDropDownItem root = _Menu.root;
            CustomDropDownItem parent = root;

            string name = type.GetNameCS();
            if (typeIndex >= 0)
            { //< 0 means only component (0 means 1st of multiple)
                name += $" {UltEventUtils.GetPlacementName(typeIndex)}";
            }
            parent = parent.GetOrCreateSubMenu(name);
            if (BoolPref.SubMenuForEachBaseType) parent = parent.GetOrCreateSubMenu(method.DeclaringType.Name);

            //Ensure submenu's are created beforehand

            CreateSubMenus(parent);




            if (BoolPref.ShowStaticMethods && method.IsStatic)
            {
                parent = parent.GetOrCreateSubMenu("Static");
            }
            if (BoolPref.GroupMethods)
            {
                parent = parent.GetOrCreateSubMenu("Methods");
            }
            if (BoolPref.GroupNonPublics && !IsPublic(method))
            {
                parent = parent.GetOrCreateSubMenu("Non-Public Methods");
            }
            if (BoolPref.GroupMethodOverloads && hasOverloads)
            {
                parent = parent.GetOrCreateSubMenu(method.Name);
            }

            return parent;
        }
        private static CustomDropDownItem GetDropDownParent(Type type, PropertyInfo property, int typeIndex)
        {

            CustomDropDownItem root = _Menu.root;
            CustomDropDownItem parent = root;

            string name = type.GetNameCS();
            if (typeIndex >= 0)
            { //< 0 means only component (0 means 1st of multiple)
                name += $" {UltEventUtils.GetPlacementName(typeIndex)}";
            }
            parent = parent.GetOrCreateSubMenu(name);
            if (BoolPref.SubMenuForEachBaseType) parent = parent.GetOrCreateSubMenu(property.DeclaringType.Name);

            //Ensure submenu's are created beforehand

            CreateSubMenus(parent);

            if (BoolPref.ShowStaticMethods && property.GetGetMethod(true).IsStatic)
            {
                parent = parent.GetOrCreateSubMenu("Static");
            }
            if (BoolPref.GroupProperties)
            {
                parent = parent.GetOrCreateSubMenu("Properties");
            }
            if (BoolPref.GroupNonPublics && !IsPublic(property))
            {
                parent = parent.GetOrCreateSubMenu("Non-Public Properties");
            }

            return parent;
        }

        private static void CreateSubMenus(CustomDropDownItem parent)
        {
            //All logic for creating all submenu's (nested) and its seperators

            if (parent.subMenuGenerated) return;

            if (BoolPref.ShowStaticMethods)
            {
                //Static
                CustomDropDownItem staticParent = parent.GetOrCreateSubMenu("Static", false);
                if (!(BoolPref.GroupMethods || BoolPref.GroupProperties))
                {
                    if (!BoolPref.GroupNonPublics)
                    {
                        parent.AddSeperator();
                    }
                }

                //Static/Methods
                if (BoolPref.GroupMethods)
                {
                    CustomDropDownItem staticMethodsParent = staticParent.GetOrCreateSubMenu("Methods", false);
                    if (BoolPref.GroupMethods && !BoolPref.GroupProperties) staticParent.AddSeperator();
                    //Static/Methods/Non-Public Methods
                    if (BoolPref.ShowNonPublics && BoolPref.GroupNonPublics)
                    {
                        staticMethodsParent.GetOrCreateSubMenu("Non-Public Methods", false);
                        if (!BoolPref.GroupProperties) staticMethodsParent.AddSeperator();
                    }
                }
                //Static/Properties
                if (BoolPref.GroupProperties)
                {
                    CustomDropDownItem staticPropertiesParent = staticParent.GetOrCreateSubMenu("Properties", false);
                    if (BoolPref.GroupProperties) staticParent.AddSeperator();
                    //Static/Properties/Non-Public Properties
                    if (BoolPref.ShowNonPublics && BoolPref.GroupNonPublics)
                    {
                        staticPropertiesParent.GetOrCreateSubMenu("Non-Public Properties", false);
                        staticPropertiesParent.AddSeperator();
                    }
                }

            }

            //Methods
            if (BoolPref.GroupMethods)
            {
                CustomDropDownItem methodsParent = parent.GetOrCreateSubMenu("Methods", false);
                if (BoolPref.GroupMethods && !BoolPref.GroupProperties && !BoolPref.GroupNonPublics) parent.AddSeperator();
                //Methods/Non-Public Methods
                if (BoolPref.ShowNonPublics && BoolPref.GroupNonPublics)
                {
                    methodsParent.GetOrCreateSubMenu("Non-Public Methods", false);
                    methodsParent.AddSeperator();
                }
            }
            //Non-Public Methods
            else if (BoolPref.ShowNonPublics)
            {

                if (BoolPref.GroupNonPublics)
                {
                    parent.GetOrCreateSubMenu("Non-Public Methods", false);
                }
            }

            //Properties
            if (BoolPref.GroupProperties)
            {
                CustomDropDownItem propertiesParent = parent.GetOrCreateSubMenu("Properties", false);
                if (BoolPref.GroupProperties) parent.AddSeperator();
                //Properties/Non-Public Properties
                if (BoolPref.ShowNonPublics && BoolPref.GroupNonPublics)
                {
                    propertiesParent.GetOrCreateSubMenu("Non-Public Properties", false);
                    propertiesParent.AddSeperator();
                }
            }
            //Non-Public Properties
            else if (BoolPref.ShowNonPublics)
            {
                if (BoolPref.GroupNonPublics)
                {
                    parent.GetOrCreateSubMenu("Non-Public Properties", false);
                    parent.AddSeperator();
                }
            }


            parent.subMenuGenerated = true;
        }


        /************************************************************************************************************************/

        private static void AddSetCallItem(CustomDropDownItem itemParent, MethodBase method, Object[] targets, string label)
        {
            itemParent.AddItem(
                new CustomDropDownItem(label,
                (userData) =>
                {
                    DrawerState.Current.CopyFrom(CachedState);

                    var i = 0;
                    CachedState.CallProperty.ModifyValues<PersistentCall>((call) =>
                    {
                        var target = targets != null ? targets[i % targets.Length] : null;
                        call.SetMethod(method, target);
                        i++;
                    }, "Set Persistent Call");

                    DrawerState.Current.Clear();
                },
                method == _CurrentMethod));
        }

        /************************************************************************************************************************/
        #endregion
        /************************************************************************************************************************/
        #region Member Gathering
        /************************************************************************************************************************/

        private static readonly Dictionary<BindingFlags, Dictionary<Type, List<MemberInfo>>>
            MemberCache = new Dictionary<BindingFlags, Dictionary<Type, List<MemberInfo>>>();

        internal static void ClearMemberCache()
        {
            MemberCache.Clear();
        }

        /************************************************************************************************************************/

        private static List<MemberInfo> GetSortedMembers(Type type, BindingFlags bindings)
        {
            // Get the cache for the specified bindings.
            Dictionary<Type, List<MemberInfo>> memberCache;
            if (!MemberCache.TryGetValue(bindings, out memberCache))
            {
                memberCache = new Dictionary<Type, List<MemberInfo>>();
                MemberCache.Add(bindings, memberCache);
            }

            // If the members for the specified type aren't cached for those bindings, gather and sort them.
            List<MemberInfo> members;
            if (!memberCache.TryGetValue(type, out members))
            {
                var properties = type.GetProperties(bindings);
                var methods = type.GetMethods(bindings);

                // When gathering static members, also include instance constructors.
                var constructors = ((bindings & BindingFlags.Static) == BindingFlags.Static) ?
                    type.GetConstructors((bindings & ~BindingFlags.Static) | BindingFlags.Instance) :
                    null;

                var capacity = properties.Length + methods.Length;
                if (constructors != null)
                    capacity += constructors.Length;

                members = new List<MemberInfo>(capacity);
                members.AddRange(properties);
                if (constructors != null)
                    members.AddRange(constructors);
                members.AddRange(methods);

                // If the bindings include static, add static members from each base type.
                if ((bindings & BindingFlags.Static) == BindingFlags.Static && type.BaseType != null)
                {
                    members.AddRange(GetSortedMembers(type.BaseType, bindings & ~BindingFlags.Instance));
                }

                UltEventUtils.StableInsertionSort(members, CompareMembers);

                memberCache.Add(type, members);
            }

            return members;
        }

        /************************************************************************************************************************/

        private static int CompareMembers(MemberInfo a, MemberInfo b)
        {
            if (BoolPref.SubMenuForEachBaseType)
            {
                var result = CompareChildBeforeBase(a.DeclaringType, b.DeclaringType);
                if (result != 0)
                    return result;
            }

            // Compare types (properties before methods).
            if (a is PropertyInfo)
            {
                if (!(b is PropertyInfo))
                    return -1;
            }
            else
            {
                if (b is PropertyInfo)
                    return 1;
            }

            // Non-Public Sub-Menu.
            if (BoolPref.GroupNonPublics)
            {
                if (IsPublic(a))
                {
                    if (!IsPublic(b))
                        return -1;
                }
                else
                {
                    if (IsPublic(b))
                        return 1;
                }
            }

            // Compare names.
            return a.Name.CompareTo(b.Name);
        }

        /************************************************************************************************************************/

        private static int CompareChildBeforeBase(Type a, Type b)
        {
            if (a == b)
                return 0;

            while (true)
            {
                a = a.BaseType;

                if (a == null)
                    return 1;

                if (a == b)
                    return -1;
            }
        }

        /************************************************************************************************************************/

        private static readonly Dictionary<MemberInfo, bool>
            MemberToIsPublic = new Dictionary<MemberInfo, bool>();

        private static bool IsPublic(MemberInfo member)
        {
            bool isPublic;
            if (!MemberToIsPublic.TryGetValue(member, out isPublic))
            {
                switch (member.MemberType)
                {
                    case MemberTypes.Constructor:
                    case MemberTypes.Method:
                        isPublic = (member as MethodBase).IsPublic;
                        break;

                    case MemberTypes.Property:
                        isPublic =
                            (member as PropertyInfo).GetGetMethod() != null ||
                            (member as PropertyInfo).GetSetMethod() != null;
                        break;

                    default:
                        throw new ArgumentException("Unhandled member type", "member");
                }

                MemberToIsPublic.Add(member, isPublic);
            }

            return isPublic;
        }

        /************************************************************************************************************************/
        #endregion
        /************************************************************************************************************************/
        #region Supported Checks
        /************************************************************************************************************************/

        private static bool IsSupported(MethodBase method, out ParameterInfo[] parameters)
        {
            if (method.IsGenericMethod ||
                (method.IsSpecialName && (!method.IsConstructor || method.IsStatic)) ||
                method.Name.Contains("<") ||
                method.IsDefined(typeof(ObsoleteAttribute), true))
            {
                parameters = null;
                return false;
            }

            // Most UnityEngine.Object types shouldn't be constructed directly.
            if (method.IsConstructor)
            {
                if (typeof(Component).IsAssignableFrom(method.DeclaringType) ||
                    typeof(ScriptableObject).IsAssignableFrom(method.DeclaringType))
                {
                    parameters = null;
                    return false;
                }
            }

            parameters = method.GetParameters();
            if (!IsSupported(parameters))
                return false;

            return true;
        }

        private static bool IsSupported(PropertyInfo property, out MethodInfo getter)
        {
            if (property.IsSpecialName ||
                property.IsDefined(typeof(ObsoleteAttribute), true))// Obsolete.
            {
                getter = null;
                return false;
            }

            getter = property.GetGetMethod(true);
            if (getter == null && !IsSupported(property.PropertyType))
                return false;

            return true;
        }

        /************************************************************************************************************************/

        /// <summary>
        /// Returns true if the specified `type` can be represented by a <see cref="PersistentArgument"/>.
        /// </summary>
        public static bool IsSupported(Type type)
        {
            if (PersistentCall.IsSupportedNative(type))
            {
                return true;
            }
            else
            {
                int linkIndex;
                PersistentArgumentType linkType;
                return DrawerState.Current.TryGetLinkable(type, out linkIndex, out linkType);
            }
        }

        /// <summary>
        /// Returns true if the type of each of the `parameters` can be represented by a <see cref="PersistentArgument"/>.
        /// </summary>
        public static bool IsSupported(ParameterInfo[] parameters)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                if (!IsSupported(parameters[i].ParameterType))
                    return false;
            }

            return true;
        }

        /************************************************************************************************************************/

        private static MemberInfo GetNextSupportedMember(List<MemberInfo> members, ref int startIndex, out ParameterInfo[] parameters, out MethodInfo getter)
        {
            while (startIndex < members.Count)
            {
                var member = members[startIndex];
                var property = member as PropertyInfo;
                if (property != null && IsSupported(property, out getter))
                {
                    parameters = null;
                    return member;
                }

                var method = member as MethodBase;
                if (method != null && IsSupported(method, out parameters))
                {
                    getter = null;
                    return member;
                }

                startIndex++;
            }

            parameters = null;
            getter = null;
            return null;
        }

        /************************************************************************************************************************/
        #endregion
        /************************************************************************************************************************/
        #region Method Signatures
        /************************************************************************************************************************/

        private static readonly Dictionary<MethodBase, string>
            MethodSignaturesWithParameters = new Dictionary<MethodBase, string>(),
            MethodSignaturesWithoutParameters = new Dictionary<MethodBase, string>();
        private static readonly StringBuilder
            MethodSignatureBuilder = new StringBuilder();

        /************************************************************************************************************************/

        public static string GetMethodSignature(MethodBase method, ParameterInfo[] parameters, bool includeParameterNames)
        {
            if (method == null)
                return null;

            var signatureCache = includeParameterNames ? MethodSignaturesWithParameters : MethodSignaturesWithoutParameters;

            string signature;
            if (!signatureCache.TryGetValue(method, out signature))
            {
                signature = BuildAndCacheSignature(method, parameters, includeParameterNames, signatureCache);
            }

            return signature;
        }

        public static string GetMethodSignature(MethodBase method, bool includeParameterNames)
        {
            if (method == null)
                return null;

            var signatureCache = includeParameterNames ? MethodSignaturesWithParameters : MethodSignaturesWithoutParameters;

            string signature;
            if (!signatureCache.TryGetValue(method, out signature))
            {
                signature = BuildAndCacheSignature(method, method.GetParameters(), includeParameterNames, signatureCache);
            }

            return signature;
        }

        /************************************************************************************************************************/

        private static string BuildAndCacheSignature(MethodBase method, ParameterInfo[] parameters, bool includeParameterNames,
            Dictionary<MethodBase, string> signatureCache)
        {
            MethodSignatureBuilder.Length = 0;

            var returnType = method.GetReturnType();
            MethodSignatureBuilder.Append(returnType.GetNameCS(false));
            MethodSignatureBuilder.Append(' ');

            MethodSignatureBuilder.Append(method.Name);

            MethodSignatureBuilder.Append(" (");
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i > 0)
                    MethodSignatureBuilder.Append(", ");

                var parameter = parameters[i];

                MethodSignatureBuilder.Append(parameter.ParameterType.GetNameCS(false));
                if (includeParameterNames)
                {
                    MethodSignatureBuilder.Append(' ');
                    MethodSignatureBuilder.Append(parameter.Name);
                }
            }
            MethodSignatureBuilder.Append(')');

            var signature = MethodSignatureBuilder.ToString();
            MethodSignatureBuilder.Length = 0;
            signatureCache.Add(method, signature);

            return signature;
        }

        /************************************************************************************************************************/
        #endregion
        /************************************************************************************************************************/
    }
}

#endif
