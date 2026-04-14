using System;
using System.Collections.Generic;

/// <summary>
/// Data passed to <see cref="CardPickerPanel"/> to configure a pick session.
/// The number of cards shown is determined by the length of <see cref="Choices"/>,
/// which should be either 3 or 5.
/// </summary>
public class CardPickerData
{
    /// <summary>Header text shown above the cards (e.g. "Choose a Reward").</summary>
    public string Title;

    /// <summary>Smaller sub-title text (e.g. "Pick 1 of 3").</summary>
    public string Subtitle;

    /// <summary>
    /// The parts to present. Supports any count; UI adapts its layout automatically.
    /// Typical values are 3 or 5.
    /// </summary>
    public IReadOnlyList<Part> Choices;

    /// <summary>
    /// Whether the player is allowed to skip without picking a card.
    /// When true a "Skip" button is shown.
    /// </summary>
    public bool AllowSkip;

    // ── Convenience factories ─────────────────────────────────────────────────

    /// <summary>Build a standard 3-choice reward screen.</summary>
    public static CardPickerData ThreeChoice(
        IReadOnlyList<Part> choices,
        string title = "CHOOSE A REWARD",
        bool allowSkip = false)
    {
        if (choices == null || choices.Count != 3)
            throw new ArgumentException("ThreeChoice requires exactly 3 parts.", nameof(choices));

        return new CardPickerData
        {
            Title = title,
            Subtitle = "Pick 1 of 3",
            Choices = choices,
            AllowSkip = allowSkip,
        };
    }

    /// <summary>Build a standard 5-choice draft screen.</summary>
    public static CardPickerData FiveChoice(
        IReadOnlyList<Part> choices,
        string title = "DRAFT A PART",
        bool allowSkip = false)
    {
        if (choices == null || choices.Count != 5)
            throw new ArgumentException("FiveChoice requires exactly 5 parts.", nameof(choices));

        return new CardPickerData
        {
            Title = title,
            Subtitle = "Pick 1 of 5",
            Choices = choices,
            AllowSkip = allowSkip,
        };
    }
}
