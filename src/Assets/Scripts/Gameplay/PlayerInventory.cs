using System;
using System.Collections.Generic;
using System.Linq;

public class PlayerInventory
{
    public const int EquipSlots = 5;

    // Parts available to equip (not yet slotted)
    public List<Part> AvailableParts = new List<Part>();

    // Fixed-size equipped slots; null means the slot is empty
    public readonly Part[] EquippedParts = new Part[EquipSlots];

    // Player resource spent to remove equipped parts
    public int MutationPoints;

    // -------------------------------------------------------------------------
    // Queries
    // -------------------------------------------------------------------------

    /// <summary>Returns the index of the first empty equipped slot, or -1 if full.</summary>
    public int FirstEmptySlot()
    {
        for (int i = 0; i < EquipSlots; i++)
            if (EquippedParts[i] == null) return i;
        return -1;
    }

    /// <summary>Returns true if at least one equipped slot is empty.</summary>
    public bool HasEmptySlot => FirstEmptySlot() != -1;

    /// <summary>All currently equipped parts (non-null slots).</summary>
    public IEnumerable<Part> ActiveParts => EquippedParts.Where(p => p != null);

    // -------------------------------------------------------------------------
    // Equipping
    // -------------------------------------------------------------------------

    /// <summary>
    /// Equips a part from the available pool into the specified slot.
    /// If slotIndex is -1 the first empty slot is used.
    /// Returns true on success.
    /// </summary>
    public bool EquipPart(Part part, int slotIndex = -1)
    {
        if (part == null) throw new ArgumentNullException(nameof(part));
        if (!AvailableParts.Contains(part))
            throw new InvalidOperationException($"Part '{part.Name}' is not in the available pool.");

        if (slotIndex == -1)
            slotIndex = FirstEmptySlot();

        if (slotIndex < 0 || slotIndex >= EquipSlots)
            return false; // no room / invalid slot

        if (EquippedParts[slotIndex] != null)
            return false; // slot already occupied

        EquippedParts[slotIndex] = part;
        AvailableParts.Remove(part);
        return true;
    }

    /// <summary>
    /// Removes an equipped part from the given slot and returns it to the
    /// available pool. Costs MutationCost mutation points.
    /// Returns true on success, false if the slot is empty or points are insufficient.
    /// </summary>
    public bool RemovePart(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= EquipSlots) return false;

        var part = EquippedParts[slotIndex];
        if (part == null) return false;

        if (MutationPoints < part.MutationCost) return false;

        MutationPoints -= part.MutationCost;
        EquippedParts[slotIndex] = null;
        AvailableParts.Add(part);
        return true;
    }

    /// <summary>
    /// Removes an equipped part by reference regardless of which slot it occupies.
    /// </summary>
    public bool RemovePart(Part part)
    {
        if (part == null) throw new ArgumentNullException(nameof(part));

        for (int i = 0; i < EquipSlots; i++)
            if (EquippedParts[i] == part)
                return RemovePart(i);

        return false; // part wasn't equipped
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

        if (!RemovePart(slotIndex)) return false;  // frees the slot (costs mutation points)
        EquippedParts[slotIndex] = incoming;
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
        foreach (var part in ActiveParts)
            species.Parts.Add(part);
    }
}
