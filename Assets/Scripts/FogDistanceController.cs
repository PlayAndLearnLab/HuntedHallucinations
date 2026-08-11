using UnityEngine;

public class FogDistanceController : MonoBehaviour
{
    public Transform player;
    public ParticleSystem fogParticles;

    [Header("Distance & Clearance")]
    public float minClearRadius = 5f;
    
    [Header("Height Scaling")]
    public float baseGroundHeight = 0f;
    public float heightScalingFactor = 0.3f;

    void Start()
    {
        if (fogParticles == null)
            fogParticles = GetComponent<ParticleSystem>();
    }

    void Update()
    {
        if (player == null)
        {
            FindPlayer();
            if (player == null) return;
        }

        // Keep the fog emitter anchored to the player's ground position
        Vector3 targetPosition = new Vector3(player.position.x, baseGroundHeight, player.position.z);
        transform.position = targetPosition;

        // Fix: Use a static height or base it strictly on player position, NOT camera position
        var main = fogParticles.main;
        
        // Set a stable default height or scale it relative to fixed world bounds
        main.startSizeY = Mathf.Max(1f, 10f * heightScalingFactor); 
    }

    private void FindPlayer()
    {
        // Option A: Search by Tag (Ensure your Player Prefab has the "Player" tag)
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
    }
}