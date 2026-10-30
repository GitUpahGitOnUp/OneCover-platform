using OneCover.Claims.Api.Models;
using OneCover.Claims.Api.Models.Enums;

namespace OneCover.Claims.Tests;

/// <summary>Unit tests for the claim status rulebook: which moves are allowed and which are blocked.</summary>
public class ClaimWorkflowTests
{
    // [Theory] + [InlineData] = the SAME test run once per data row, so every allowed or blocked move
    // is checked without copy-pasting a test for each one.

    // Every move the workflow allows.
    [Theory]
    [InlineData(ClaimStatus.Submitted, ClaimStatus.InReview)]
    [InlineData(ClaimStatus.InReview, ClaimStatus.InfoRequested)]
    [InlineData(ClaimStatus.InReview, ClaimStatus.Approved)]
    [InlineData(ClaimStatus.InReview, ClaimStatus.Denied)]
    [InlineData(ClaimStatus.InfoRequested, ClaimStatus.InReview)]
    [InlineData(ClaimStatus.Approved, ClaimStatus.Closed)]
    [InlineData(ClaimStatus.Denied, ClaimStatus.Closed)]
    public void CanMove_AllowsValidMoves(ClaimStatus from, ClaimStatus to)
    {
        Assert.True(ClaimWorkflow.CanMove(from, to));
    }

    // A sample of moves that must be blocked: skipping review, reopening, flipping a decision.
    [Theory]
    [InlineData(ClaimStatus.Submitted, ClaimStatus.Approved)]       // can't skip review
    [InlineData(ClaimStatus.Submitted, ClaimStatus.Closed)]         // can't close without a decision
    [InlineData(ClaimStatus.Closed, ClaimStatus.InReview)]          // Closed is final
    [InlineData(ClaimStatus.Denied, ClaimStatus.Approved)]          // a decision can't be flipped
    [InlineData(ClaimStatus.Approved, ClaimStatus.Denied)]
    [InlineData(ClaimStatus.InfoRequested, ClaimStatus.Approved)]   // must go back through review
    [InlineData(ClaimStatus.InReview, ClaimStatus.InReview)]        // a move to the same status isn't a move
    public void CanMove_BlocksInvalidMoves(ClaimStatus from, ClaimStatus to)
    {
        Assert.False(ClaimWorkflow.CanMove(from, to));
    }

    [Fact]
    public void NextStatuses_IsEmpty_ForClosed()
    {
        Assert.Empty(ClaimWorkflow.NextStatuses(ClaimStatus.Closed));
    }

    [Fact]
    public void NextStatuses_ListsAllThreeOptions_FromInReview()
    {
        var next = ClaimWorkflow.NextStatuses(ClaimStatus.InReview);

        Assert.Equal([ClaimStatus.InfoRequested, ClaimStatus.Approved, ClaimStatus.Denied], next);
    }

    [Fact]
    public void NextStatuses_AgreesWithCanMove_ForEveryStatus()
    {
        // For every status, each status listed as "next" must be allowed by CanMove, and every status
        // not listed must be blocked. This keeps the two methods from drifting apart.
        foreach (var from in Enum.GetValues<ClaimStatus>())
        {
            var next = ClaimWorkflow.NextStatuses(from);

            foreach (var to in Enum.GetValues<ClaimStatus>())
            {
                Assert.Equal(next.Contains(to), ClaimWorkflow.CanMove(from, to));
            }
        }
    }
}
