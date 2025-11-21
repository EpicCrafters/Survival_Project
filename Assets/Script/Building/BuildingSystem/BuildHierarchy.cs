using UnityEngine;

public static class BuildHierarchy
{
    private static Transform _root;

    public static Transform Root
    {
        get
        {
            if (_root == null)
            {
                var existing = GameObject.Find("BuildRoot");
                if (existing != null) _root = existing.transform;
                else
                {
                    GameObject go = new GameObject("BuildRoot");
                    _root = go.transform;
                }
            }
            return _root;
        }
    }
}
