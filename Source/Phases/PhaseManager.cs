using Celeste.Mod.UltimateMadelineCeleste.Network;
using Celeste.Mod.UltimateMadelineCeleste.Phases.Hub;
using Celeste.Mod.UltimateMadelineCeleste.Phases.Playing;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Phases;

/// <summary>
/// Manages game phase transitions and ensures the correct phase entity is active.
/// </summary>
public class PhaseManager
{
    public static PhaseManager Instance { get; private set; }

    private const string HubStageId = "FancyFurret/UltimateMadelineCeleste/Hub";

    /// <summary>
    /// The map SID to load when transitioning to Playing phase.
    /// Set before the level transition occurs.
    /// </summary>
    public string PendingLevelSID { get; set; }

    public PhaseManager()
    {
        Instance = this;
    }

    public void Load()
    {
        On.Celeste.Level.LoadLevel += OnLevelLoad;
    }

    public void Unload()
    {
        On.Celeste.Level.LoadLevel -= OnLevelLoad;
    }

    private void OnLevelLoad(On.Celeste.Level.orig_LoadLevel orig, Level level, Player.IntroTypes playerIntro, bool isFromLoader)
    {
        var sid = level.Session.Area.SID;
        var isUmcLevel = sid.Contains("UltimateMadelineCeleste");
        var isHub = sid == HubStageId;

        // Not a UMC level - skip our logic
        if (!isUmcLevel)
        {
            orig(level, playerIntro, isFromLoader);
            return;
        }

        // Start a session if entering any UMC level without one
        if (!GameSession.Started)
        {
            NetworkManager.Instance?.StartLocalSession();
        }

        var session = GameSession.Instance;
        if (session == null)
        {
            orig(level, playerIntro, isFromLoader);
            return;
        }

        // Determine which phase we should be in based on level and session state
        if (isHub)
        {
            // Entering hub - ensure we're in Lobby phase
            EnsurePhase<HubPhase>(level, GamePhase.Lobby);
        }
        else if (session.Phase == GamePhase.Playing)
        {
            // Entering a gameplay level while in Playing phase
            var levelSID = PendingLevelSID ?? sid;
            PendingLevelSID = null;
            EnsurePhase<PlayingPhase>(level, GamePhase.Playing, levelSID);
        }
        else
        {
            // Loading a non-hub UMC level but not in Playing phase - redirect to hub
            UmcLogger.Info($"Entered UMC level '{sid}' outside Playing phase - redirecting to hub");
            session.SetPhase(GamePhase.Lobby);

            // Schedule redirect after this frame completes
            level.OnEndOfFrame += () =>
            {
                LevelEnter.Go(new global::Celeste.Session(AreaData.Get(HubStageId).ToKey()), false);
            };

            // Still call orig to let the level load (avoids crashes), redirect happens after
            orig(level, playerIntro, isFromLoader);
            return;
        }

        orig(level, playerIntro, isFromLoader);
    }

    private void EnsurePhase<T>(Level level, GamePhase expectedPhase, string levelSID = null) where T : Entity
    {
        var existing = level.Entities.FindFirst<T>();
        if (existing != null)
            return;

        Entity phase = typeof(T) switch
        {
            var t when t == typeof(HubPhase) => new HubPhase(),
            var t when t == typeof(PlayingPhase) => new PlayingPhase(levelSID ?? level.Session.Area.SID),
            _ => null
        };

        if (phase != null)
        {
            level.Add(phase);
            UmcLogger.Info($"PhaseManager: Added {typeof(T).Name} to level");
        }
    }

    /// <summary>
    /// Transitions to a new level with the Playing phase.
    /// </summary>
    public void TransitionToLevel(string mapSID)
    {
        PendingLevelSID = mapSID;
        GameSession.Instance?.SetPhase(GamePhase.Playing);

        var areaData = AreaData.Get(mapSID);
        if (areaData == null)
        {
            UmcLogger.Error($"Failed to find area data for: {mapSID}");
            PendingLevelSID = null;
            return;
        }

        var level = Engine.Scene as Level;
        if (level != null)
        {
            level.OnEndOfFrame += () =>
            {
                LevelEnter.Go(new global::Celeste.Session(areaData.ToKey()), false);
            };
        }
    }

    /// <summary>
    /// Transitions back to the hub with the Lobby phase.
    /// </summary>
    public void TransitionToLobby()
    {
        GameSession.Instance?.SetPhase(GamePhase.Lobby);

        var level = Engine.Scene as Level;
        if (level != null)
        {
            level.OnEndOfFrame += () =>
            {
                LevelEnter.Go(new global::Celeste.Session(AreaData.Get(HubStageId).ToKey()), false);
            };
        }
    }
}
