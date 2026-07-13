using UnityEngine;

public class SafeAreaFitter : MonoBehaviour, ISafeAreaFitter
{
    public Canvas MyCanvas { get; private set; }
    public RectTransform MyRT { get; private set; }
    public RectTransform Root => _root;
    [SerializeField] private RectTransform _root;


    public void InitSafeArea()
    {
        MyCanvas = GetComponent<Canvas>();
        MyRT = GetComponent<RectTransform>();

        if (_root == null)
            return;

        ResolutionScreen.Subscribe(ChangeResolution);
    }

    private void OnDestroy()
    {
        ResolutionScreen.Unsubscribe(ChangeResolution);
    }

    private void ChangeResolution(float width, float height, float scaleFactor)
    {
        Apply();
    }
    void Apply()
    {
        Rect safeArea = Screen.safeArea;
        Vector2 screenSize = new Vector2(Screen.width, Screen.height);

        // ÇÈ¼¿ ¡æ ¾ÞÄ¿ ºñÀ²·Î º¯È¯
        Vector2 anchorMin = safeArea.position / screenSize;
        Vector2 anchorMax = (safeArea.position + safeArea.size) / screenSize;

        _root.anchorMin = anchorMin;
        _root.anchorMax = anchorMax;
        _root.offsetMin = Vector2.zero;
        _root.offsetMax = Vector2.zero;
    }
}
