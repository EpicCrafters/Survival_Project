using UnityEngine;
public enum ResourceType
{
    Tree,
    Bush,
    Rock
}
public interface IMinenable
{
    
    ResourceType GetResourceType();
}
