using UnityEngine;

public class GameCamera : MonoBehaviour
{
    [SerializeField] private Camera _mainCamera;

    private float _baseSize;
    public Camera MainCamera => _mainCamera;

    private void Awake()
    {
        _mainCamera.orthographicSize = ResolutionScreen.ORTHOGRAPHIC_SIZE;
        _baseSize = _mainCamera.orthographicSize;
        ResolutionScreen.Subscribe(ChangeResolution);
    }

    private void OnDestroy()
    {
        ResolutionScreen.Unsubscribe(ChangeResolution);
    }

    private void ChangeResolution(float width, float height, float scaleFactor)
    {
        float aspect = ResolutionScreen.REF_ASPECT / _mainCamera.aspect;
        _mainCamera.orthographicSize = ResolutionScreen.ORTHOGRAPHIC_SIZE  * aspect;
    }
}
