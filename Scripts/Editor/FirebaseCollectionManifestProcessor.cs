#if FIREBASE_SDK && UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

// 릴리즈가 아닌 Android 빌드에서 Firebase 애널리틱스 수집을 막는다.
// Firebase는 코드에서 초기화하지 않아도 앱 시작 시 스스로 켜져 세션·화면 이벤트를 보내므로 매니페스트로 꺼야 한다.
public class FirebaseCollectionManifestProcessor : IPostGenerateGradleAndroidProject
{
    private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
    private const string DeactivatedKey = "firebase_analytics_collection_deactivated";

    public int callbackOrder => 0;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        if (BuildFlags.IsRelease) return;

        var manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
        var doc = new XmlDocument();
        doc.Load(manifestPath);

        var application = doc.SelectSingleNode("manifest/application");
        if (application == null)
        {
            Debug.LogError($"[FirebaseCollectionManifestProcessor] application 노드가 없어 애널리틱스 수집을 끄지 못했습니다: {manifestPath}");
            return;
        }

        var metaData = doc.CreateElement("meta-data");
        metaData.Attributes.Append(CreateAndroidAttribute(doc, "name", DeactivatedKey));
        metaData.Attributes.Append(CreateAndroidAttribute(doc, "value", "true"));
        application.AppendChild(metaData);
        doc.Save(manifestPath);

        Debug.Log("[FirebaseCollectionManifestProcessor] 릴리즈가 아닌 빌드라 Firebase 애널리틱스 수집을 끕니다");
    }

    private static XmlAttribute CreateAndroidAttribute(XmlDocument doc, string name, string value)
    {
        var attribute = doc.CreateAttribute("android", name, AndroidNamespace);
        attribute.Value = value;
        return attribute;
    }
}
#endif
