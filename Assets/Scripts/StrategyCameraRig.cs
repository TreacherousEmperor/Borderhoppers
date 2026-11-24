using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Strategická kamera. Skript hýbe pivotem (tento GameObject) a nastavuje pozici/rotaci
/// Cinemachine kamery, která je jeho potomek. Cinemachine kamera nesmí mít
/// žádný Follow/Aim (Body i Aim = Do Nothing / Position a Rotation Control = None).
///
/// WASD / šipky = pohyb, Q / E = otáčení, kolečko = zoom,
/// prostřední tlačítko myši = tažení mapy, okraj obrazovky = posun (volitelné).
/// </summary>
public class StrategyCameraRig : MonoBehaviour
{
    public WorldView worldView;
    public Transform cameraHolder;               // objekt Cinemachine kamery (potomek tohoto objektu)

    [Header("Pohyb")]
    public float moveSpeed = 40f;                // rychlost při střední vzdálenosti
    public float dragSpeed = 0.0025f;            // citlivost tažení myší
    public bool edgeScroll = false;
    public float edgeSize = 12f;                 // šířka okraje v pixelech

    [Header("Otáčení a zoom")]
    public float rotateSpeed = 90f;
    public float zoomStep = 0.12f;               // o kolik % se změní vzdálenost jedním kliknutím kolečka
    public float minDistance = 8f;
    public float maxDistance = 120f;
    public float pitchNear = 35f;                // úhel pohledu při blízkém zoomu
    public float pitchFar = 70f;                 // úhel při oddáleném zoomu

    [Header("Plynulost")]
    public float smoothing = 10f;

    Vector3 pos, targetPos;
    float yaw, targetYaw;
    float dist, targetDist;
    bool initialized;

    void LateUpdate()
    {
        if (worldView == null || worldView.World == null || cameraHolder == null) return;

        if (!initialized) Init();

        float dt = Time.unscaledDeltaTime;
        ReadInput(dt);
        ClampToMap();

        // plynulé dotažení k cílovým hodnotám
        float k = 1f - Mathf.Exp(-smoothing * dt);
        pos  = Vector3.Lerp(pos, targetPos, k);
        yaw  = Mathf.LerpAngle(yaw, targetYaw, k);
        dist = Mathf.Lerp(dist, targetDist, k);

        float pitch = Mathf.Lerp(pitchNear, pitchFar, Mathf.InverseLerp(minDistance, maxDistance, dist));
        transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        Quaternion tilt = Quaternion.Euler(pitch, 0f, 0f);
        cameraHolder.localRotation = tilt;
        cameraHolder.localPosition = tilt * new Vector3(0f, 0f, -dist);
    }

    void Init()
    {
        var starts = worldView.World.startPositions;
        Vector3 focus = starts.Count > 0
            ? worldView.CellPos(starts[0])
            : worldView.transform.position + new Vector3(worldView.World.width * 0.5f, 0f, worldView.World.height * 0.5f);

        pos = targetPos = new Vector3(focus.x, 0f, focus.z);
        yaw = targetYaw = 0f;
        dist = targetDist = Mathf.Lerp(minDistance, maxDistance, 0.3f);
        initialized = true;
    }

    void ReadInput(float dt)
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;

        Quaternion yawRot = Quaternion.Euler(0f, targetYaw, 0f);
        Vector3 right = yawRot * Vector3.right, forward = yawRot * Vector3.forward;
        Vector3 move = Vector3.zero;

        if (kb != null)
        {
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move += right;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  move -= right;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    move += forward;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  move -= forward;
            if (kb.eKey.isPressed) targetYaw += rotateSpeed * dt;
            if (kb.qKey.isPressed) targetYaw -= rotateSpeed * dt;
        }

        if (mouse != null)
        {
            // zoom kolečkem
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                targetDist = Mathf.Clamp(targetDist * (1f - Mathf.Sign(scroll) * zoomStep), minDistance, maxDistance);

            // tažení mapy prostředním tlačítkem
            if (mouse.middleButton.isPressed)
            {
                Vector2 d = mouse.delta.ReadValue();
                targetPos -= (right * d.x + forward * d.y) * dist * dragSpeed;
            }

            // posun u okraje obrazovky
            if (edgeScroll && Application.isFocused)
            {
                Vector2 m = mouse.position.ReadValue();
                if (m.x <= edgeSize) move -= right;
                if (m.x >= Screen.width - edgeSize) move += right;
                if (m.y <= edgeSize) move -= forward;
                if (m.y >= Screen.height - edgeSize) move += forward;
            }
        }

        // při větším oddálení se hýbe rychleji
        float speed = moveSpeed * (dist / Mathf.Lerp(minDistance, maxDistance, 0.3f));
        if (move.sqrMagnitude > 0f) targetPos += move.normalized * speed * dt;
    }

    void ClampToMap()
    {
        var w = worldView.World;
        Vector3 o = worldView.transform.position;
        targetPos.x = Mathf.Clamp(targetPos.x, o.x, o.x + w.width);
        targetPos.z = Mathf.Clamp(targetPos.z, o.z, o.z + w.height);
        targetPos.y = 0f;
    }

    /// <summary>Přesune kameru na místo ve světě (např. na vybranou jednotku).</summary>
    public void FocusOn(Vector3 worldPos)
    {
        targetPos = new Vector3(worldPos.x, 0f, worldPos.z);
    }
}
