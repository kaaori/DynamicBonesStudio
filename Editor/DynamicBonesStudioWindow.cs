using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VRCSDK2;

/**
 * Planned features:
 * - Single bone mode
 *      - Adds a single bone to the root with exclusions, for very basic dynamic bones/testing parameters
 * - Multi bone mode
 *      - Adds a bone to every bone in the list, ?with exclusions?
 *      - Save json file of preferred dynamic bone parameters (for hair/skirt/etc) for quick avatar testing/applying
 *      
 * - Colliders?
 *      - Automatically place colliders on leg/hip/head/index fingers with sliders to easily adjust sizes.
 *      - Add the colliders to desired bones (hair/ears/skirt/etc)
 */

public class DynamicBonesPreset
{
    public string Type { get; set; }
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

    // TODO: Move to ini file with storable values.
    private readonly List<string> _commonAccessories = new List<string> {"skirt", "gimmick", "ear", "scarf", "tie", "tail", "breast"};
    private List<DynamicBone> _tempBones = new List<DynamicBone>();
    private GameObject _avatar = null;
    private Animator _avatarAnim = null;

    private List<DynamicBone> _allDynamicBones = new List<DynamicBone>();
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

    void HandleOnPlayModeChanged()
    {
        if (_isAutoRefreshEnabled)
        {
                _allDynamicBones = _avatar.GetComponentsInChildren<DynamicBone>().ToList();
        }

    }
    void OnGUI()
    {
        _selectedTabIndex = GUILayout.Toolbar(_selectedTabIndex, new string[]{"Basic setup", "Studio"});
        EditorApplication.playmodeStateChanged += HandleOnPlayModeChanged;
        switch (_selectedTabIndex)
        {
            // Basic setup tab
            case 0:
                _selectedTabIndex = 0;

                EditorGUILayout.LabelField("Avatar:", EditorStyles.boldLabel);
                _avatar = (GameObject) EditorGUILayout.ObjectField(_avatar, typeof(GameObject), true);

                // Check for VRC Avatar Descriptor
                if (_avatar != null && _avatar.GetComponent<VRC_AvatarDescriptor>() == null)
                {
                    EditorUtility.DisplayDialog("Error",
                        "You need to select a game object with a VRC Avatar Descriptor and an Animator!", "Ok");
                    _avatar = null;
                    return;
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
                _hairBone = (Transform)EditorGUILayout.ObjectField(_hairBone, typeof(Transform), true);

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
                    AddDynamicBones();
                }
                break;

            // Studio tab
            case 1:
                _selectedTabIndex = 1;

                // If avatar is not assigned exit
                if (_avatar == null)
                {
                    EditorGUILayout.LabelField("Select an avatar in the basic setup tab to begin", EditorStyles.boldLabel);
                    return;
                }

                //groupEnabled = EditorGUILayout.BeginToggleGroup("Default Bone Settings", groupEnabled);
                //{
                //    myBool = EditorGUILayout.Toggle("Toggle", myBool);
                //    myFloat = EditorGUILayout.Slider("Slider", myFloat, -3, 3);
                //}
                //EditorGUILayout.EndToggleGroup();
                GUILayout.Label("Experimental features:", EditorStyles.boldLabel);
                GUILayout.BeginHorizontal();
                _isAutoRefreshEnabled = EditorGUILayout.ToggleLeft("Auto-refresh bones on state change", _isAutoRefreshEnabled);
                if (!_isAutoRefreshEnabled && GUILayout.Button("Refresh bones"))
                {
                    _allDynamicBones = _avatar.GetComponentsInChildren<DynamicBone>().ToList();
                    Debug.Log("Found "+_allDynamicBones.Count+" new dynamic bones.");
                }
                GUILayout.EndHorizontal();
                //_isAutoApplyEnabled = EditorGUILayout.ToggleLeft("Auto-apply bone settings on stop", _isAutoApplyEnabled);
                //if (EditorApplication.isPlaying && _isAutoApplyEnabled)
                //{
                //    tempBones = _allDynamicBones;
                //}
                if (EditorApplication.isPlaying)
                { 
                    if (GUILayout.Button("Save play-mode bone settings"))
                    {
                        _tempBones = _allDynamicBones;
                        foreach (var dynamicBone in _allDynamicBones)
                        {
                            EditorPrefs.SetFloat(dynamicBone.name + "Damping", dynamicBone.m_Damping);
                            EditorPrefs.SetFloat(dynamicBone.name + "Elasticity", dynamicBone.m_Elasticity);
                            EditorPrefs.SetFloat(dynamicBone.name + "Stiffness", dynamicBone.m_Stiffness);
                            EditorPrefs.SetFloat(dynamicBone.name + "Inert", dynamicBone.m_Inert);
                            EditorPrefs.SetFloat(dynamicBone.name + "Radius", dynamicBone.m_Radius);
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
                                EditorPrefs.GetFloat(dynamicBone.name + "Radius"));

                            // Registry garbage collect
                            EditorPrefs.DeleteKey(dynamicBone.name + "Damping");
                            EditorPrefs.DeleteKey(dynamicBone.name + "Elasticity");
                            EditorPrefs.DeleteKey(dynamicBone.name + "Stiffness");
                            EditorPrefs.DeleteKey(dynamicBone.name + "Inert");
                            EditorPrefs.DeleteKey(dynamicBone.name + "Radius");
                        }
                        EditorPrefs.DeleteKey("DynamicBoneStudioDataSaved");
                    }
                }
                foreach (var dynamicBone in _allDynamicBones)
                {
                    if (dynamicBone == null)
                        continue;
                    EditorGUILayout.LabelField(dynamicBone.name, EditorStyles.boldLabel);
                    UpdateDynamicBone(dynamicBone);
                }
                break;
        }
    }

    private static void UpdateDynamicBone(DynamicBone dynamicBone, bool isSetValue = false, float dampFloat = 0f, 
                                                                    float elastFloat = 0f, float stiffFloat = 0f, 
                                                                    float inertFloat = 0f, float radiusFloat = 0f)
    {
        var dynamicBoneTarget = dynamicBone;
        var dynamicBoneSo = new SerializedObject(dynamicBoneTarget);
        var damp = dynamicBoneSo.FindProperty("m_Damping");
        var elast = dynamicBoneSo.FindProperty("m_Elasticity");
        var stiff = dynamicBoneSo.FindProperty("m_Stiffness");
        var inert = dynamicBoneSo.FindProperty("m_Inert");
        var radius = dynamicBoneSo.FindProperty("m_Radius");
        if (isSetValue)
        {
            damp.floatValue = dampFloat;
            elast.floatValue = elastFloat;
            stiff.floatValue = stiffFloat;
            inert.floatValue = inertFloat;
            radius.floatValue = radiusFloat;

        }
        EditorGUILayout.PropertyField(damp, true);
        EditorGUILayout.PropertyField(elast, true);
        EditorGUILayout.PropertyField(stiff, true);
        EditorGUILayout.PropertyField(inert, true);
        EditorGUILayout.PropertyField(radius, true);

        dynamicBoneSo.ApplyModifiedProperties();
    }


    private List<Transform> TryFindCommonAccessories()
    {
        var prevAccessories = AccessoriesTransforms;
        if (_avatar == null)
        {
            return AccessoriesTransforms;
        }

        // Clean list of any missing items
        AccessoriesTransforms.RemoveAll(x => x == null);

        var accessoriesTempList = _commonAccessories
            .Select(commonAccessory =>
                AllBones.FirstOrDefault(x => x.name.ToLowerInvariant().Contains(commonAccessory)))
            .Where(tempAdd => tempAdd != null).ToList();

        if (accessoriesTempList.Any(x => x.name.ToLowerInvariant().Contains("ear")) &&
            accessoriesTempList.Any(x => x.name.ToLowerInvariant().Contains("gimmick")))
        {
            var ear = accessoriesTempList.FirstOrDefault(x => x.name.ToLowerInvariant().Contains("ear"));
            var gimmick = accessoriesTempList.FirstOrDefault(x => x.name.ToLowerInvariant().Contains("gimmick"));

            var useEar = EditorUtility.DisplayDialog("Potential Issue", "Found two potential ear root bones, which one would you like to apply the dynamic bone to?",
                ear.name, gimmick.name);

            // If user chooses to keep the ear bone, remove gimmick from list and vice versa.
            accessoriesTempList.Remove(useEar ? gimmick : ear);
        }

        // Non-linq variant of ^
        //foreach (var commonAccessory in commonAccessories)
        //{
        //    var tempAdd = allBones.FirstOrDefault(x => x.name.ToLowerInvariant().Contains(commonAccessory.ToLowerInvariant()));
        //    if (tempAdd != null)
        //    {
        //        accessoriesTempList.Add(tempAdd);
        //    }
        //}
        accessoriesTempList = accessoriesTempList.Union(prevAccessories).ToList();
        return accessoriesTempList.Count > 0 ? accessoriesTempList : null;
    }

    private Transform TryFindHairRoot()
    {
        if (_avatar == null)
        {
            return null;
        }

        var headBone = _avatarAnim.GetBoneTransform(HumanBodyBones.Head);
        if (headBone.transform.Find("Hair") != null)
        {
            Debug.Log("Auto Dynamic Bones - Found transform named 'Hair' in children. Setting as default hair root");
            return headBone.transform.Find("Hair");
        }
        return null;
    }

    public void AddDynamicBones()
    {
        var presets = LoadIniPreset();
        if (_avatar.GetComponentsInChildren<DynamicBone>() != null)
        {
            var result = EditorUtility.DisplayDialog("Warning!",
                "Add dynamic bones now?\nNOTE: THIS WILL DELETE ALL EXISTING DYANMIC BONES, EVEN IN CHILDREN", "Yes", "No");
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
        var hairDynamicBone = _hairBone.gameObject.AddComponent<DynamicBone>();
        hairDynamicBone.m_Root = _hairBone;

        Debug.Log("Adding dynamic bones.");

        // TODO: Replace when new preset system is in place
        var hairPreset = presets.FirstOrDefault(x => x.Type == "Hair");
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

        foreach (var accessory in AccessoriesTransforms)
        {
            var accessoryBone = accessory.gameObject.AddComponent<DynamicBone>();
            accessoryBone.m_Root = accessory;
        }
    }

    /** TODO Presets
*  - In "studio" tab
*       - Set of sliders for each "accessory" added
*       - Text box to name accessory (default to bone name)
*       - Save to preset button
*       - Load presets as a dropdown list to apply to any set of sliders
*/


    public List<DynamicBonesPreset> LoadIniPreset()
    {
    
        if (File.Exists(Application.dataPath + "/Kaori/DynamicBonesStudio/Editor/DynamicBonesPresets.ini"))
        {
            var iniFile = new IniFile(Application.dataPath + "/Kaori/DynamicBonesStudio/Editor/DynamicBonesPresets.ini");
            return ReadPresets(iniFile);
        }

        var newIniFile = new IniFile(Application.dataPath + "/Kaori/DynamicBonesStudio/Editor/DynamicBonesPresets.ini");
        // TODO: Write default preset values
        // TODO: Gravity/Force etc
        // Hair defaults
        newIniFile.Write("HairUpdateRate", "60");
        newIniFile.Write("HairType", "Hair");
        newIniFile.Write("HairDamp", "0.2");
        newIniFile.Write("HairElasticity", "0.05");
        newIniFile.Write("HairInert", "0");
        newIniFile.Write("HairRadius", "0");
        newIniFile.Write("HairStiff", "0.8");

        return ReadPresets(newIniFile);
    }

    private List<DynamicBonesPreset> ReadPresets(IniFile iniFile)
    {
        try
        {
            var dynamicbonesPresets = new List<DynamicBonesPreset>();
            var hairPreset = new DynamicBonesPreset
            {
                Type = iniFile.Read("HairType"),
                UpdateRate = float.Parse(iniFile.Read("HairUpdateRate")),
                Damping = float.Parse(iniFile.Read("HairDamp")),
                Elasticity = float.Parse(iniFile.Read("HairElasticity")),
                Inert = float.Parse(iniFile.Read("HairInert")),
                Radius = float.Parse(iniFile.Read("HairRadius")),
                Stiffness = float.Parse(iniFile.Read("HairStiff"))
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
