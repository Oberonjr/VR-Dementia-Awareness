using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(JulietteConversation))]
public class JulietteConversationEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        JulietteConversation conversation = (JulietteConversation)target;
        if (GUILayout.Button("Sit her down"))
        {
            EventBus<OnOpenDoorAnim>.Publish(new OnOpenDoorAnim());                                                                                                                                                                                                                                                          
            EventBus<OnWalkAnim>.Publish(new OnWalkAnim()); 
            EventBus<OnSitAnim>.Publish(new OnSitAnim());
        }
    }
}
