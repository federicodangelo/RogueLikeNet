namespace RogueLikeNet.Client.Core.Rendering;

/// <summary>
/// A vertical layout system for the side HUD panel.
/// Sections are anchored to top or bottom. Fixed-height sections take their declared rows,
/// variable-height sections share the remaining space. Sections can be scrollable and can
/// accept input focus (cycled with TAB).
/// </summary>
public class HudLayout
{
    private readonly List<HudSection> _sections = new();
    private int _focusedSectionIndex = -1;

    public IReadOnlyList<HudSection> Sections => _sections;
    public int FocusedSectionIndex => _focusedSectionIndex;
    public HudSection? FocusedSection => _focusedSectionIndex >= 0 && _focusedSectionIndex < _sections.Count
        ? _sections[_focusedSectionIndex] : null;

    public void AddSection(HudSection section) => _sections.Add(section);

    /// <summary>
    /// Compute the StartRow and RowCount for each section given the available rows.
    /// Top-anchored sections fill from the top, bottom-anchored from the bottom.
    /// Variable sections split remaining space.
    /// </summary>
    public void ComputeLayout(int availableRows)
    {
        // Pass 1: sum fixed top/ bottom rows, count variable sections
        int fixedTopRows = 0;
        int fixedBottomRows = 0;
        int variableCount = 0;

        foreach (var s in _sections)
        {
            if (s.IsFixedHeight)
            {
                if (s.Anchor == HudAnchor.Bottom) fixedBottomRows += s.FixedHeight;
                else fixedTopRows += s.FixedHeight;
            }
            else
            {
                variableCount++;
            }
        }

        int remainingRows = Math.Max(0, availableRows - fixedTopRows - fixedBottomRows);
        int variableEach = variableCount > 0 ? remainingRows / variableCount : 0;
        int variableExtra = variableCount > 0 ? remainingRows % variableCount : 0;

        // Pass 2: assign positions top-down for top-anchored, bottom-up for bottom-anchored
        int topCursor = 0;
        int bottomCursor = availableRows;

        // First assign all top-anchored sections (in order)
        int varIndex = 0;
        foreach (var s in _sections)
        {
            if (s.Anchor == HudAnchor.Bottom) continue;

            s.StartRow = topCursor;
            if (s.IsFixedHeight)
            {
                s.RowCount = s.FixedHeight;
            }
            else
            {
                s.RowCount = variableEach + (varIndex < variableExtra ? 1 : 0);
                varIndex++;
            }
            topCursor += s.RowCount;
        }

        // Then assign bottom-anchored sections (in reverse order so last-added bottom is at the very bottom)
        for (int i = _sections.Count - 1; i >= 0; i--)
        {
            var s = _sections[i];
            if (s.Anchor != HudAnchor.Bottom) continue;

            if (s.IsFixedHeight)
            {
                s.RowCount = s.FixedHeight;
            }
            else
            {
                s.RowCount = variableEach + (varIndex < variableExtra ? 1 : 0);
                varIndex++;
            }
            bottomCursor -= s.RowCount;
            s.StartRow = bottomCursor;
        }
    }

    /// <summary>Cycle focus to the next section that accepts input.</summary>
    public void CycleFocus()
    {
        var inputSections = new List<int>();
        for (int i = 0; i < _sections.Count; i++)
        {
            if (_sections[i].AcceptsInput) inputSections.Add(i);
        }

        if (inputSections.Count == 0)
        {
            _focusedSectionIndex = -1;
            return;
        }

        int current = inputSections.IndexOf(_focusedSectionIndex);
        int next = (current + 1) % inputSections.Count;
        _focusedSectionIndex = inputSections[next];
    }

    /// <summary>
    /// Shift focus to the next AcceptsInput section in top-to-bottom order,
    /// wrapping to the first if already at the last (or if there is only one).
    /// Returns the newly focused section, or null if no input sections exist.
    /// </summary>
    public HudSection? FocusNextInputSection()
    {
        var inputIndices = GetInputSectionIndices();
        if (inputIndices.Count == 0) return null;
        int cur = inputIndices.IndexOf(_focusedSectionIndex);
        _focusedSectionIndex = inputIndices[(cur + 1) % inputIndices.Count];
        return _sections[_focusedSectionIndex];
    }

    /// <summary>
    /// Shift focus to the previous AcceptsInput section in top-to-bottom order,
    /// wrapping to the last if already at the first (or if there is only one).
    /// Returns the newly focused section, or null if no input sections exist.
    /// </summary>
    public HudSection? FocusPreviousInputSection()
    {
        var inputIndices = GetInputSectionIndices();
        if (inputIndices.Count == 0) return null;
        int cur = inputIndices.IndexOf(_focusedSectionIndex);
        _focusedSectionIndex = inputIndices[(cur - 1 + inputIndices.Count) % inputIndices.Count];
        return _sections[_focusedSectionIndex];
    }

    private List<int> GetInputSectionIndices()
    {
        var list = new List<int>();
        for (int i = 0; i < _sections.Count; i++)
            if (_sections[i].AcceptsInput) list.Add(i);
        return list;
    }

    /// <summary>Set focus to the given section index.</summary>
    public void SetFocus(int sectionIndex)
    {
        if (sectionIndex >= 0 && sectionIndex < _sections.Count && _sections[sectionIndex].AcceptsInput)
            _focusedSectionIndex = sectionIndex;
    }
}

public enum HudAnchor
{
    Top,
    Bottom,
}

public class HudSection
{
    public string Name { get; init; } = "";
    public HudAnchor Anchor { get; init; } = HudAnchor.Top;
    public bool IsFixedHeight { get; init; } = true;
    public int FixedHeight { get; init; }
    public bool Scrollable { get; init; }
    public bool AcceptsInput { get; init; }
    /// <summary>
    /// When true, the renderer shows "↑ more above" / "↓ more below" indicators that
    /// each consume one visible row. Scrolling logic accounts for these reserved rows so
    /// the selected item is never hidden behind an indicator.
    /// </summary>
    public bool UseScrollIndicators { get; init; }

    // Computed by layout — set by ComputeLayout
    public int StartRow { get; set; }
    public int RowCount { get; set; }

    // Scroll state for scrollable sections
    public int ScrollOffset { get; set; }
    public int TotalContentRows { get; set; }
    public int SelectedIndex { get; set; }

    /// <summary>Total allocated rows, clamped to zero minimum.</summary>
    public int VisibleContentRows => Math.Max(0, RowCount);

    /// <summary>
    /// Returns the number of rows actually available for item rendering given the total
    /// item count and the current ScrollOffset. When UseScrollIndicators is true, visible
    /// indicator rows (↑ / ↓) are subtracted so the selected item is never hidden behind one.
    /// </summary>
    public int EffectiveItemRows(int totalItems)
    {
        if (!UseScrollIndicators || totalItems <= RowCount)
            return RowCount;
        int rows = RowCount;
        if (ScrollOffset > 0) rows--;                    // top indicator occupies one row
        if (ScrollOffset + rows < totalItems) rows--;    // bottom indicator occupies one row
        return Math.Max(1, rows);
    }

    public void ScrollUp()
    {
        if (SelectedIndex > 0) SelectedIndex--;
        EnsureSelectionVisible();
    }

    public void ScrollDown(int maxItems)
    {
        if (SelectedIndex < maxItems - 1) SelectedIndex++;
        EnsureSelectionVisible(maxItems);
    }

    /// <summary>
    /// Adjusts ScrollOffset so that SelectedIndex is within the visible window.
    /// Does not account for scroll indicators — prefer EnsureSelectionVisible(totalItems)
    /// when the section uses UseScrollIndicators.
    /// </summary>
    public void EnsureSelectionVisible()
    {
        if (SelectedIndex < ScrollOffset)
            ScrollOffset = SelectedIndex;
        else if (SelectedIndex >= ScrollOffset + VisibleContentRows)
            ScrollOffset = SelectedIndex - VisibleContentRows + 1;
    }

    /// <summary>
    /// Indicator-aware variant. Iterates until ScrollOffset is stable so that
    /// SelectedIndex is genuinely visible (not behind a scroll indicator row).
    /// Falls back to the simple variant when UseScrollIndicators is false.
    /// </summary>
    public void EnsureSelectionVisible(int totalItems)
    {
        if (!UseScrollIndicators)
        {
            EnsureSelectionVisible();
            return;
        }
        int prevOffset;
        do
        {
            prevOffset = ScrollOffset;
            int rows = EffectiveItemRows(totalItems);
            if (SelectedIndex < ScrollOffset)
                ScrollOffset = SelectedIndex;
            else if (SelectedIndex >= ScrollOffset + rows)
                ScrollOffset = SelectedIndex - rows + 1;
        } while (ScrollOffset != prevOffset);
    }

    /// <summary>
    /// Clamps SelectedIndex and ScrollOffset to be valid for the given total item count.
    /// Call after the section's RowCount changes (e.g. after ComputeLayout) or when the
    /// item collection shrinks, to ensure the cursor never points outside the visible area.
    /// </summary>
    public void ClampToValidState(int totalItems)
    {
        if (totalItems <= 0)
        {
            SelectedIndex = 0;
            ScrollOffset = 0;
            return;
        }
        if (SelectedIndex >= totalItems)
            SelectedIndex = totalItems - 1;
        // If everything fits in the available rows, scrolling is unnecessary
        if (totalItems <= RowCount)
            ScrollOffset = 0;
        EnsureSelectionVisible(totalItems);
    }
}
