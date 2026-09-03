using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Overlay panel that presents a set of part cards and lets the player pick one.
/// The number of cards shown is data-driven via <see cref="CardPickerData"/> —
/// typically 3 (reward) or 5 (draft).
///
/// Usage:
///   var picker = CardPickerPanel.Show(uiDocument, data);
///   picker.OnPicked += part => { … };   // null when the player skips
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class CardPickerPanel : MonoBehaviour
{
    // ── Events ────────────────────────────────────────────────

    /// <summary>
    /// Fired when the player confirms a choice.
    /// The argument is the chosen <see cref="Part"/>, or <c>null</c> when skipped.
    /// </summary>
    public event Action<Part> OnPicked;

    // ── Private state ─────────────────────────────────────────
    UIDocument _doc;
    VisualElement _root;

    Label _titleLabel;
    Label _subtitleLabel;
    VisualElement _cardRow;
    Button _confirmBtn;
    Button _skipBtn;

    PickerCardElement _selectedCard;
    CardPickerData _data;

    // ── Unity lifecycle ───────────────────────────────────────

    void Awake()
    {
        _doc = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;

        _titleLabel = _root.Q<Label>("cpp-title");
        _subtitleLabel = _root.Q<Label>("cpp-subtitle");
        _cardRow = _root.Q<VisualElement>("cpp-card-row");
        _confirmBtn = _root.Q<Button>("cpp-confirm-btn");
        _skipBtn = _root.Q<Button>("cpp-skip-btn");

        _confirmBtn.clicked += OnConfirmClicked;
        _skipBtn.clicked += OnSkipClicked;

        Hide();
    }

    // ── Public API ────────────────────────────────────────────

    /// <summary>
    /// Populate and show the picker for <paramref name="data"/>.
    /// </summary>
    public void Show(CardPickerData data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _selectedCard = null;

        _titleLabel.text = data.Title ?? "CHOOSE A PART";
        _subtitleLabel.text = data.Subtitle ?? $"Pick 1 of {data.Choices?.Count ?? 0}";

        Utility.UI.EnableClass(!data.AllowSkip, _skipBtn, "cpp-skip-btn--hidden");
        Utility.UI.EnableClass(true, _confirmBtn, "cpp-confirm-btn--hidden");
        _confirmBtn?.SetEnabled(false);

        BuildCards(data.Choices);

        _root.style.display = DisplayStyle.Flex;
    }

    /// <summary>
    /// Hide the panel without triggering <see cref="OnPicked"/>.
    /// </summary>
    public void Hide()
    {
        _root.style.display = DisplayStyle.None;
    }

    // ── Card building ─────────────────────────────────────────

    void BuildCards(IReadOnlyList<Part> choices)
    {
        _cardRow.Clear();

        if (choices == null) return;

        foreach (var part in choices)
        {
            var card = new PickerCardElement(part);
            card.OnSelected += HandleCardSelected;
            _cardRow.Add(card);
        }
    }

    // ── Interaction ───────────────────────────────────────────

    void HandleCardSelected(PickerCardElement card)
    {
        // Deselect previous
        _selectedCard?.SetSelected(false);
        _selectedCard = card;
        card.SetSelected(true);

        Utility.UI.EnableClass(false, _confirmBtn, "cpp-confirm-btn--hidden");
        _confirmBtn?.SetEnabled(true);
    }

    void OnConfirmClicked()
    {
        if (_selectedCard == null)
            return;

        Confirm(_selectedCard.Part);
    }

    void OnSkipClicked()
    {
        Hide();
        OnPicked?.Invoke(null);
    }

    void Confirm(Part part)
    {
        Hide();
        OnPicked?.Invoke(part);
    }

}
