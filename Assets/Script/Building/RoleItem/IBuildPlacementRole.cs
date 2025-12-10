using UnityEngine;

public interface IBuildPlacementRole
{
    void ComputePreview(
        Vector3 hitPoint,
        float rotY,
        Transform anchor,
        BuildManager manager,
        ItemData item,
        out Vector3 outPos,
        out float outRotY,
        out bool canPlace
    );

    bool ValidatePlacement(
        Vector3 pos,
        float rotY,
        Transform anchor,
        ItemData item,
        BuildManager manager
    );

    void OnServerPlaced(
        GameObject spawnedObj,
        Transform anchor,
        ItemData item,
        BuildManager manager
    );
}
