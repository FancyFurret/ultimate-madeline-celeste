using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Entities;

/// <summary>
/// Manages all UmcBerry entities during gameplay.
/// Handles berry pickup, drop on death, and collection at goal.
/// </summary>
public class BerryManager
{
    public static BerryManager Instance { get; private set; }

    private readonly List<UmcBerry> _berries = new();
    private Level _level;

    public BerryManager(Level level)
    {
        Instance = this;
        _level = level;

        // Subscribe to goal events
        GoalFlag.OnPlayerReachedGoal += HandlePlayerReachedGoal;
        UmcBerry.OnBerryCollected += HandleBerryCollected;

        // Find all existing UmcBerry entities in the level
        RefreshBerryList();

        UmcLogger.Info($"BerryManager initialized with {_berries.Count} berries");
    }

    /// <summary>
    /// Refreshes the list of berries from the current scene.
    /// </summary>
    public void RefreshBerryList()
    {
        _berries.Clear();
        if (_level == null) return;

        foreach (var entity in _level.Tracker.GetEntities<UmcBerry>())
        {
            if (entity is UmcBerry berry)
            {
                _berries.Add(berry);
            }
        }
    }

    /// <summary>
    /// Registers a newly spawned berry.
    /// </summary>
    public void RegisterBerry(UmcBerry berry)
    {
        if (!_berries.Contains(berry))
        {
            _berries.Add(berry);
            UmcLogger.Info($"Registered berry at {berry.SpawnPosition}");
        }
    }

    /// <summary>
    /// Called when a player reaches the goal. Collects their carried berry.
    /// </summary>
    private void HandlePlayerReachedGoal(UmcPlayer player)
    {
        foreach (var berry in _berries)
        {
            if (berry.IsCollected) continue;
            if (berry.Carrier != player) continue;

            berry.Collect(player);
        }
    }

    /// <summary>
    /// Called when a berry is collected. Records the score.
    /// </summary>
    private void HandleBerryCollected(UmcBerry berry, UmcPlayer player)
    {
        RoundState.Current?.RecordBerryCollected(player);
    }

    /// <summary>
    /// Cleans up the manager.
    /// </summary>
    public void Cleanup()
    {
        GoalFlag.OnPlayerReachedGoal -= HandlePlayerReachedGoal;
        UmcBerry.OnBerryCollected -= HandleBerryCollected;
        UmcBerry.ClearHandlers();
        _berries.Clear();

        if (Instance == this)
            Instance = null;

        UmcLogger.Info("BerryManager cleaned up");
    }
}

