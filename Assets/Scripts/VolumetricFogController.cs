// VolumetricFogController.cs
using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class VolumetricFogController : MonoBehaviour
{
    [Header("Fog Dimensions")]
    [Tooltip("Radius of the clear zone around the player — no fog here")]
    public float innerRadius = 5f;
    [Tooltip("Outer edge of the fog cylinder")]
    public float outerRadius = 20f;
    [Tooltip("Total height of the fog volume")]
    public float fogHeight = 8f;

    [Header("Fog Density")]
    [Range(0f, 1f)] public float density = 0.85f;
    [Tooltip("How many particles to maintain at any time")]
    public int particleCount = 120;

    [Header("Appearance")]
    public Texture2D spriteSheet;
    [Tooltip("Number of columns in the sprite sheet")]
    public int sheetColumns = 4;
    [Tooltip("Number of rows in the sprite sheet")]
    public int sheetRows = 4;
    public Color fogColor = new Color(0.75f, 0.8f, 0.85f, 1f);
    [Range(2f, 12f)] public float particleSize = 6f;
    [Range(0f, 1f)] public float sizeVariance = 0.4f;

    [Header("Motion")]
    public float driftSpeed = 0.3f;
    public float rotationalDrift = 5f;

    [Header("References")]
    public Transform player;
    public Material fogMaterial; // assign the FogParticle material

    private ParticleSystem _ps;
    private ParticleSystem.Particle[] _particles;
    private ParticleSystemRenderer _renderer;

    void Awake()
    {
        _ps = GetComponent<ParticleSystem>();
        _renderer = GetComponent<ParticleSystemRenderer>();
        _particles = new ParticleSystem.Particle[particleCount];

        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;

        ConfigureParticleSystem();
        SpawnInitialParticles();
    }

    void ConfigureParticleSystem()
    {
        // Stop the default emission — we drive particles manually
        var main = _ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.maxParticles = particleCount;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = Mathf.Infinity;
        main.startSpeed = 0f;

        var emission = _ps.emission;
        emission.enabled = false;

        // Sprite sheet animation
        var textureSheet = _ps.textureSheetAnimation;
        textureSheet.enabled = true;
        textureSheet.numTilesX = sheetColumns;
        textureSheet.numTilesY = sheetRows;
        textureSheet.animation = ParticleSystemAnimationType.WholeSheet;
        textureSheet.startFrame = new ParticleSystem.MinMaxCurve(
            0f, (float)(sheetColumns * sheetRows - 1) / (sheetColumns * sheetRows)
        );

        _renderer.material = fogMaterial;
        _renderer.renderMode = ParticleSystemRenderMode.Billboard;
        _renderer.sortMode = ParticleSystemSortMode.YoungestInFront;
    }

    void SpawnInitialParticles()
    {
        _ps.SetParticles(new ParticleSystem.Particle[0], 0);

        for (int i = 0; i < particleCount; i++)
        {
            _particles[i] = CreateParticle();
        }
        _ps.SetParticles(_particles, particleCount);
    }

    ParticleSystem.Particle CreateParticle()
    {
        var p = new ParticleSystem.Particle();

        Vector3 pos = RandomPositionInRing();
        p.position = pos;
        p.startSize = particleSize * Random.Range(1f - sizeVariance, 1f + sizeVariance);
        p.startColor = fogColor;
        p.remainingLifetime = float.MaxValue;
        p.startLifetime = float.MaxValue;

        // Encode drift velocity in the velocity field
        float angle = Random.Range(0f, Mathf.PI * 2f);
        p.velocity = new Vector3(
            Mathf.Cos(angle) * driftSpeed * Random.Range(0.5f, 1.5f),
            0f,
            Mathf.Sin(angle) * driftSpeed * Random.Range(0.5f, 1.5f)
        );

        // Random sprite frame via axisOfRotation trick — store frame in rotation
        p.rotation = 0f;
        p.randomSeed = (uint)Random.Range(0, int.MaxValue);

        return p;
    }

    Vector3 RandomPositionInRing()
    {
        // Uniform distribution in an annular ring
        float r = Mathf.Sqrt(Random.Range(
            innerRadius * innerRadius,
            outerRadius * outerRadius
        ));
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float height = Random.Range(-fogHeight * 0.5f, fogHeight * 0.5f);

        Vector3 origin = player != null ? player.position : Vector3.zero;
        return new Vector3(
            origin.x + r * Mathf.Cos(angle),
            origin.y + height,
            origin.z + r * Mathf.Sin(angle)
        );
    }

    void LateUpdate()
    {
        if (player == null) return;

        // Move the GameObject to follow the player (XZ only)
        Vector3 target = player.position;
        target.y = player.position.y; // keep vertical with player
        transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * 8f);

        int count = _ps.GetParticles(_particles);

        for (int i = 0; i < count; i++)
        {
            Vector3 worldPos = _particles[i].position;
            Vector3 toParticle = worldPos - player.position;
            toParticle.y = 0f;
            float dist = toParticle.magnitude;

            // Respawn particles that drift too far or inside the clear zone
            if (dist > outerRadius * 1.1f || dist < innerRadius * 0.9f ||
                Mathf.Abs(worldPos.y - player.position.y) > fogHeight * 0.6f)
            {
                _particles[i] = CreateParticle();
                continue;
            }

            // Apply slow drift + slight rotational curl
            float curl = rotationalDrift * Time.deltaTime * Mathf.Deg2Rad;
            Vector3 vel = _particles[i].velocity;
            float vx = vel.x * Mathf.Cos(curl) - vel.z * Mathf.Sin(curl);
            float vz = vel.x * Mathf.Sin(curl) + vel.z * Mathf.Cos(curl);
            _particles[i].velocity = new Vector3(vx, 0f, vz);
            _particles[i].position += _particles[i].velocity * Time.deltaTime;

            // Fade alpha at top/bottom vertical edges (soft cap)
            float verticalT = 1f - Mathf.Abs(
                (worldPos.y - player.position.y) / (fogHeight * 0.5f)
            );
            float verticalFade = Mathf.SmoothStep(0f, 1f, verticalT * 2f);

            // Fade alpha based on radial position (inner fade-in zone)
            // float radialT = Mathf.InverseLerp(innerRadius, innerRadius + (outerRadius - innerRadius) * 0.4f, dist);
            // float radialFade = Mathf.SmoothStep(0f, 1f, radialT);

            Color c = fogColor;
            // c.a = density * verticalFade * radialFade;
            c.a = density * verticalFade;
            _particles[i].startColor = c;
        }

        _ps.SetParticles(_particles, count);
    }

    // Draw gizmos so you can see the fog zone in Scene view
    void OnDrawGizmosSelected()
    {
        if (player == null) return;
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.3f);
        DrawWireCircle(player.position, innerRadius);
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.15f);
        DrawWireCircle(player.position, outerRadius);
    }

    void DrawWireCircle(Vector3 center, float radius)
    {
        int segments = 36;
        Vector3 prev = center + new Vector3(radius, 0, 0);
        for (int i = 1; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
