using RogueLikeNet.Client.Core.Rendering;

namespace RogueLikeNet.Client.Core.Tests;

public class HudLayoutTests
{
    [Fact]
    public void ComputeLayout_FixedTopSections_GetCorrectPositions()
    {
        var layout = new HudLayout();
        layout.AddSection(new HudSection { Name = "A", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 4 });
        layout.AddSection(new HudSection { Name = "B", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 3 });

        layout.ComputeLayout(30);

        Assert.Equal(0, layout.Sections[0].StartRow);
        Assert.Equal(4, layout.Sections[0].RowCount);
        Assert.Equal(4, layout.Sections[1].StartRow);
        Assert.Equal(3, layout.Sections[1].RowCount);
    }

    [Fact]
    public void ComputeLayout_BottomAnchoredSection_AtBottom()
    {
        var layout = new HudLayout();
        layout.AddSection(new HudSection { Name = "Top", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 5 });
        layout.AddSection(new HudSection { Name = "Bottom", Anchor = HudAnchor.Bottom, IsFixedHeight = true, FixedHeight = 3 });

        layout.ComputeLayout(30);

        Assert.Equal(0, layout.Sections[0].StartRow);
        Assert.Equal(5, layout.Sections[0].RowCount);
        Assert.Equal(27, layout.Sections[1].StartRow);
        Assert.Equal(3, layout.Sections[1].RowCount);
    }

    [Fact]
    public void ComputeLayout_VariableSection_FillsRemaining()
    {
        var layout = new HudLayout();
        layout.AddSection(new HudSection { Name = "Fixed", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 5 });
        layout.AddSection(new HudSection { Name = "Variable", IsFixedHeight = false });
        layout.AddSection(new HudSection { Name = "Bottom", Anchor = HudAnchor.Bottom, IsFixedHeight = true, FixedHeight = 3 });

        layout.ComputeLayout(30);

        Assert.Equal(0, layout.Sections[0].StartRow);
        Assert.Equal(5, layout.Sections[1].StartRow);
        Assert.Equal(22, layout.Sections[1].RowCount); // 30 - 5 - 3
        Assert.Equal(27, layout.Sections[2].StartRow);
    }

    [Fact]
    public void CycleFocus_CyclesThroughInputSections()
    {
        var layout = new HudLayout();
        layout.AddSection(new HudSection { Name = "A", IsFixedHeight = true, FixedHeight = 5, AcceptsInput = false });
        layout.AddSection(new HudSection { Name = "B", IsFixedHeight = true, FixedHeight = 5, AcceptsInput = true });
        layout.AddSection(new HudSection { Name = "C", IsFixedHeight = true, FixedHeight = 5, AcceptsInput = true });
        layout.AddSection(new HudSection { Name = "D", IsFixedHeight = true, FixedHeight = 5, AcceptsInput = false });

        Assert.Equal(-1, layout.FocusedSectionIndex);

        layout.CycleFocus();
        Assert.Equal(1, layout.FocusedSectionIndex); // B

        layout.CycleFocus();
        Assert.Equal(2, layout.FocusedSectionIndex); // C

        layout.CycleFocus();
        Assert.Equal(1, layout.FocusedSectionIndex); // back to B
    }

    [Fact]
    public void SetFocus_IgnoresNonInputSection()
    {
        var layout = new HudLayout();
        layout.AddSection(new HudSection { Name = "NoInput", IsFixedHeight = true, FixedHeight = 5 });
        layout.AddSection(new HudSection { Name = "HasInput", IsFixedHeight = true, FixedHeight = 5, AcceptsInput = true });

        layout.SetFocus(0); // Non-input section
        Assert.Equal(-1, layout.FocusedSectionIndex);

        layout.SetFocus(1); // Input section
        Assert.Equal(1, layout.FocusedSectionIndex);
    }

    [Fact]
    public void HudSection_ScrollUpDown_UpdatesSelectedIndex()
    {
        var section = new HudSection { Name = "Test", IsFixedHeight = false, Scrollable = true, RowCount = 5 };
        section.SelectedIndex = 0;

        section.ScrollDown(10);
        Assert.Equal(1, section.SelectedIndex);

        section.ScrollDown(10);
        section.ScrollDown(10);
        Assert.Equal(3, section.SelectedIndex);

        section.ScrollUp();
        Assert.Equal(2, section.SelectedIndex);
    }

    [Fact]
    public void HudSection_ScrollDown_ClampsAtMax()
    {
        var section = new HudSection { Name = "Test", IsFixedHeight = false, Scrollable = true, RowCount = 5 };
        section.SelectedIndex = 4;

        section.ScrollDown(5); // Should not go past 4
        Assert.Equal(4, section.SelectedIndex);
    }

    [Fact]
    public void HudSection_ScrollUp_ClampsAtZero()
    {
        var section = new HudSection { Name = "Test", IsFixedHeight = false, Scrollable = true, RowCount = 5 };
        section.SelectedIndex = 0;

        section.ScrollUp(); // Should stay at 0
        Assert.Equal(0, section.SelectedIndex);
    }

    // ── Dynamic resize: ClampToValidState ──────────────────────────────────────

    [Fact]
    public void ClampToValidState_SelectedIndexOutOfRange_ClampsToLastItem()
    {
        var section = new HudSection { Name = "S", IsFixedHeight = false, RowCount = 10 };
        section.SelectedIndex = 8;

        section.ClampToValidState(5); // Only 5 items now

        Assert.Equal(4, section.SelectedIndex);
    }

    [Fact]
    public void ClampToValidState_AllItemsFit_ResetsScrollOffset()
    {
        var section = new HudSection { Name = "S", IsFixedHeight = false, RowCount = 6 };
        section.SelectedIndex = 2;
        section.ScrollOffset = 2;

        // All 5 items fit in 6 visible rows — scroll is unnecessary
        section.ClampToValidState(5);

        Assert.Equal(0, section.ScrollOffset);
        Assert.Equal(2, section.SelectedIndex); // Index unchanged (still valid)
    }

    [Fact]
    public void ClampToValidState_ZeroItems_ResetsState()
    {
        var section = new HudSection { Name = "S", IsFixedHeight = false, RowCount = 5 };
        section.SelectedIndex = 3;
        section.ScrollOffset = 1;

        section.ClampToValidState(0);

        Assert.Equal(0, section.SelectedIndex);
        Assert.Equal(0, section.ScrollOffset);
    }

    [Fact]
    public void ClampToValidState_RowCountShrinks_CursorStaysVisible()
    {
        // Section had 10 rows, cursor at row 8 with scroll. Then container shrinks to 3 rows.
        var section = new HudSection { Name = "S", IsFixedHeight = false, RowCount = 3 };
        section.SelectedIndex = 8;
        section.ScrollOffset = 6;

        section.ClampToValidState(10); // 10 items still exist; only size shrank

        // SelectedIndex is still 8 (within item range), but scroll must cover it
        Assert.Equal(8, section.SelectedIndex);
        // ScrollOffset: to show index 8 in a 3-row window → offset must be at most 6 (8-3+1=6)
        Assert.Equal(6, section.ScrollOffset);
    }

    [Fact]
    public void ClampToValidState_BothItemCountAndRowCountShrink_ClampsCorrectly()
    {
        var section = new HudSection { Name = "S", IsFixedHeight = false, RowCount = 3 };
        section.SelectedIndex = 8;
        section.ScrollOffset = 5;

        // Items shrank to 4, visible rows to 3 — cursor is out of range
        section.ClampToValidState(4);

        Assert.Equal(3, section.SelectedIndex); // Clamped to last item (index 3)
        Assert.True(section.ScrollOffset <= section.SelectedIndex);
        Assert.True(section.SelectedIndex < section.ScrollOffset + section.VisibleContentRows);
    }

    // ── Cross-section navigation ───────────────────────────────────────────────

    [Fact]
    public void FocusNextInputSection_WrapsToFirst_WhenAtLast()
    {
        var layout = new HudLayout();
        layout.AddSection(new HudSection { Name = "A", IsFixedHeight = true, FixedHeight = 5, AcceptsInput = true });
        layout.AddSection(new HudSection { Name = "B", IsFixedHeight = true, FixedHeight = 5, AcceptsInput = true });
        layout.SetFocus(1); // B

        var next = layout.FocusNextInputSection();

        Assert.NotNull(next);
        Assert.Equal("A", next.Name); // Wraps back to A
        Assert.Equal(0, layout.FocusedSectionIndex);
    }

    [Fact]
    public void FocusPreviousInputSection_WrapsToLast_WhenAtFirst()
    {
        var layout = new HudLayout();
        layout.AddSection(new HudSection { Name = "A", IsFixedHeight = true, FixedHeight = 5, AcceptsInput = true });
        layout.AddSection(new HudSection { Name = "B", IsFixedHeight = true, FixedHeight = 5, AcceptsInput = true });
        layout.SetFocus(0); // A

        var prev = layout.FocusPreviousInputSection();

        Assert.NotNull(prev);
        Assert.Equal("B", prev.Name); // Wraps to last
        Assert.Equal(1, layout.FocusedSectionIndex);
    }

    [Fact]
    public void FocusNextInputSection_SingleSection_ReturnsSameSection()
    {
        var layout = new HudLayout();
        layout.AddSection(new HudSection { Name = "Only", IsFixedHeight = true, FixedHeight = 5, AcceptsInput = true });
        layout.SetFocus(0);

        var next = layout.FocusNextInputSection();

        Assert.NotNull(next);
        Assert.Equal("Only", next.Name); // Same section
        Assert.Equal(0, layout.FocusedSectionIndex);
    }

    [Fact]
    public void FocusPreviousInputSection_SingleSection_ReturnsSameSection()
    {
        var layout = new HudLayout();
        layout.AddSection(new HudSection { Name = "Only", IsFixedHeight = true, FixedHeight = 5, AcceptsInput = true });
        layout.SetFocus(0);

        var prev = layout.FocusPreviousInputSection();

        Assert.NotNull(prev);
        Assert.Equal("Only", prev.Name);
        Assert.Equal(0, layout.FocusedSectionIndex);
    }

    [Fact]
    public void FocusNextInputSection_SkipsNonInputSections()
    {
        var layout = new HudLayout();
        layout.AddSection(new HudSection { Name = "A", IsFixedHeight = true, FixedHeight = 5, AcceptsInput = true });
        layout.AddSection(new HudSection { Name = "Mid", IsFixedHeight = true, FixedHeight = 5, AcceptsInput = false });
        layout.AddSection(new HudSection { Name = "B", IsFixedHeight = true, FixedHeight = 5, AcceptsInput = true });
        layout.SetFocus(0); // A

        var next = layout.FocusNextInputSection();

        Assert.NotNull(next);
        Assert.Equal("B", next.Name); // Skipped Mid
        Assert.Equal(2, layout.FocusedSectionIndex);
    }

    [Fact]
    public void FocusNextInputSection_NoInputSections_ReturnsNull()
    {
        var layout = new HudLayout();
        layout.AddSection(new HudSection { Name = "A", IsFixedHeight = true, FixedHeight = 5, AcceptsInput = false });

        var next = layout.FocusNextInputSection();

        Assert.Null(next);
    }

    // ── Scroll indicator row accounting ───────────────────────────────────────

    // Helper: asserts that SelectedIndex is within the effective visible window.
    private static void AssertIndexVisible(HudSection s, int totalItems)
    {
        int rows = s.EffectiveItemRows(totalItems);
        Assert.True(s.SelectedIndex >= s.ScrollOffset,
            $"Index {s.SelectedIndex} < ScrollOffset {s.ScrollOffset}");
        Assert.True(s.SelectedIndex < s.ScrollOffset + rows,
            $"Index {s.SelectedIndex} >= ScrollOffset {s.ScrollOffset} + effectiveRows {rows}");
    }

    [Fact]
    public void EffectiveItemRows_NoIndicators_WhenItemsFit()
    {
        var section = new HudSection { Name = "S", IsFixedHeight = false, RowCount = 5, UseScrollIndicators = true };
        // Items fit — no indicators
        Assert.Equal(5, section.EffectiveItemRows(5));
        Assert.Equal(5, section.EffectiveItemRows(3));
    }

    [Fact]
    public void EffectiveItemRows_BothIndicators_WhenScrolledToMiddle()
    {
        var section = new HudSection { Name = "S", IsFixedHeight = false, RowCount = 5, UseScrollIndicators = true };
        section.ScrollOffset = 2; // top indicator present
        // bottom: 2 + (5-1) = 6 < 10 → bottom indicator also present → rows = 5-2 = 3
        Assert.Equal(3, section.EffectiveItemRows(10));
    }

    [Fact]
    public void EffectiveItemRows_OnlyBottomIndicator_WhenAtTop()
    {
        var section = new HudSection { Name = "S", IsFixedHeight = false, RowCount = 5, UseScrollIndicators = true };
        section.ScrollOffset = 0; // no top indicator
        // bottom: 0 + 5 = 5 < 8 → bottom indicator → rows = 5-1 = 4
        Assert.Equal(4, section.EffectiveItemRows(8));
    }

    [Fact]
    public void EffectiveItemRows_OnlyTopIndicator_WhenAtEnd()
    {
        var section = new HudSection { Name = "S", IsFixedHeight = false, RowCount = 5, UseScrollIndicators = true };
        section.ScrollOffset = 6; // top indicator present; items 6-9 fill the remaining 4 rows
        // bottom: 6 + (5-1) = 10, NOT < 10 → no bottom indicator → rows = 5-1 = 4
        Assert.Equal(4, section.EffectiveItemRows(10));
    }

    [Fact]
    public void ScrollDown_WithScrollIndicators_SelectionNeverHiddenBehindIndicator()
    {
        // 5 rows, 8 items — indicators will appear once scrolling starts
        var section = new HudSection { Name = "S", IsFixedHeight = false, RowCount = 5, UseScrollIndicators = true };
        section.SelectedIndex = 0;
        section.ScrollOffset = 0;

        for (int target = 1; target < 8; target++)
        {
            section.ScrollDown(8);
            AssertIndexVisible(section, 8);
        }

        Assert.Equal(7, section.SelectedIndex);
    }

    [Fact]
    public void ScrollDown_WithScrollIndicators_CanReachEveryItem()
    {
        var section = new HudSection { Name = "S", IsFixedHeight = false, RowCount = 5, UseScrollIndicators = true };

        // Scroll from 0 to 9 in a 5-row section with 10 items
        for (int target = 1; target < 10; target++)
        {
            section.ScrollDown(10);
            AssertIndexVisible(section, 10);
        }

        Assert.Equal(9, section.SelectedIndex);
    }

    [Fact]
    public void ScrollUp_WithScrollIndicators_SelectionRemainsVisible()
    {
        // Start at the bottom and scroll all the way up — selection must stay visible at each step
        var section = new HudSection { Name = "S", IsFixedHeight = false, RowCount = 5, UseScrollIndicators = true };
        // Scroll down first to position near end
        for (int i = 0; i < 9; i++) section.ScrollDown(10);
        Assert.Equal(9, section.SelectedIndex);

        for (int i = 0; i < 9; i++)
        {
            section.ScrollUp();
            AssertIndexVisible(section, 10);
        }

        Assert.Equal(0, section.SelectedIndex);
    }

    [Fact]
    public void EnsureSelectionVisible_WithIndicators_ConvergesForEveryOffset()
    {
        // For each possible selectedIndex and a section that uses indicators,
        // EnsureSelectionVisible(totalItems) must leave the item truly visible.
        const int totalItems = 12;
        const int rowCount = 5;

        for (int idx = 0; idx < totalItems; idx++)
        {
            var section = new HudSection { Name = "S", IsFixedHeight = false, RowCount = rowCount, UseScrollIndicators = true };
            section.SelectedIndex = idx;
            section.ScrollOffset = 0; // force recalculation from scratch
            section.EnsureSelectionVisible(totalItems);
            AssertIndexVisible(section, totalItems);
        }
    }

    [Fact]
    public void ClampToValidState_WithScrollIndicators_SelectionVisible()
    {
        // 5 rows, was showing items 6-10 (with indicators). Items shrink to 8.
        var section = new HudSection { Name = "S", IsFixedHeight = false, RowCount = 5, UseScrollIndicators = true };
        section.SelectedIndex = 9;
        section.ScrollOffset = 6;

        section.ClampToValidState(8);

        Assert.Equal(7, section.SelectedIndex); // Clamped to last valid item
        AssertIndexVisible(section, 8);          // Must be truly visible (not behind indicator)
    }

    [Fact]
    public void ClampToValidState_WithScrollIndicators_AllItemsFitResetsOffset()
    {
        // Was scrolled, but after resize all items fit — no indicators, no offset needed
        var section = new HudSection { Name = "S", IsFixedHeight = false, RowCount = 8, UseScrollIndicators = true };
        section.SelectedIndex = 3;
        section.ScrollOffset = 2;

        section.ClampToValidState(5); // 5 items fit in 8 rows

        Assert.Equal(0, section.ScrollOffset);
        Assert.Equal(3, section.SelectedIndex);
    }

    [Fact]
    public void FocusedSection_WhenNoFocus_ReturnsNull()
    {
        var layout = new HudLayout();
        layout.AddSection(new HudSection { Name = "A", IsFixedHeight = true, FixedHeight = 5, AcceptsInput = true });
        Assert.Equal(-1, layout.FocusedSectionIndex);
        Assert.Null(layout.FocusedSection);
    }

    [Fact]
    public void FocusedSection_WhenFocused_ReturnsCorrectSection()
    {
        var layout = new HudLayout();
        layout.AddSection(new HudSection { Name = "A", IsFixedHeight = true, FixedHeight = 5, AcceptsInput = true });
        layout.SetFocus(0);
        Assert.NotNull(layout.FocusedSection);
        Assert.Equal("A", layout.FocusedSection!.Name);
    }

    [Fact]
    public void CycleFocus_NoInputSections_StaysNegativeOne()
    {
        var layout = new HudLayout();
        layout.AddSection(new HudSection { Name = "A", IsFixedHeight = true, FixedHeight = 5, AcceptsInput = false });
        layout.AddSection(new HudSection { Name = "B", IsFixedHeight = true, FixedHeight = 5, AcceptsInput = false });

        layout.CycleFocus();
        Assert.Equal(-1, layout.FocusedSectionIndex);
    }

    [Fact]
    public void SetFocus_OutOfRange_DoesNotChange()
    {
        var layout = new HudLayout();
        layout.AddSection(new HudSection { Name = "A", IsFixedHeight = true, FixedHeight = 5, AcceptsInput = true });
        layout.SetFocus(0);
        Assert.Equal(0, layout.FocusedSectionIndex);

        layout.SetFocus(99);
        Assert.Equal(0, layout.FocusedSectionIndex); // Unchanged
    }

    [Fact]
    public void HudSection_EnsureSelectionVisible_NoParam_ScrollsDownWhenNeeded()
    {
        var section = new HudSection { Name = "S", IsFixedHeight = false, RowCount = 3 };
        section.SelectedIndex = 5;
        section.ScrollOffset = 0;

        section.EnsureSelectionVisible();

        // SelectedIndex 5 should be visible in a 3-row window
        Assert.True(section.SelectedIndex >= section.ScrollOffset);
        Assert.True(section.SelectedIndex < section.ScrollOffset + section.VisibleContentRows);
    }

    [Fact]
    public void HudSection_EnsureSelectionVisible_NoParam_ScrollsUpWhenNeeded()
    {
        var section = new HudSection { Name = "S", IsFixedHeight = false, RowCount = 3 };
        section.SelectedIndex = 1;
        section.ScrollOffset = 5;

        section.EnsureSelectionVisible();

        Assert.Equal(1, section.ScrollOffset);
    }

    [Fact]
    public void HudSection_EnsureSelectionVisible_WithIndicators_ConvergesForEdgeCase()
    {
        // Set up a scenario where the do/while loop needs 2 iterations:
        // SelectedIndex right at the boundary where adding an indicator shifts the visible range
        var section = new HudSection { Name = "S", IsFixedHeight = false, RowCount = 5, UseScrollIndicators = true };
        section.SelectedIndex = 4;
        section.ScrollOffset = 1; // top indicator present, initially 3 effective rows

        section.EnsureSelectionVisible(10);

        // After stabilization, selected index should be visible
        int rows = section.EffectiveItemRows(10);
        Assert.True(section.SelectedIndex >= section.ScrollOffset);
        Assert.True(section.SelectedIndex < section.ScrollOffset + rows);
    }
}
