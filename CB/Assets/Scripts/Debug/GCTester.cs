using System;
using Unity.Profiling;
using UnityEngine;

public class GCTester : MonoBehaviour
{

    private static readonly ProfilerMarker _marker =
        new ProfilerMarker("EnumConverter.Test");
    public void OnClickConvert()
    {
        GC.Collect();
        long before = GC.GetTotalMemory(true);
        //using (_marker.Auto())
        {
            for (int i = 0; i < 10000; i++)
            {
               //var go = EnumConverter.Enum32ToInt(
               //     EnumConverter.IntToEnum32<BrickType>(
               //         Utility.RandomInt(
               //             EnumConverter.Enum32ToInt(BrickType.MAX))));
               var go = ((int)((BrickType)(Utility.RandomInt((int)BrickType.MAX))));
               //var go = ((int)((BrickType)((int)BrickType.MAX)));
            }

        }
        long after = GC.GetTotalMemory(false);
        Debug.LogError($"�Ҵ緮: {after - before} bytes");
    }
}
