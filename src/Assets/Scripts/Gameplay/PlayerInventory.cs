using System;
using System.Collections.Generic;
using System.Linq;
using Runtime.Game;
using UnityEngine;

public class PlayerInventory : EventReceiver
{
    // Parts available to equip (not yet slotted)
    public List<Part> AvailableParts = new List<Part>();

    // Equipped parts have freeform positions inside the species circle.
    public readonly List<Part> EquippedParts = new List<Part>();

    // Player resource spent to remove equipped parts
    public int MutationPoints;

    public PlayerInventory()
    {
        Load();
        Runtime.Events.RequestGlobalSave.Subscribe(this, OnGlobalSaveRequested);
    }

    void OnGlobalSaveRequested(Runtime.Events.RequestGlobalSave _)
    {
        var save = GetSave();
        if (save == null) return;

        save.mutationPoints = MutationPoints;
        save.availablePartSchemaHashes ??= new List<int>();
        save.equippedPartSchemaHashes ??= new List<int>();
        save.equippedPartPositionX ??= new List<float>();
        save.equippedPartPositionY ??= new List<float>();
        save.availablePartSchemaHashes.Clear();
        foreach (var part in AvailableParts)
            save.availablePartSchemaHashes.Add(part?.Schema?.GetHashCode() ?? 0);

        save.equippedPartSchemaHashes.Clear();
        save.equippedPartPositionX.Clear();
        save.equippedPartPositionY.Clear();
        foreach (var part in EquippedParts)
        {
            save.equippedPartSchemaHashes.Add(part?.Schema?.GetHashCode() ?? 0);
            save.equippedPartPositionX.Add(part?.PlacementPosition.x ?? 0f);
            save.equippedPartPositionY.Add(part?.PlacementPosition.y ?? 0f);
        }

        save.pendingSave = true;
    }

    void Load()
    {
        var save = GetSave();
        if (save != null && save.ExistsOnDisk())
        {
            LoadSavedInventory(save);
        }

        AddCheatParts();
    }

    void LoadSavedInventory(Save.PlayerSave save)
    {
        MutationPoints = save.mutationPoints;
        AvailableParts.Clear();
        EquippedParts.Clear();
        foreach (var schemaHash in save.availablePartSchemaHashes ?? new List<int>())
        {
            var part = CreatePart(schemaHash);
            if (part != null)
                AvailableParts.Add(part);
        }

        var equippedHashes = save.equippedPartSchemaHashes ?? new List<int>();
        var positionX = save.equippedPartPositionX ?? new List<float>();
        var positionY = save.equippedPartPositionY ?? new List<float>();
        for (int i = 0; i < equippedHashes.Count; i++)
        {
            var part = CreatePart(equippedHashes[i]);
            if (part == null)
                continue;

            bool hasSavedPosition = i < positionX.Count && i < positionY.Count;
            Vector2 savedPosition = hasSavedPosition
                ? new Vector2(positionX[i], positionY[i])
                : Vector2.zero;
            if (hasSavedPosition
                && PartCirclePacking.IsValidPlacement(savedPosition, part.Radius, EquippedParts))
            {
                part.PlacementPosition = savedPosition;
                EquippedParts.Add(part);
            }
            else if (PartCirclePacking.TryFindPosition(part.Radius, EquippedParts, out Vector2 position))
            {
                part.PlacementPosition = position;
                EquippedParts.Add(part);
            }
            else
            {
                AvailableParts.Add(part);
            }
        }
    }

    void AddCheatParts()
    {
        var constants = Runtime.Game.GlobalConstantsHandler.Constants;
        if (constants == null || !constants.enableCheatParts || constants.CheatParts == null)
        {
            return;
        }

        foreach (var schemaGroup in constants.CheatParts.Where(schema => schema != null).GroupBy(schema => schema))
        {
            int configuredCount = schemaGroup.Count();
            int ownedCount = AvailableParts.Count(part => part?.Schema == schemaGroup.Key)
                + EquippedParts.Count(part => part?.Schema == schemaGroup.Key);
            for (int count = ownedCount; count < configuredCount; count++)
            {
                AvailableParts.Add(schemaGroup.Key.CreatePart());
            }
        }
    }

    static Part CreatePart(int schemaHash)
    {
        if (schemaHash == 0 || Schema.DataManager.Instance == null)
            return null;

        var schema = Schema.DataManager.Instance.FindAssetByHash(schemaHash) as PartSchema;
        return schema?.CreatePart();
    }

    static Save.PlayerSave GetSave()
    {
        var runtimeConstants = Runtime.Game.GlobalConstantsHandler.RuntimeConstants;
        if (runtimeConstants == null || Save.SaveManager.Instance == null)
            return null;

        return runtimeConstants.playerSave;
    }

    // -------------------------------------------------------------------------
    // Queries
    // -------------------------------------------------------------------------

    /// <summary>All currently equipped parts.</summary>
    public IEnumerable<Part> ActiveParts => EquippedParts;

    // -------------------------------------------------------------------------
    // Equipping
    // -------------------------------------------------------------------------

    /// <summary>
    /// Equips a part from the available pool at a valid species-circle position.
    /// </summary>
    public bool EquipPart(Part part, Vector2 position)
    {
        if (part == null) throw new ArgumentNullException(nameof(part));
        if (!AvailableParts.Contains(part))
            throw new InvalidOperationException($"Part '{part.Name}' is not in the available pool.");

        if (!PartCirclePacking.IsValidPlacement(position, part.Radius, EquippedParts))
            return false;

        part.PlacementPosition = position;
        EquippedParts.Add(part);
        AvailableParts.Remove(part);
        return true;
    }

    /// <summary>
    /// Compatibility overload for callers that previously selected a slot.
    /// Parts now use the first valid circle-packed position instead.
    /// </summary>
    public bool EquipPart(Part part, int slotIndex = -1)
    {
        if (part == null) throw new ArgumentNullException(nameof(part));
        if (slotIndex < -1)
            return false;

        if (!PartCirclePacking.TryFindPosition(part.Radius, EquippedParts, out Vector2 position))
            return false;

        return EquipPart(part, position);
    }

    public bool MoveEquippedPart(Part part, Vector2 position)
    {
        if (part == null || !EquippedParts.Contains(part))
            return false;

        if (!PartCirclePacking.IsValidPlacement(position, part.Radius, EquippedParts, part))
            return false;

        part.PlacementPosition = position;
        return true;
    }

    /// <summary>
    /// Removes an equipped part from the given slot and returns it to the
    /// available pool. Costs MutationCost mutation points.
    /// Returns true on success, false if the slot is empty or points are insufficient.
    /// </summary>
    public bool RemovePart(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= EquippedParts.Count) return false;

        var part = EquippedParts[slotIndex];
        if (part == null) return false;

        if (MutationPoints < part.MutationCost) return false;

        MutationPoints -= part.MutationCost;
        EquippedParts.RemoveAt(slotIndex);
        AvailableParts.Add(part);
        return true;
    }

    /// <summary>
    /// Removes an equipped part by reference regardless of which slot it occupies.
    /// </summary>
    public bool RemovePart(Part part)
    {
        if (part == null) throw new ArgumentNullException(nameof(part));

        int index = EquippedParts.IndexOf(part);
        return index >= 0 && RemovePart(index);
    }

    /// <summary>
    /// Swaps an equipped part out and replaces it with another from the available pool.
    /// The removed part is returned to the available pool and costs its MutationCost.
    /// </summary>
    public bool SwapPart(int slotIndex, Part incoming)
    {
        if (incoming == null) throw new ArgumentNullException(nameof(incoming));
        if (!AvailableParts.Contains(incoming))
            throw new InvalidOperationException($"Part '{incoming.Name}' is not in the available pool.");

        if (slotIndex < 0 || slotIndex >= EquippedParts.Count)
            return false;

        var outgoing = EquippedParts[slotIndex];
        if (MutationPoints < outgoing.MutationCost)
            return false;

        var otherParts = EquippedParts.Where((part, index) => index != slotIndex).ToList();
        Vector2 position = outgoing.PlacementPosition;
        if (!PartCirclePacking.IsValidPlacement(position, incoming.Radius, otherParts)
            && !PartCirclePacking.TryFindPosition(incoming.Radius, otherParts, out position))
        {
            return false;
        }

        MutationPoints -= outgoing.MutationCost;
        AvailableParts.Add(outgoing);
        EquippedParts[slotIndex] = incoming;
        incoming.PlacementPosition = position;
        AvailableParts.Remove(incoming);
        return true;
    }

    // -------------------------------------------------------------------------
    // Available pool management
    // -------------------------------------------------------------------------

    /// <summary>Adds a part directly to the available pool (e.g. found as loot).</summary>
    public void AddToPart(Part part)
    {
        if (part == null) throw new ArgumentNullException(nameof(part));
        AvailableParts.Add(part);
    }

    /// <summary>Discards a part from the available pool permanently.</summary>
    public bool DiscardAvailablePart(Part part)
    {
        return AvailableParts.Remove(part);
    }

    /// <summary>
    /// Adds a purchased part and spends mutation points as one operation.
    /// </summary>
    public bool TryPurchasePart(Part part, int cost)
    {
        if (part == null || cost < 0 || MutationPoints < cost)
            return false;

        MutationPoints -= cost;
        AvailableParts.Add(part);
        return true;
    }

    // -------------------------------------------------------------------------
    // Species sync
    // -------------------------------------------------------------------------

    /// <summary>
    /// Applies the currently equipped parts to a Species, replacing its part list.
    /// Call this before running a combat simulation.
    /// </summary>
    public void ApplyToSpecies(Species species)
    {
        species.Parts.Clear();

        species.Parts.Add(GlobalConstantsHandler.Constants.defaultAttack.CreatePart());
        species.Parts.Add(GlobalConstantsHandler.Constants.defaultDefend.CreatePart());
        species.Parts.Add(GlobalConstantsHandler.Constants.defaultForage.CreatePart());

        foreach (var part in ActiveParts)
            species.Parts.Add(part);
        species.RebuildConnectedParts();
    }
}
