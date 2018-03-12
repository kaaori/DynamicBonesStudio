using UnityEngine;
using UnityEditor;

class SavePresetWindow : EditorWindow
{

    public string presetName = "";
    private bool isClicked;

    void OnGUI()
    {
        presetName = EditorGUILayout.TextField("Preset Name", presetName);

        if (GUILayout.Button("Save Preset"))
        {
            OnClickSavePrefab();
            GUIUtility.ExitGUI();
        }
    }

    public string GetPresetName()
    {
        return isClicked ? 
            presetName : "";
    }

    void OnClickSavePrefab()
    {
        presetName = presetName.Trim();
        isClicked = true;
        if (string.IsNullOrEmpty(presetName))
        {
            EditorUtility.DisplayDialog("Unable to save Preset", "Please specify a valid Preset name.", "Close");
            return;
        }



        Close();
    }

}