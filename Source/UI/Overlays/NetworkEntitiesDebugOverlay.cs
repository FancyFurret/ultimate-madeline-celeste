using System.Linq;
using Celeste.Mod.UltimateMadelineCeleste.Network;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.UI.Overlays;

/// <summary>
/// Debug overlay that displays all registered networked entities.
/// Toggle with the debug setting in mod options.
/// </summary>
public class NetworkEntitiesDebugOverlay : Entity
{
    private float _scrollOffset;
    private const float ScrollSpeed = 200f;
    private const float MaxVisibleHeight = 800f;

    public NetworkEntitiesDebugOverlay()
    {
        Tag = Tags.HUD | Tags.Global | Tags.PauseUpdate | Tags.TransitionUpdate;
        Depth = -10002;
    }

    public static void Load()
    {
        On.Celeste.Level.LoadLevel += OnLevelLoad;
    }

    public static void Unload()
    {
        On.Celeste.Level.LoadLevel -= OnLevelLoad;
    }

    private static void OnLevelLoad(On.Celeste.Level.orig_LoadLevel orig, Level self, Player.IntroTypes playerIntro, bool isFromLoader)
    {
        orig(self, playerIntro, isFromLoader);

        var existing = self.Entities.FindFirst<NetworkEntitiesDebugOverlay>();
        if (existing == null)
        {
            self.Add(new NetworkEntitiesDebugOverlay());
        }
    }

    public override void Update()
    {
        base.Update();

        // Allow scrolling with mouse wheel or up/down keys when debug is shown
        if (MInput.Keyboard.Check(Microsoft.Xna.Framework.Input.Keys.PageUp))
        {
            _scrollOffset -= ScrollSpeed * Engine.DeltaTime;
        }
        if (MInput.Keyboard.Check(Microsoft.Xna.Framework.Input.Keys.PageDown))
        {
            _scrollOffset += ScrollSpeed * Engine.DeltaTime;
        }

        _scrollOffset = MathHelper.Clamp(_scrollOffset, 0, float.MaxValue);
    }

    public override void Render()
    {
        base.Render();

        var registry = NetworkedEntityRegistry.Instance;
        if (registry == null)
        {
            DrawNoRegistry();
            return;
        }

        DrawEntityList(registry);
    }

    private void DrawNoRegistry()
    {
        const float padding = 15f;
        Draw.Rect(padding - 5, padding - 5, 200, 30, Color.Black * 0.8f);
        ActiveFont.Draw(
            "Network Debug: No Registry",
            new Vector2(padding, padding),
            Vector2.Zero,
            Vector2.One * 0.35f,
            Color.Orange
        );
    }

    private void DrawEntityList(NetworkedEntityRegistry registry)
    {
        const float padding = 15f;
        const float lineHeight = 22f;
        const float headerHeight = 28f;
        const float columnPadding = 12f;

        // Column widths
        const float colType = 160f;
        const float colNetId = 90f;
        const float colOwner = 100f;
        const float colLocal = 50f;
        const float colPosition = 140f;
        const float colScene = 120f;

        var totalWidth = colType + colNetId + colOwner + colLocal + colPosition + colScene + (columnPadding * 5);

        // Get entities via reflection since _entities is private
        var entitiesField = typeof(NetworkedEntityRegistry).GetField("_entities",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var entities = entitiesField?.GetValue(registry) as System.Collections.Generic.Dictionary<uint, NetworkedEntity>;

        var entityCount = entities?.Count ?? 0;
        var contentHeight = (entityCount * lineHeight) + headerHeight + 40f;
        var visibleHeight = System.Math.Min(contentHeight, MaxVisibleHeight);

        // Clamp scroll offset
        var maxScroll = System.Math.Max(0, contentHeight - MaxVisibleHeight);
        _scrollOffset = MathHelper.Clamp(_scrollOffset, 0, maxScroll);

        // Draw background
        Draw.Rect(padding - 8, padding - 8, totalWidth + 16, visibleHeight + 16, Color.Black * 0.85f);

        // Draw header
        var x = padding;
        var y = padding - _scrollOffset;

        // Title
        var isOnline = NetworkManager.Instance?.IsOnline ?? false;
        var localId = NetworkManager.Instance?.LocalClientId ?? 0;
        var statusColor = isOnline ? Color.LightGreen : Color.Gray;
        var statusText = isOnline ? "● Online" : "○ Offline";

        ActiveFont.Draw(
            $"Network Entities Debug [{statusText}] - {entityCount} entities - Local ID: {localId:X}",
            new Vector2(x, y),
            Vector2.Zero,
            Vector2.One * 0.4f,
            statusColor
        );

        y += headerHeight;

        // Column headers
        if (y + lineHeight > padding && y < padding + visibleHeight)
        {
            var headerColor = Color.Cyan * 0.9f;
            var hx = x;

            ActiveFont.Draw("Type", new Vector2(hx, y), Vector2.Zero, Vector2.One * 0.32f, headerColor);
            hx += colType;
            ActiveFont.Draw("Net ID", new Vector2(hx, y), Vector2.Zero, Vector2.One * 0.32f, headerColor);
            hx += colNetId;
            ActiveFont.Draw("Owner", new Vector2(hx, y), Vector2.Zero, Vector2.One * 0.32f, headerColor);
            hx += colOwner;
            ActiveFont.Draw("Local", new Vector2(hx, y), Vector2.Zero, Vector2.One * 0.32f, headerColor);
            hx += colLocal;
            ActiveFont.Draw("Position", new Vector2(hx, y), Vector2.Zero, Vector2.One * 0.32f, headerColor);
            hx += colPosition;
            ActiveFont.Draw("Scene", new Vector2(hx, y), Vector2.Zero, Vector2.One * 0.32f, headerColor);

            // Draw separator line
            Draw.Line(x, y + lineHeight - 4, x + totalWidth, y + lineHeight - 4, Color.Cyan * 0.4f);
        }

        y += lineHeight;

        if (entities == null || entityCount == 0)
        {
            if (y > padding && y < padding + visibleHeight)
            {
                ActiveFont.Draw(
                    "No networked entities registered",
                    new Vector2(x, y),
                    Vector2.Zero,
                    Vector2.One * 0.32f,
                    Color.Gray
                );
            }
            return;
        }

        // Draw each entity
        var index = 0;
        foreach (var kvp in entities.OrderBy(e => e.Key))
        {
            if (y + lineHeight < padding)
            {
                y += lineHeight;
                index++;
                continue;
            }

            if (y > padding + visibleHeight)
                break;

            var networkId = kvp.Key;
            var netEntity = kvp.Value;
            var entity = netEntity.Entity;

            // Alternate row colors
            var rowColor = index % 2 == 0 ? Color.White * 0.02f : Color.Transparent;
            Draw.Rect(x - 4, y - 2, totalWidth + 8, lineHeight, rowColor);

            var hx = x;
            var textColor = netEntity.IsOwner ? Calc.HexToColor("90EE90") : Color.White * 0.85f;

            // Type name
            var typeName = entity?.GetType().Name ?? "null";
            if (typeName.Length > 18) typeName = typeName.Substring(0, 16) + "..";
            ActiveFont.Draw(typeName, new Vector2(hx, y), Vector2.Zero, Vector2.One * 0.3f, textColor);
            hx += colType;

            // Network ID (hex)
            ActiveFont.Draw($"{networkId:X8}", new Vector2(hx, y), Vector2.Zero, Vector2.One * 0.3f, textColor);
            hx += colNetId;

            // Owner ID (shortened)
            var ownerShort = $"{netEntity.OwnerId & 0xFFFF:X4}";
            ActiveFont.Draw(ownerShort, new Vector2(hx, y), Vector2.Zero, Vector2.One * 0.3f, textColor);
            hx += colOwner;

            // Is Local (owner)
            var localText = netEntity.IsOwner ? "YES" : "no";
            var localColor = netEntity.IsOwner ? Color.LightGreen : Color.Gray;
            ActiveFont.Draw(localText, new Vector2(hx, y), Vector2.Zero, Vector2.One * 0.3f, localColor);
            hx += colLocal;

            // Position
            var pos = entity?.Position ?? Vector2.Zero;
            var posText = $"{pos.X:F0}, {pos.Y:F0}";
            ActiveFont.Draw(posText, new Vector2(hx, y), Vector2.Zero, Vector2.One * 0.3f, textColor);
            hx += colPosition;

            // Scene info
            var sceneInfo = "none";
            if (entity?.Scene != null)
            {
                if (entity.Scene is Level level)
                {
                    sceneInfo = level.Session?.Level ?? "Level";
                }
                else
                {
                    sceneInfo = entity.Scene.GetType().Name;
                }
            }
            if (sceneInfo.Length > 14) sceneInfo = sceneInfo.Substring(0, 12) + "..";
            ActiveFont.Draw(sceneInfo, new Vector2(hx, y), Vector2.Zero, Vector2.One * 0.3f, textColor);

            y += lineHeight;
            index++;
        }

        // Scroll indicator if content is scrollable
        if (maxScroll > 0)
        {
            var scrollPercent = _scrollOffset / maxScroll;
            var scrollBarHeight = (visibleHeight / contentHeight) * visibleHeight;
            var scrollBarY = padding + (scrollPercent * (visibleHeight - scrollBarHeight));

            Draw.Rect(padding + totalWidth + 4, padding, 4, visibleHeight, Color.Gray * 0.3f);
            Draw.Rect(padding + totalWidth + 4, scrollBarY, 4, scrollBarHeight, Color.White * 0.6f);

            // Hint text
            ActiveFont.Draw(
                "PgUp/PgDn to scroll",
                new Vector2(padding, padding + visibleHeight + 4),
                Vector2.Zero,
                Vector2.One * 0.25f,
                Color.Gray * 0.7f
            );
        }
    }
}

