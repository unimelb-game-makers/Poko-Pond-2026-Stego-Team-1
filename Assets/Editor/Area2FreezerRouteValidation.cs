using System;
using UnityEngine;

public static class Area2FreezerRouteValidation
{
    // Run without -quit; the Play Mode probe exits Unity on pass/failure.
    public static void RunBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Run in an isolated batch project, not the user's open editor.");
        Area2SceneBuilder.RepairCrusherRoomBatch();
        Area2FreezerRouteProbe.BeginTest();
    }
}
