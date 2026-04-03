using UnityEngine;

public enum HandMode { Neutral, Grab, Magnet, Modeling, Rotate }

/// <summary>
/// Singleton que centraliza o modo gestual de cada mão.
/// Consulta ObjectGrab, SummonObject e MagneticSnapping por mão.
/// Prioridade configurável via array no Inspector — a ordem define qual modo vence.
/// </summary>
public class HandModeManager : MonoBehaviour
{
    public static HandModeManager Instance { get; private set; }

    [Header("Prioridade de Modos (topo = maior prioridade)")]
    public HandMode[] modePriority = {
        HandMode.Modeling,
        HandMode.Rotate,
        HandMode.Grab,
        HandMode.Magnet,
        HandMode.Neutral
    };

    private ObjectGrab _grab;
    private SummonObject _magnet;
    private MagneticSnapping _modeling;
    private WristRotation _rotate;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        _grab = FindAnyObjectByType<ObjectGrab>();
        _magnet = FindAnyObjectByType<SummonObject>();
        _modeling = FindAnyObjectByType<MagneticSnapping>();
        _rotate = FindAnyObjectByType<WristRotation>();
    }

    public HandMode GetMode(bool isLeft)
    {
        foreach (HandMode mode in modePriority)
        {
            if (IsModeActiveForHand(mode, isLeft)) return mode;
        }
        return HandMode.Neutral;
    }

    private bool IsModeActiveForHand(HandMode mode, bool isLeft)
    {
        return mode switch
        {
            HandMode.Modeling => _modeling != null && _modeling.IsActiveForHand(isLeft),
            HandMode.Rotate   => _rotate != null && _rotate.IsActiveForHand(isLeft),
            HandMode.Grab     => _grab != null && _grab.IsActiveForHand(isLeft),
            HandMode.Magnet   => _magnet != null && _magnet.IsActiveForHand(isLeft),
            _                 => false
        };
    }
}
