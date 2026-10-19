using UnityEngine;

// Demo shim for Goal Poacher's per-device quality settings (Meta Quest vs PC).
// With no instance in the scene GrassSystem uses its default path.
public class QuestQualityManager : MonoBehaviour
{
    public static QuestQualityManager instance;
    public static int use3dGrass = 1;

    public bool isTrailerCinematic;
}
