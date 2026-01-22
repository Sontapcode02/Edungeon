using UnityEngine;
using UnityEngine.Tilemaps;

public class SceneDebugger : MonoBehaviour
{
    void Start()
    {
        Debug.Log("🔍 --- SCENE DEBUGGER START ---");
        
        // 1. Check Camera
        Camera cam = Camera.main;
        if (cam != null)
        {
            Debug.Log($"📷 Camera found at {cam.transform.position}. Orthographic: {cam.orthographic}, Size: {cam.orthographicSize}, FarClip: {cam.farClipPlane}");
            Debug.Log($"📷 Culling Mask: {cam.cullingMask}");
        }
        else
        {
            Debug.LogError("❌ No Main Camera found!");
        }

        // 2. Check Grid
        Grid grid = FindObjectOfType<Grid>();
        if (grid != null)
        {
            Debug.Log($"🗺️ Grid found: '{grid.name}' at {grid.transform.position}. ActiveInHierarchy: {grid.gameObject.activeInHierarchy}");
            
            // Check Child Tilemaps
            Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>();
            if (maps.Length == 0) Debug.LogError("❌ Grid exists but NO Tilemap components found in children!");
            
            foreach (var map in maps)
            {
                var renderer = map.GetComponent<TilemapRenderer>();
                bool isVisible = renderer != null && renderer.enabled;
                int tileCount = map.GetUsedTilesCount();
                
                Debug.Log($"   🔸 Map '{map.name}': Active={map.gameObject.activeInHierarchy}, Renderer={isVisible}, TileCount={tileCount}, SortingLayer={renderer?.sortingLayerName}, Order={renderer?.sortingOrder}");
                
                // Color Warning
                if (map.color.a == 0) Debug.LogError($"❌ Map '{map.name}' has Alpha = 0 (Invisible)!");
            }
        }
        else
        {
            Debug.LogError("❌ No GRID object found in scene!");
        }

        // 3. Check Parent of Grid (in case it's inside Disabled object)
        if (grid != null && grid.transform.parent != null)
        {
            Debug.Log($"📂 Grid parent is '{grid.transform.parent.name}', Active: {grid.transform.parent.gameObject.activeInHierarchy}");
        }

        Debug.Log("🔍 --- SCENE DEBUGGER END ---");
    }
}
