using UnityEngine;
using UnityEditor;

class SavePresetWindow : EditorWindow
{

    public string PresetName = "";
    private bool _isClicked;

    void OnGUI()
    {
        PresetName = EditorGUILayout.TextField("Preset Name", PresetName);
        if (GUILayout.Button("Save Preset"))
        {
            OnClickSavePrefab();
            GUIUtility.ExitGUI();
        }
    }

    public string GetPresetName()
    {
        return _isClicked ? 
            PresetName : "";
    }

    void OnClickSavePrefab()
    {
        PresetName = PresetName.Trim();
        _isClicked = true;
        if (string.IsNullOrEmpty(PresetName))
        {
            EditorUtility.DisplayDialog("Unable to save Preset", "Please specify a valid Preset name.", "Close");
            return;
        }
        Close();
    }

}