# Trigger Coverage Chain Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent Trigger deduplication from deleting an active Trigger that still covers downstream FOVs.

**Architecture:** Keep the existing Trigger ABI and reduction algorithm. Add a regression executable that invokes the real private reduction method, then make the minimal deletion-rule change and isolate reduction mutations with a deep copy that is published only after coverage succeeds.

**Tech Stack:** C# 7.3, .NET Framework 4.8, reflection-based console regression test.

## Global Constraints

- Do not force `InspecJudgeReview` entries to `true`.
- Preserve `Dictionary<int, Dictionary<int, int>>` Trigger ABI.
- Do not delete an active Trigger when `InspecNums[indexRef] != 0`.
- Do not publish a partially reduced Trigger map when coverage validation fails.

---

### Task 1: Add the chain-break regression test

**Files:**
- Create: `SWM.RegressionTests/SWM.RegressionTests.csproj`
- Create: `SWM.RegressionTests/Program.cs`

- [ ] Build and run the regression test against the existing code.
- [ ] Verify it fails because Trigger 65 is incorrectly removed.

### Task 2: Fix reduction and publish semantics

**Files:**
- Modify: `Parameters.cs`

- [ ] Keep active Trigger 65 when it has downstream coverage.
- [ ] Continue removing a leaf Trigger whose mode is zero.
- [ ] Deep-copy the active Trigger map before reduction.
- [ ] Publish the reduced map only after coverage succeeds.

### Task 3: Verify

- [ ] Run the regression executable and verify both scenarios pass.
- [ ] Build `SWM.csproj` in `Debug|AnyCPU`.

