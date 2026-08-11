using UnityEngine;

public class PlayerFogFollower : MonoBehaviour
{
    public Material fogMaterial;
    public Transform playerTransform;

    private static readonly int PlayerPosID = Shader.PropertyToID("_PlayerPosition");

    void Update()
    {
        if (playerTransform == null)
        {
            // Auto-assign player at runtime if instantiated
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
            return;
        }

        // Center fog mesh over player
        Vector3 fogPos = playerTransform.position;
        transform.position = new Vector3(fogPos.x, transform.position.y, fogPos.z);

        // Update shader center
        fogMaterial.SetVector(PlayerPosID, playerTransform.position);
    }
}
