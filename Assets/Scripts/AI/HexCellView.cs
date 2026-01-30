using UnityEngine;

public class HexCellView : MonoBehaviour
{
    public HexCell Data;
    public MeshRenderer Renderer;

    public void Initialize(HexCell data, float stepHeight)
    {
        Data = data;
        RefreshPosition(stepHeight);
    }

    public void RefreshPosition(float stepHeight)
    {
        transform.localPosition = HexMath.HexToWorld(Data.Q, Data.R, Data.Elevation, stepHeight);
        if (Data.Type == HexType.Ramp)
        {
            transform.localRotation = Quaternion.Euler(0, Data.RampDirection * 60f, 0);
        }
        else
        {
            transform.localRotation = Quaternion.identity;
        }
    }
}
