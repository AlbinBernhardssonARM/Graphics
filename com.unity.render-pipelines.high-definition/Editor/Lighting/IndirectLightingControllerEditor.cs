using UnityEngine.Rendering.HighDefinition;

namespace UnityEditor.Rendering.HighDefinition
{
    [CanEditMultipleObjects]
    [VolumeComponentEditor(typeof(IndirectLightingController))]
    class IndirectLightingControllerEditor : VolumeComponentEditor
    {
        SerializedDataParameter m_ReflectionProbeIntensityMultiplier;
        SerializedDataParameter m_ReflectionLightingMultiplier;
        SerializedDataParameter m_ReflectionLightinglayersMask;
        SerializedDataParameter m_IndirectDiffuseLightingMultiplier;
        SerializedDataParameter m_IndirectDiffuseLightinglayersMask;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<IndirectLightingController>(serializedObject);

            m_ReflectionProbeIntensityMultiplier = Unpack(o.Find(x => x.reflectionProbeIntensityMultiplier));
            m_ReflectionLightingMultiplier = Unpack(o.Find(x => x.reflectionLightingMultiplier));
            m_ReflectionLightinglayersMask = Unpack(o.Find(x => x.reflectionLightinglayersMask));
            m_IndirectDiffuseLightingMultiplier = Unpack(o.Find(x => x.indirectDiffuseLightingMultiplier));
            m_IndirectDiffuseLightinglayersMask = Unpack(o.Find(x => x.indirectDiffuseLightinglayersMask));
        }

        public override void OnInspectorGUI()
        {
            PropertyField(m_ReflectionProbeIntensityMultiplier, EditorGUIUtility.TrTextContent("Indirect Diffuse Intensity", "Sets the multiplier for baked diffuse lighting."));
            PropertyField(m_ReflectionLightingMultiplier, EditorGUIUtility.TrTextContent("Indirect Specular Intensity", "Sets the multiplier for reflected specular lighting."));
            PropertyField(m_ReflectionLightinglayersMask, EditorGUIUtility.TrTextContent("Indirect Specular Intensity", "Sets the multiplier for reflected specular lighting."));
            PropertyField(m_IndirectDiffuseLightingMultiplier, EditorGUIUtility.TrTextContent("Indirect Specular Intensity", "Sets the multiplier for reflected specular lighting."));
            PropertyField(m_IndirectDiffuseLightinglayersMask, EditorGUIUtility.TrTextContent("Indirect Specular Intensity", "Sets the multiplier for reflected specular lighting."));
        }
    }
}
