using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// 배열 항목마다 클립과 볼륨 조절 영역을 두 줄로 표시한다.
[CustomPropertyDrawer(typeof(SoundDatabaseSO.ClipVolumeEntry))]
public class ClipVolumeEntryDrawer : PropertyDrawer
{
    private const float VolumeWidth = 64f;
    private const float VolumeLabelWidth = 48f;
    private const float SliderMax = 2f;
    private const float Spacing = 4f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => EditorGUIUtility.singleLineHeight * 2f + Spacing * 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        int previousIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        var clipRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        var labelRect = new Rect(position.x, clipRect.yMax + Spacing, VolumeLabelWidth, clipRect.height);
        var sliderRect = new Rect(labelRect.xMax, labelRect.y,
            Mathf.Max(0f, position.width - VolumeLabelWidth - VolumeWidth - Spacing), clipRect.height);
        var volumeRect = new Rect(sliderRect.xMax + Spacing, labelRect.y, VolumeWidth, clipRect.height);
        var volumeProperty = property.FindPropertyRelative("volumeMultiplier");

        EditorGUI.PropertyField(clipRect, property.FindPropertyRelative("clip"), GUIContent.none);
        EditorGUI.LabelField(labelRect, "Volume");
        bool previousMixedValue = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = volumeProperty.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        float sliderValue = GUI.HorizontalSlider(sliderRect,
            Mathf.Clamp(volumeProperty.floatValue, 0f, SliderMax), 0f, SliderMax);
        if (EditorGUI.EndChangeCheck())
            volumeProperty.floatValue = sliderValue;
        EditorGUI.showMixedValue = previousMixedValue;

        // 숫자 입력은 기존처럼 2배보다 큰 값도 허용한다.
        EditorGUI.PropertyField(volumeRect, volumeProperty, GUIContent.none);

        EditorGUI.indentLevel = previousIndent;
        EditorGUI.EndProperty();
    }

    // Unity 기본 인스펙터의 UI Toolkit에서도 같은 두 줄 레이아웃을 사용한다.
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        var root = new VisualElement();
        root.style.paddingBottom = Spacing;

        var clipField = new PropertyField(property.FindPropertyRelative("clip"), string.Empty);
        clipField.style.minWidth = 0;
        root.Add(clipField);

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginTop = Spacing;

        var volumeLabel = new Label("Volume");
        volumeLabel.style.width = VolumeLabelWidth;
        volumeLabel.style.flexShrink = 0;

        var volumeProperty = property.FindPropertyRelative("volumeMultiplier");
        var slider = new Slider(0f, SliderMax);
        slider.style.flexGrow = 1;
        slider.style.flexBasis = 0;
        slider.style.minWidth = 0;
        slider.style.marginRight = Spacing;
        slider.SetValueWithoutNotify(volumeProperty.floatValue);
        slider.showMixedValue = volumeProperty.hasMultipleDifferentValues;
        slider.RegisterValueChangedCallback(evt =>
        {
            volumeProperty.floatValue = Mathf.Clamp(evt.newValue, 0f, SliderMax);
            volumeProperty.serializedObject.ApplyModifiedProperties();
        });
        // 숫자 입력과 Undo를 동기화하며, 표시만으로 범위 밖의 저장값을 바꾸지 않는다.
        slider.TrackPropertyValue(volumeProperty, updatedProperty =>
        {
            slider.SetValueWithoutNotify(updatedProperty.floatValue);
            slider.showMixedValue = updatedProperty.hasMultipleDifferentValues;
        });

        var volumeField = new PropertyField(volumeProperty, string.Empty);
        volumeField.style.width = VolumeWidth;
        volumeField.style.flexShrink = 0;

        row.Add(volumeLabel);
        row.Add(slider);
        row.Add(volumeField);
        root.Add(row);
        return root;
    }
}
