using UnityEngine;

public interface ISafeAreaFitter
{
    public Canvas MyCanvas { get; }
    public RectTransform MyRT { get;}
    public RectTransform Root { get; }

    public void InitSafeArea();
}
