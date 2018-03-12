using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions.Must;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VRCSDK2;

/**
 * 
 * If you're reading this I'm sorry for the spaghet code, I am but a self taught programmer through the university of stack overflow.
 * 
 * Planned features:  
 * - Colliders?
 *      - Automatically place colliders on leg/hip/head/index fingers with sliders to easily adjust sizes.
 *      - Add the colliders to desired bones (hair/ears/skirt/etc)
 *      
 * - Auto root common accessories with empty game objects if available
 */

public class DynamicBonesPreset
{
    public string Name { get; set; }
    public Transform Root { get; set; }
    public float UpdateRate { get; set; }
    public float Damping { get; set; }
    public float Elasticity { get; set; }
    public float Stiffness { get; set; }
    public float Inert { get; set; }
    public float Radius { get; set; }
    public Vector3 EndLength { get; set; }
    public Vector3 EndOffset { get; set; }
    public Vector3 Gravity { get; set; }
    public Vector3 Force { get; set; }
    public Transform[] Colliders { get; set; }
    public Transform[] Exclusions { get; set; }
}

public class DynamicBonesStudioWindow : EditorWindow
{
    // Interface values
    private int _selectedTabIndex;

    private bool _isAutoRefreshEnabled;
    private bool _isDataSaved;
    private bool _isOptionsShowing;
    private string _itemToAddToWhitelist = "Enter item name here";
    private IniFile _configFile = null;

    // Default values if ini file fails to load.
    private List<string> _commonAccessories =
        new List<string> { "skirt", "gimmick_l", "gimmick_r", "earl", "earr", "ear_l", "ear_r", "scarf", "tie", "tail", "breast_l", "breast_r", "bell" };

    private GameObject _avatar = null;
    private Animator _avatarAnim = null;

    private Vector2 scrollPos;
    private Vector3 vec3Zero = Vector3.zero;

    private List<DynamicBone> _allDynamicBones = new List<DynamicBone>();
    private List<DynamicBonesPreset> _dynamicBonePresets = new List<DynamicBonesPreset>();

    private bool _isEditorLastStatePlaying;

    private Transform _hipsBone = null;
    private Transform _hairBone = null;
    private Transform _neckBone = null;
    private List<Transform> _accessoriesBones = null;

    //private SerializedProperty exclusions = null;

    [MenuItem("Window/Dynamic Bones Studio")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(DynamicBonesStudioWindow));
    }

    public List<Transform> ExclusionTransforms = new List<Transform>();
    public List<Transform> BonesTransforms = new List<Transform>();
    public List<Transform> AccessoriesTransforms = new List<Transform>();

    public List<Transform> AllBones = new List<Transform>();
    private bool _isAskForBoneNameEnabled;
    private bool _isAboutShowing;
    private SavePresetWindow _presetSaveInstance;
    private bool _isPresetNameSet;
    private string _cfgFilePath;
    private int _presetChoiceIndex;

    void HandleOnPlayModeChanged()
    {
        if (_isAutoRefreshEnabled)
        {
            _allDynamicBones = _avatar.GetComponentsInChildren<DynamicBone>().ToList();
        }
    }

    void OnEnable()
    {
        _cfgFilePath = Application.dataPath + "/Kaori/DynamicBonesStudio/Editor/DynamicBonesPresets.cfg";
    }

    void OnInspectorUpdate()
    {
        Repaint();
    }

    void OnGUI()
    {
        EditorGUILayout.BeginVertical();

        _isOptionsShowing = EditorGUILayout.Foldout(_isOptionsShowing, "Options");
        if (_isOptionsShowing)
        {
            EditorGUILayout.LabelField("Add item to common accessory whitelist:", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            GUI.SetNextControlName("WhitelistTextField");
            _itemToAddToWhitelist = EditorGUILayout.TextField(_itemToAddToWhitelist);
            if (GUILayout.Button("Add item") || (Event.current.keyCode == KeyCode.Return && GUI.GetNameOfFocusedControl() == "WhitelistTextField"))
            {
                if (_configFile == null)
                {
                    InitConfigFile();
                }
                _configFile.SetValue("AccessoryWhitelist", _itemToAddToWhitelist.ToLowerInvariant(), _itemToAddToWhitelist.ToLowerInvariant());
                //GUI.FocusControl("WhitelistTextField");
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        _selectedTabIndex = GUILayout.Toolbar(_selectedTabIndex, new string[] {"Basic setup", "Studio"});
        EditorApplication.playmodeStateChanged += HandleOnPlayModeChanged;

        switch (_selectedTabIndex)
        {
            // Basic setup tab
            case 0:
            {
                _selectedTabIndex = 0;

                EditorGUILayout.LabelField("Avatar:", EditorStyles.boldLabel);
                _avatar = (GameObject) EditorGUILayout.ObjectField(_avatar, typeof(GameObject), true);

                // Check for VRC Avatar Descriptor
                if (_avatar != null && _avatar.GetComponent<VRC_AvatarDescriptor>() == null)
                {
                    var result = EditorUtility.DisplayDialog("Error",
                        "You need to select a game object with a VRC Avatar Descriptor and an Animator!", "Add now", "Cancel");
                    if (result)
                    {
                        _avatar.AddComponent<VRC_AvatarDescriptor>();
                    }
                    else
                    {
                        _avatar = null;
                        return;
                    }
                }

                // Check if avatar is non-null and has an Animator.
                if (_avatar != null && _avatar.GetComponent<Animator>() == null)
                {
                    EditorUtility.DisplayDialog("Error",
                        "There is no animator on this Avatar!", "Ok");
                    _avatar = null;
                    return;
                }

                // If avatar is not assigned exit
                if (_avatar == null)
                    return;

                _avatarAnim = _avatar.GetComponent<Animator>();
                AllBones = _avatarAnim.GetComponentsInChildren<Transform>().ToList();
                _hipsBone = _avatarAnim.GetBoneTransform(HumanBodyBones.Hips);
                _neckBone = _avatarAnim.GetBoneTransform(HumanBodyBones.Neck);

                EditorGUILayout.LabelField("Hair root Bone:", EditorStyles.boldLabel);
                // Try to automatically find hair root.
                if (_hairBone == null)
                {
                    _hairBone = TryFindHairRoot();
                }
                _hairBone = (Transform) EditorGUILayout.ObjectField(_hairBone, typeof(Transform), true);

                EditorGUILayout.LabelField("Accessories Bones:", EditorStyles.boldLabel);
                var accessoriesTarget = this;
                var accessoriesSo = new SerializedObject(accessoriesTarget);
                var accessoriesTransformsProperty = accessoriesSo.FindProperty("AccessoriesTransforms");
                EditorGUILayout.PropertyField(accessoriesTransformsProperty, true);
                accessoriesSo.ApplyModifiedProperties();

                if (GUILayout.Button("Try and find common accessories"))
                {
                    AccessoriesTransforms = TryFindCommonAccessories();
                    if (AccessoriesTransforms != null)
                    {
                        accessoriesTransformsProperty = accessoriesSo.FindProperty("AccessoriesTransforms");
                        EditorGUILayout.PropertyField(accessoriesTransformsProperty, true);
                        accessoriesSo.ApplyModifiedProperties();
                    }
                    Debug.Log("Auto Dynamic Bones - Finding common accessories");
                }
                if (GUILayout.Button("Apply Dynamic Bones"))
                {
                    if (_hairBone == null)
                    {
                        var result = EditorUtility.DisplayDialog("Warning", "No hair transform set, continue?", "Yes", "No");
                        if (!result)
                        {
                            return;
                        }
                    }
                    AddDynamicBones();
                }
                break;
            }

            // Studio tab
            case 1:
            {
                _selectedTabIndex = 1;

                // If avatar is not assigned exit
                if (_avatar == null)
                {
                    EditorGUILayout.LabelField("Select an avatar in the basic setup tab to begin",
                        EditorStyles.boldLabel);
                    return;
                }

                GUILayout.Label("Experimental features:", EditorStyles.boldLabel);

                GUILayout.BeginHorizontal();
                _isAutoRefreshEnabled =
                    EditorGUILayout.ToggleLeft("Auto-refresh bones on state change", _isAutoRefreshEnabled);

                if (GUILayout.Button("Refresh bones"))
                {
                    _allDynamicBones = _avatar.GetComponentsInChildren<DynamicBone>().ToList();
                    Debug.Log("Found " + _allDynamicBones.Count + " new dynamic bones.");
                }
                GUILayout.EndHorizontal();

                _isAskForBoneNameEnabled =
                    EditorGUILayout.ToggleLeft("Ask for name when saving dynamic bone presets", _isAskForBoneNameEnabled);

                if (GUILayout.Button("Refresh presets"))
                {
                    LoadDynamicBonePresets();
                }

                if (EditorApplication.isPlaying)
                {
                    EditorGUILayout.LabelField("Will not save colliders or exclusions, beware", EditorStyles.boldLabel);
                    if (GUILayout.Button("Save play-mode bone settings"))
                    {
                        foreach (var dynamicBone in _allDynamicBones)
                        {
                            EditorPrefs.SetFloat(dynamicBone.name + "Damping", dynamicBone.m_Damping);
                            EditorPrefs.SetFloat(dynamicBone.name + "Elasticity", dynamicBone.m_Elasticity);
                            EditorPrefs.SetFloat(dynamicBone.name + "Stiffness", dynamicBone.m_Stiffness);
                            EditorPrefs.SetFloat(dynamicBone.name + "Inert", dynamicBone.m_Inert);
                            EditorPrefs.SetFloat(dynamicBone.name + "Radius", dynamicBone.m_Radius);

                            // End offset vector
                            EditorPrefs.SetFloat(dynamicBone.name + "EndOffsetX", dynamicBone.m_EndOffset.x);
                            EditorPrefs.SetFloat(dynamicBone.name + "EndOffsetY", dynamicBone.m_EndOffset.y);
                            EditorPrefs.SetFloat(dynamicBone.name + "EndOffsetZ", dynamicBone.m_EndOffset.z);
                            // End offset vector

                            // Gravity vector
                            EditorPrefs.SetFloat(dynamicBone.name + "GravityX", dynamicBone.m_Gravity.x);
                            EditorPrefs.SetFloat(dynamicBone.name + "GravityY", dynamicBone.m_Gravity.y);
                            EditorPrefs.SetFloat(dynamicBone.name + "GravityZ", dynamicBone.m_Gravity.z);
                            // Gravity vector

                            // Force vector
                            EditorPrefs.SetFloat(dynamicBone.name + "ForceX", dynamicBone.m_Force.x);
                            EditorPrefs.SetFloat(dynamicBone.name + "ForceY", dynamicBone.m_Force.y);
                            EditorPrefs.SetFloat(dynamicBone.name + "ForceZ", dynamicBone.m_Force.z);
                            // Force vector


                            }
                            EditorPrefs.SetBool("DynamicBoneStudioDataSaved", true);
                        Debug.Log("Prefs saved");
                    }
                }

                if (EditorPrefs.HasKey("DynamicBoneStudioDataSaved"))
                {
                    if (GUILayout.Button("Load play-mode bone settings"))
                    {
                        foreach (var dynamicBone in _allDynamicBones)
                        {
                            UpdateDynamicBone(dynamicBone, true,
                                EditorPrefs.GetFloat(dynamicBone.name + "Damping"),
                                EditorPrefs.GetFloat(dynamicBone.name + "Elasticity"),
                                EditorPrefs.GetFloat(dynamicBone.name + "Stiffness"),
                                EditorPrefs.GetFloat(dynamicBone.name + "Inert"),
                                EditorPrefs.GetFloat(dynamicBone.name + "Radius"),

                                EditorPrefs.GetFloat(dynamicBone.name + "EndOffsetX"),
                                EditorPrefs.GetFloat(dynamicBone.name + "EndOffsetY"),
                                EditorPrefs.GetFloat(dynamicBone.name + "EndOffsetZ"),

                                EditorPrefs.GetFloat(dynamicBone.name + "GravityX"),
                                EditorPrefs.GetFloat(dynamicBone.name + "GravityY"),
                                EditorPrefs.GetFloat(dynamicBone.name + "GravityZ"),

                                EditorPrefs.GetFloat(dynamicBone.name + "ForceX"),
                                EditorPrefs.GetFloat(dynamicBone.name + "ForceY"),
                                EditorPrefs.GetFloat(dynamicBone.name + "ForceZ")
                                );

                            // Registry garbage collect
                            EditorPrefs.DeleteKey(dynamicBone.name + "Damping");
                            EditorPrefs.DeleteKey(dynamicBone.name + "Elasticity");
                            EditorPrefs.DeleteKey(dynamicBone.name + "Stiffness");
                            EditorPrefs.DeleteKey(dynamicBone.name + "Inert");
                            EditorPrefs.DeleteKey(dynamicBone.name + "Radius");

                            EditorPrefs.DeleteKey(dynamicBone.name + "EndOffsetX");
                            EditorPrefs.DeleteKey(dynamicBone.name + "EndOffsetY");
                            EditorPrefs.DeleteKey(dynamicBone.name + "EndOffsetZ");

                            EditorPrefs.DeleteKey(dynamicBone.name + "GravityX");
                            EditorPrefs.DeleteKey(dynamicBone.name + "GravityY");
                            EditorPrefs.DeleteKey(dynamicBone.name + "GravityZ");

                            EditorPrefs.DeleteKey(dynamicBone.name + "ForceX");
                            EditorPrefs.DeleteKey(dynamicBone.name + "ForceY");
                            EditorPrefs.DeleteKey(dynamicBone.name + "ForceZ");
                        }
                        EditorPrefs.DeleteKey("DynamicBoneStudioDataSaved");
                    }
                }

                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
                {
                    foreach (var dynamicBone in _allDynamicBones)
                    {
                        if (dynamicBone == null)
                            continue;

                        GUILayout.BeginVertical(EditorStyles.helpBox);
                        {
                            GUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField(dynamicBone.name, EditorStyles.boldLabel, new []{GUILayout.ExpandWidth(false)});
                            if (_isAskForBoneNameEnabled)
                            {
                                if (GUILayout.Button("Save as preset"))
                                {
                                    var saveInstance = CreateInstance<SavePresetWindow>();
                                    saveInstance.ShowUtility();
                                    _isPresetNameSet = true;
                                    _presetSaveInstance = saveInstance;
                                }
                            }
                            else
                            {
                                if (GUILayout.Button("Save as preset '" + dynamicBone.name + "'"))
                                {
                                    SaveDynamicBonePreset(dynamicBone.name, dynamicBone);
                                    LoadDynamicBonePresets();
                                }
                            }
                            if (_isPresetNameSet && _presetSaveInstance.GetPresetName() != "")
                            {
                                //SO SCUFFED LMAO
                                SaveDynamicBonePreset(_presetSaveInstance.presetName, dynamicBone);
                                _presetSaveInstance.presetName = "";
                                _presetSaveInstance = null;
                                _isPresetNameSet = false;
                                LoadDynamicBonePresets();
                            }
                            GUILayout.EndHorizontal();
                            GUILayout.BeginHorizontal();
                            if (_dynamicBonePresets.Count > 0)
                            {
                                EditorGUILayout.LabelField("Load preset:", new[] {GUILayout.ExpandWidth(false)});
                                _presetChoiceIndex = 
                                        EditorGUILayout.Popup(_presetChoiceIndex, _dynamicBonePresets.Select(x=>x.Name)
                                        .ToArray());
                                if (GUILayout.Button("Load"))
                                {
                                    var preset = LoadSingleDynamicBonePreset(_dynamicBonePresets.ElementAtOrDefault(_presetChoiceIndex).Name);
                                    if (preset != null)
                                    {
                                        UpdateDynamicBone(dynamicBone, true, dynamicBonePreset: preset);
                                    }
                                    else
                                    {
                                        Debug.Log("Preset was null");
                                    }
                                }
                            }

                            GUILayout.EndHorizontal();
                            UpdateDynamicBone(dynamicBone);
                        }
                        GUILayout.EndVertical();
                    }
                }

                EditorGUILayout.EndScrollView();

                break;
            }
        }
        
        _isAboutShowing = EditorGUILayout.Foldout(_isAboutShowing, "About");

        if (_isAboutShowing)
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Dynamic Bones Studio v0.01\n" +
                                    "by Kaori\n\n" +
                                    "Feedback or bugs can be posted to GitHub or sent to me through discord:\n" +
                                    "Kaori#0420", EditorStyles.textArea);
            GUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUI.indentLevel*15);
            if (GUILayout.Button("GitHub", new[] { GUILayout.ExpandWidth(false) }))
            {
                Application.OpenURL("https://github.com/kaaori/DynamicBonesStudio");
            }
            if (GUILayout.Button("Buy me a coffee", new[] { GUILayout.ExpandWidth(false) }))
            {
                Application.OpenURL("https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=JZYZWQQZRJ4FC");
            }
            GUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
            GUILayout.EndVertical();
        }
    }

    private DynamicBonesPreset LoadSingleDynamicBonePreset(string presetName)
    {
        if (_configFile == null)
        {
            InitConfigFile();
        }
        var section = _configFile.GetSection(presetName);
        var dynamicBonePreset = new DynamicBonesPreset();
        var tempOffsetVec3 = Vector3.zero;
        var tempGravityVec3 = Vector3.zero;
        var tempForceVec3 = Vector3.zero;
        foreach (var sectionKey in section.Keys)
        {
            switch (sectionKey)
            {
                case "Name":
                    dynamicBonePreset.Name = section[sectionKey];
                    break;
                case "Damp":
                    dynamicBonePreset.Damping = float.Parse(section[sectionKey]);
                    break;
                case "Elasticity":
                    dynamicBonePreset.Elasticity = float.Parse(section[sectionKey]);
                    break;
                case "Stiff":
                    dynamicBonePreset.Stiffness = float.Parse(section[sectionKey]);
                    break;
                case "Inert":
                    dynamicBonePreset.Inert = float.Parse(section[sectionKey]);
                    break;
                case "Radius":
                    dynamicBonePreset.Radius = float.Parse(section[sectionKey]);
                    break;

                case "EndOffsetX":
                    tempOffsetVec3.x = float.Parse(section[sectionKey]);
                    break;
                case "EndOffsetY":
                    tempOffsetVec3.y = float.Parse(section[sectionKey]);
                    break;
                case "EndOffsetZ":
                    tempOffsetVec3.z = float.Parse(section[sectionKey]);
                    break;

                case "GravityX":
                    tempGravityVec3.x = float.Parse(section[sectionKey]);
                    break;
                case "GravityY":
                    tempGravityVec3.y = float.Parse(section[sectionKey]);
                    break;
                case "GravityZ":
                    tempGravityVec3.z = float.Parse(section[sectionKey]);
                    break;

                case "ForceX":
                    tempForceVec3.x = float.Parse(section[sectionKey]);
                    break;
                case "ForceY":
                    tempForceVec3.y = float.Parse(section[sectionKey]);
                    break;
                case "ForceZ":
                    tempForceVec3.z = float.Parse(section[sectionKey]);
                    break;

            }
        }
        dynamicBonePreset.EndOffset = tempOffsetVec3;
        dynamicBonePreset.Gravity = tempGravityVec3;
        dynamicBonePreset.Force = tempForceVec3;
        return dynamicBonePreset;
    }

    private void LoadDynamicBonePresets()
    {
        if (_configFile == null)
        {
            InitConfigFile();
        }

        _configFile.Refresh();

        if (_configFile.SectionNames != null)
        {
            foreach (var configFileSectionName in _configFile.SectionNames)
            {
                if (configFileSectionName == "[AccessoryWhitelist]")
                {
                    continue;
                }
                //var section = _configFile.GetSection(configFileSectionName);
                var dynamicBonePreset = LoadSingleDynamicBonePreset(configFileSectionName);
                _dynamicBonePresets.Add(dynamicBonePreset);
            }
        }
        else
        {
            Debug.Log("No section names found");
        }
    }

    private void UpdateDynamicBone(DynamicBone dynamicBone, bool isSetValue = false, float dampFloat = 0f,
            float elastFloat = 0f, float stiffFloat = 0f,
            float inertFloat = 0f, float radiusFloat = 0f,
            float endOffsetX = 0f, float endOffsetY = 0f, float endOffsetZ = 0f, // End offset vec3
            float gravityX = 0f, float gravityY = 0f, float gravityZ = 0f,       // gravity vec3
            float forceX = 0f, float forceY = 0f, float forceZ = 0f,             // force vec3
            DynamicBonesPreset dynamicBonePreset = null)                        
    {
        var dynamicBoneTarget = dynamicBone;
        var dynamicBoneSo = new SerializedObject(dynamicBoneTarget);
        var root = dynamicBoneSo.FindProperty("m_Root");
        var damp = dynamicBoneSo.FindProperty("m_Damping");
        var elast = dynamicBoneSo.FindProperty("m_Elasticity");
        var stiff = dynamicBoneSo.FindProperty("m_Stiffness");
        var inert = dynamicBoneSo.FindProperty("m_Inert");
        var radius = dynamicBoneSo.FindProperty("m_Radius");
        var endLength = dynamicBoneSo.FindProperty("m_EndLength");
        var endOffset = dynamicBoneSo.FindProperty("m_EndOffset");
        var grav = dynamicBoneSo.FindProperty("m_Gravity");
        var force = dynamicBoneSo.FindProperty("m_Force");
        var colliders = dynamicBoneSo.FindProperty("m_Colliders");
        var exclusions = dynamicBoneSo.FindProperty("m_Exclusions");

        if (isSetValue)
        {
            if (dynamicBonePreset != null)
            {
                dampFloat = dynamicBonePreset.Damping;
                elastFloat = dynamicBonePreset.Elasticity;
                stiffFloat = dynamicBonePreset.Stiffness;
                inertFloat = dynamicBonePreset.Inert;
                radiusFloat = dynamicBonePreset.Radius;

                endOffsetX = dynamicBonePreset.EndOffset.x;
                endOffsetY = dynamicBonePreset.EndOffset.y;
                endOffsetZ = dynamicBonePreset.EndOffset.z;

                gravityX = dynamicBonePreset.Gravity.x;
                gravityY = dynamicBonePreset.Gravity.y;
                gravityZ = dynamicBonePreset.Gravity.z;

                forceX = dynamicBonePreset.Force.x;
                forceY = dynamicBonePreset.Force.y;
                forceZ = dynamicBonePreset.Force.z;

            }
            damp.floatValue = dampFloat;
            elast.floatValue = elastFloat;
            stiff.floatValue = stiffFloat;
            inert.floatValue = inertFloat;
            radius.floatValue = radiusFloat;
            endOffset.vector3Value = new Vector3(endOffsetX, endOffsetY, endOffsetZ);
            grav.vector3Value = new Vector3(gravityX, gravityY, gravityZ);
            force.vector3Value = new Vector3(forceX, forceY, forceZ);
        }

        EditorGUI.indentLevel = 1;
        {
            EditorGUILayout.PropertyField(damp, true);
            EditorGUILayout.PropertyField(elast, true);
            EditorGUILayout.PropertyField(stiff, true);
            EditorGUILayout.PropertyField(inert, true);
            EditorGUILayout.PropertyField(radius, true);
            EditorGUILayout.PropertyField(endLength, true);

            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.PropertyField(endOffset, true, new[] { GUILayout.ExpandWidth(false) });
                EditorGUILayout.PropertyField(grav, true, new[] { GUILayout.ExpandWidth(false) });
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(force, true, new[] { GUILayout.ExpandWidth(false) });

            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.PropertyField(colliders, true, new[] { GUILayout.ExpandWidth(false) });
                EditorGUILayout.PropertyField(exclusions, true, new[] { GUILayout.ExpandWidth(false) });
            }
            EditorGUILayout.EndHorizontal();

        }
        EditorGUI.indentLevel = 0;

        dynamicBoneSo.ApplyModifiedProperties();
    }

    private List<Transform> TryFindCommonAccessories()
    {
        var configFileWhitelist = LoadAccessoryList();
        var prevAccessories = AccessoriesTransforms;
        if (_avatar == null)
        {
            return AccessoriesTransforms;
        }
        //if (configFileWhitelist != null)
        //{
        //    _commonAccessories = configFileWhitelist.Values.ToList();
        //}

        // Clean list of any missing items
        AccessoriesTransforms.RemoveAll(x => x == null);

        var accessoriesTempList = _commonAccessories
            .Select(commonAccessory =>
                AllBones.FirstOrDefault(x => x.name.ToLowerInvariant().Contains(commonAccessory)))
            .Where(tempAdd => tempAdd != null).ToList();
        // Non-linq variant of ^
        //var accessoriesTempList = new List<Transform>();
        //foreach (var commonAccessory in _commonAccessories)
        //{
        //    var tempAdd = AllBones.FirstOrDefault(x => x.name.ToLowerInvariant().Contains(commonAccessory.ToLowerInvariant()));
        //    if (tempAdd != null)
        //    {
        //        accessoriesTempList.Add(tempAdd);
        //    }
        //}

        if (accessoriesTempList.Any(x => x.name.ToLowerInvariant().Contains("ear")) &&
            accessoriesTempList.Any(x => x.name.ToLowerInvariant().Contains("gimmick")))
        {
            var ear = accessoriesTempList.FirstOrDefault(x => x.name.ToLowerInvariant().Contains("ear"));
            var gimmickL = accessoriesTempList.FirstOrDefault(x => x.name.ToLowerInvariant().Contains("gimmick_l"));
            var gimmickR = accessoriesTempList.FirstOrDefault(x => x.name.ToLowerInvariant().Contains("gimmick_r"));

            var useEar = EditorUtility.DisplayDialog("Potential Issue",
                "Found two potential ear root bones, which one would you like to apply the dynamic bone to?",
                ear.name, "Gimmick L & R");

            if (useEar)
            {
                accessoriesTempList.Remove(gimmickL);
                accessoriesTempList.Remove(gimmickR);
            }
            else
            {
                accessoriesTempList.Remove(ear);
            }
        }


        accessoriesTempList = accessoriesTempList.Union(prevAccessories).ToList();
        return accessoriesTempList.Count > 0 ? accessoriesTempList : null;
    }

    private Transform TryFindHairRoot()
    {
        if (_avatar == null)
        {
            return null;
        }

        var hairTransform = _avatarAnim.GetBoneTransform(HumanBodyBones.Head).GetComponentsInChildren<Transform>().FirstOrDefault(x=>x.name.ToLowerInvariant().Contains("hair"));
        if (hairTransform != null)
        {
            Debug.Log("Auto Dynamic Bones - Found transform named 'Hair' in children. Setting as default hair root");
            return hairTransform;
        }
        return null;
    }

    public void AddDynamicBones()
    {
        var presets = LoadIniPreset();
        if (_avatar.GetComponentsInChildren<DynamicBone>() != null)
        {
            var result = EditorUtility.DisplayDialog("Warning!",
                "Add dynamic bones now?\nNOTE: THIS WILL DELETE ALL EXISTING DYANMIC BONES, EVEN IN CHILDREN", "Yes",
                "No");
            if (!result)
            {
                return;
            }
            Debug.Log("Destroying existing dynamic bones.");
            foreach (var dynamicBone in _avatar.gameObject.GetComponentsInChildren<DynamicBone>())
            {
                DestroyImmediate(dynamicBone);
            }
        }
        Debug.Log("Auto Dynamic Bones - Applying dynamic bones");
        if (_hairBone != null)
        {
            var hairDynamicBone = _hairBone.gameObject.AddComponent<DynamicBone>();
            hairDynamicBone.m_Root = _hairBone;

            Debug.Log("Adding dynamic bones.");

            // TODO: Replace when new preset system is in place
            var hairPreset = presets.FirstOrDefault(x => x.Name == "Hair");
            if (hairPreset != null)
            {
                EditorUtility.SetDirty(hairDynamicBone);
                UpdateDynamicBone(hairDynamicBone, true,
                    hairPreset.Damping,
                    hairPreset.Elasticity,
                    hairPreset.Stiffness,
                    hairPreset.Inert,
                    hairPreset.Radius
                );
            }
        }
        

        foreach (var accessory in AccessoriesTransforms)
        {
            var accessoryBone = accessory.gameObject.AddComponent<DynamicBone>();
            accessoryBone.m_Root = accessory;
        }
    }


    private void SaveDynamicBonePreset(string presetName, DynamicBone bone)
    {
        if (_configFile == null)
        {
            InitConfigFile();
        }
        _configFile.Refresh();

        //_configFile.SetValue(presetName, "UpdateRate", bone.m_UpdateRate.ToString());

        _configFile.SetValue(presetName, "Name", presetName);

        _configFile.SetValue(presetName, "Damp", bone.m_Damping.ToString());
        _configFile.SetValue(presetName, "Elasticity", bone.m_Elasticity.ToString());
        _configFile.SetValue(presetName, "Stiff", bone.m_Stiffness.ToString());
        _configFile.SetValue(presetName, "Inert", bone.m_Inert.ToString());
        _configFile.SetValue(presetName, "Radius", bone.m_Radius.ToString());

        // End offset vec3
        _configFile.SetValue(presetName, "EndOffsetX", bone.m_EndOffset.x.ToString());
        _configFile.SetValue(presetName, "EndOffsetY", bone.m_EndOffset.y.ToString());
        _configFile.SetValue(presetName, "EndOffsetZ", bone.m_EndOffset.z.ToString());

        // Gravity vec3
        _configFile.SetValue(presetName, "GravityX", bone.m_Gravity.x.ToString());
        _configFile.SetValue(presetName, "GravityY", bone.m_Gravity.y.ToString());
        _configFile.SetValue(presetName, "GravityZ", bone.m_Gravity.z.ToString());

        // Force vec3
        _configFile.SetValue(presetName, "ForceX", bone.m_Force.x.ToString());
        _configFile.SetValue(presetName, "ForceY", bone.m_Force.y.ToString());
        _configFile.SetValue(presetName, "ForceZ", bone.m_Force.z.ToString());



    }
    /** TODO Presets
*  - In "studio" tab
*       - Set of sliders for each "accessory" added
*       - Text box to name accessory (default to bone name)
*       - Save to preset button
*       - Load presets as a dropdown list to apply to any set of sliders
*/
    private void InitConfigFile()
    {
        //Application.dataPath + "/Kaori/DynamicBonesStudio/Editor/DynamicBonesPresets.cfg"
        _configFile = File.Exists(_cfgFilePath) 
            ? new IniFile(Application.dataPath + "/Kaori/DynamicBonesStudio/Editor/DynamicBonesPresets.cfg") 
            : new IniFile();
        //_configFile.Save(Application.dataPath + "/Kaori/DynamicBonesStudio/Editor/DynamicBonesPresets.cfg");
    }

    private Dictionary<string, string> LoadAccessoryList()
    {
        if (_configFile == null)
        {
            InitConfigFile();
        }
        var accessoryWhitelist = _configFile.GetSection("AccessoryWhitelist");
        if (accessoryWhitelist != null)
        {
            _commonAccessories = accessoryWhitelist.Values.ToList();
        }
        return accessoryWhitelist;
    }

    public List<DynamicBonesPreset> LoadIniPreset()
    {
        if (!File.Exists(Application.dataPath + "/Kaori/DynamicBonesStudio/Editor/DynamicBonesPresets.cfg"))
        {
            InitConfigFile();
        }
        var cfgFile = new IniFile(Application.dataPath + "/Kaori/DynamicBonesStudio/Editor/DynamicBonesPresets.cfg");

        // TODO: Write default preset values
        // TODO: Gravity/Force etc
        // Hair defaults
        cfgFile.SetValue("DynamicBonePreset", "HairUpdateRate", "60" );
        cfgFile.SetValue("DynamicBonePreset", "HairType", "Hair" );
        cfgFile.SetValue("DynamicBonePreset", "HairDamp", "0.2");
        cfgFile.SetValue("DynamicBonePreset", "HairElasticity", "0.05");
        cfgFile.SetValue("DynamicBonePreset", "HairInert", "0");
        cfgFile.SetValue("DynamicBonePreset", "HairRadius", "0");
        cfgFile.SetValue("DynamicBonePreset", "HairStiff", "0.8");

        //cfgFile.Save(Application.dataPath + "/Kaori/DynamicBonesStudio/Editor/DynamicBonesPresets.cfg");

        return ReadPresets(cfgFile);
    }

    private List<DynamicBonesPreset> ReadPresets(IniFile ConfigFile)
    {
        try
        {
            var dynamicbonesPresets = new List<DynamicBonesPreset>();
            var hairPreset = new DynamicBonesPreset
            {
                Name = ConfigFile.GetValue("DynamicBonePreset", "HairType"),
                UpdateRate = float.Parse(ConfigFile.GetValue("DynamicBonePreset", "HairUpdateRate")),
                Damping = float.Parse(ConfigFile.GetValue("DynamicBonePreset", "HairDamp")),
                Elasticity = float.Parse(ConfigFile.GetValue("DynamicBonePreset", "HairElasticity")),
                Inert = float.Parse(ConfigFile.GetValue("DynamicBonePreset", "HairInert")),
                Radius = float.Parse(ConfigFile.GetValue("DynamicBonePreset", "HairRadius")),
                Stiffness = float.Parse(ConfigFile.GetValue("DynamicBonePreset", "HairStiff"))
            };
            dynamicbonesPresets.Add(hairPreset);
            return dynamicbonesPresets;
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
        }
        return null;
    }
}