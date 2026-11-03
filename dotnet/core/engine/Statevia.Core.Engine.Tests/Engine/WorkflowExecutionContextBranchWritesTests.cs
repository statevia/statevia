using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Engine;
using System.Text.Json;
using Xunit;

namespace Statevia.Core.Engine.Tests.Engine;

/// <summary>
/// Hosted Fork の枝の Context が親の値を引き継ぎ、Join では枝が書いた値だけを戻すことを確かめる。
/// </summary>
/// <remarks>
/// 枝の Context は <see cref="WorkflowExecutionContext.CreateForkBranch"/> で作り、終端時に
/// <see cref="WorkflowExecutionContext.ExportBranchWrites"/> で書いた値を取り出し、親は <see cref="WorkflowExecutionContext.ApplyVarWrites"/> で枝の完了順に適用する。
/// </remarks>
public sealed class WorkflowExecutionContextBranchWritesTests
{
    /// <summary>枝は親の vars / states を同じパスで読める。</summary>
    [Fact]
    public void CreateForkBranch_InheritsParentVarsAndStates()
    {
        // Arrange
        var parent = CreateParentWithOrder();

        // Act
        var branch = CreateBranch(parent);
        var root = branch.ToPathRoot();

        // Assert
        Assert.Equal("new", SimpleJsonPathResolver.Resolve(root, "$.vars.order.status").Value);
        Assert.Equal("o-1", SimpleJsonPathResolver.Resolve(root, "$.states.Receive.output.orderId").Value);
    }

    /// <summary>JSON で運ばれた引き継ぎ値（work item の payload 経由）も、ネストした書き込みで親から引き継いだ兄弟キーを失わない。</summary>
    [Fact]
    public void CreateForkBranch_FromJsonParent_NestedWriteKeepsInheritedSiblings()
    {
        // Arrange
        using var vars = JsonDocument.Parse("""{"order":{"status":"new","total":0}}""");
        using var states = JsonDocument.Parse("""{"Receive":{"output":{"orderId":"o-1"}}}""");
        var branch = WorkflowExecutionContext.CreateForkBranch(
            new ForkBranchStart(null, new ForkParentContext(vars.RootElement, states.RootElement, "parent-1")),
            "W");

        // Act
        branch.SetVar("$.vars.order.status", "charged");
        var root = branch.ToPathRoot();

        // Assert
        Assert.Equal("charged", Text(SimpleJsonPathResolver.Resolve(root, "$.vars.order.status").Value));
        Assert.Equal("0", Text(SimpleJsonPathResolver.Resolve(root, "$.vars.order.total").Value));
        Assert.Equal("o-1", SimpleJsonPathResolver.Resolve(root, "$.states.Receive.output.orderId").Value);
    }

    /// <summary>枝の <c>$.sys.execution.id</c> は親の論理実行 ID になる。</summary>
    [Fact]
    public void CreateForkBranch_SysExecutionIdIsLogicalExecutionId()
    {
        // Arrange
        var parent = WorkflowExecutionContext.Create(null, "parent-1", "W");

        // Act
        var branch = CreateBranch(parent);

        // Assert
        Assert.Equal("parent-1", SimpleJsonPathResolver.Resolve(branch.ToPathRoot(), "$.sys.execution.id").Value);
    }

    /// <summary>ネストした Fork の孫も、根の論理実行 ID と、子が引き継いだ値を引き継ぐ。</summary>
    [Fact]
    public void CreateForkBranch_Nested_InheritsRootIdAndAncestorValues()
    {
        // Arrange
        var root = CreateParentWithOrder();
        var child = CreateBranch(root);
        child.SetVar("$.vars.childOnly", 1);

        // Act
        var grandchild = CreateBranch(child);
        var path = grandchild.ToPathRoot();

        // Assert
        Assert.Equal("parent-1", SimpleJsonPathResolver.Resolve(path, "$.sys.execution.id").Value);
        Assert.Equal("new", SimpleJsonPathResolver.Resolve(path, "$.vars.order.status").Value);
        Assert.Equal(1, SimpleJsonPathResolver.Resolve(path, "$.vars.childOnly").Value);
    }

    /// <summary>Fork 到達時の写しは、以後の親の書き込みの影響を受けない。</summary>
    [Fact]
    public void ExportForkParentContext_IsNotAffectedByLaterParentWrites()
    {
        // Arrange
        var parent = CreateParentWithOrder();
        var snapshot = parent.ExportForkParentContext();

        // Act
        parent.SetVar("$.vars.order.status", "changed");
        parent.SetStateOutput("Receive", new Dictionary<string, object?> { ["orderId"] = "o-2" });
        var branch = WorkflowExecutionContext.CreateForkBranch(new ForkBranchStart(null, snapshot), "W");
        var root = branch.ToPathRoot();

        // Assert
        Assert.Equal("new", SimpleJsonPathResolver.Resolve(root, "$.vars.order.status").Value);
        Assert.Equal("o-1", SimpleJsonPathResolver.Resolve(root, "$.states.Receive.output.orderId").Value);
    }

    /// <summary>引き継いだだけの値は、Join へ戻す値に含めない。</summary>
    [Fact]
    public void ExportBranchWrites_ExcludesInheritedValues()
    {
        // Arrange
        var branch = CreateBranch(CreateParentWithOrder());
        branch.SetStateOutput("Charge", "ok");
        branch.SetVar("$.vars.billing", new Dictionary<string, object?> { ["id"] = "b-1" });

        // Act
        var writes = WorkflowExecutionContext.ExportBranchWrites(branch.ExportCheckpoint());

        // Assert
        Assert.Equal(["Charge"], writes.States!.Keys);
        var write = Assert.Single(writes.VarWrites!);
        Assert.Equal(["billing"], write.Path);
        Assert.Equal("b-1", ((JsonElement)write.Value!).GetProperty("id").GetString());
    }

    /// <summary>祖先のパスを書くと、記録済みの子孫のパスはまとめられる。</summary>
    [Fact]
    public void SetVar_AncestorAfterDescendant_KeepsOnlyAncestorPath()
    {
        // Arrange
        var context = WorkflowExecutionContext.Create(null);
        context.SetVar("$.vars.order.status", "charged");

        // Act
        context.SetVar("$.vars.order", new Dictionary<string, object?> { ["status"] = "shipped" });
        var paths = context.ExportCheckpoint().WrittenVarPaths;

        // Assert
        var path = Assert.Single(paths!);
        Assert.Equal(["order"], path);
    }

    /// <summary>祖先が記録済みなら、子孫のパスは記録しない（祖先の値に含まれる）。</summary>
    [Fact]
    public void SetVar_DescendantAfterAncestor_DoesNotAddPath()
    {
        // Arrange
        var context = WorkflowExecutionContext.Create(null);
        context.SetVar("$.vars.order", new Dictionary<string, object?> { ["status"] = "new" });

        // Act
        context.SetVar("$.vars.Order.Status", "charged");
        var writes = WorkflowExecutionContext.ExportBranchWrites(context.ExportCheckpoint());

        // Assert
        var write = Assert.Single(writes.VarWrites!);
        Assert.Equal(["order"], write.Path);
        Assert.Equal("charged", ((JsonElement)write.Value!).GetProperty("status").GetString());
    }

    /// <summary>書いたパス・完了した状態・論理実行 ID は checkpoint を往復しても残る（Unload と再開をまたぐ）。</summary>
    [Fact]
    public void ExportCheckpoint_RoundTrip_KeepsWrittenRecordsAndLogicalId()
    {
        // Arrange
        var branch = CreateBranch(CreateParentWithOrder());
        branch.SetVar("$.vars.order.status", "charged");
        branch.SetStateOutput("Charge", "ok");

        // Act
        var restored = WorkflowExecutionContext.CreateFromCheckpoint(branch.ExportCheckpoint(), "child-1", "W");
        var writes = WorkflowExecutionContext.ExportBranchWrites(restored.ExportCheckpoint());

        // Assert
        Assert.Equal("parent-1", SimpleJsonPathResolver.Resolve(restored.ToPathRoot(), "$.sys.execution.id").Value);
        Assert.Equal(["Charge"], writes.States!.Keys);
        var write = Assert.Single(writes.VarWrites!);
        Assert.Equal(["order", "status"], write.Path);
    }

    /// <summary>書いたパスの記録が無い checkpoint は、vars の全トップレベルキーと全状態を書いたものとして扱う。</summary>
    [Fact]
    public void ExportBranchWrites_CheckpointWithoutRecords_TreatsAllAsWritten()
    {
        // Arrange
        using var vars = JsonDocument.Parse("""{"billing":{"id":"b-1"},"note":"x"}""");
        using var state = JsonDocument.Parse("""{"output":"ok"}""");
        var data = new CheckpointContextData
        {
            Vars = vars.RootElement,
            States = new Dictionary<string, JsonElement?> { ["Charge"] = state.RootElement }
        };

        // Act
        var writes = WorkflowExecutionContext.ExportBranchWrites(data);

        // Assert
        Assert.Equal(["Charge"], writes.States!.Keys);
        Assert.Equal([["billing"], ["note"]], writes.VarWrites!.Select(write => write.Path.ToArray()));
    }

    /// <summary>
    /// 枝 A が引き継いだ <c>order</c> を書き換え、後に完了した枝 B は触らないとき、Join 後の親の <c>order</c> は A の値になる。
    /// </summary>
    [Fact]
    public void Join_BranchWithoutWrite_DoesNotOverwriteSiblingWithInheritedValue()
    {
        // Arrange
        var parent = CreateParentWithOrder();
        var branchA = CreateBranch(parent);
        var branchB = CreateBranch(parent);
        branchA.SetVar("$.vars.order", new Dictionary<string, object?> { ["status"] = "charged" });
        branchB.SetStateOutput("Ship", "shipped");

        // Act
        ApplyInCompletionOrder(parent, branchA, branchB);
        var root = parent.ToPathRoot();

        // Assert
        Assert.Equal("charged", Text(SimpleJsonPathResolver.Resolve(root, "$.vars.order.status").Value));
        Assert.Equal("shipped", Text(SimpleJsonPathResolver.Resolve(root, "$.states.Ship.output").Value));
    }

    /// <summary>枝 A の <c>order.status</c> と枝 B の <c>order.total</c> は、Join 後の親に両方残る。</summary>
    [Fact]
    public void Join_DisjointPaths_KeepsBothWrites()
    {
        // Arrange
        var parent = CreateParentWithOrder();
        var branchA = CreateBranch(parent);
        var branchB = CreateBranch(parent);
        branchA.SetVar("$.vars.order.status", "charged");
        branchB.SetVar("$.vars.order.total", 100);

        // Act
        ApplyInCompletionOrder(parent, branchA, branchB);
        var root = parent.ToPathRoot();

        // Assert
        Assert.Equal("charged", Text(SimpleJsonPathResolver.Resolve(root, "$.vars.order.status").Value));
        Assert.Equal("100", Text(SimpleJsonPathResolver.Resolve(root, "$.vars.order.total").Value));
        Assert.Equal("o-1", Text(SimpleJsonPathResolver.Resolve(root, "$.vars.order.id").Value));
    }

    /// <summary>重なるパスは後に完了した枝が勝つ（枝 B の <c>order</c> 全体が、枝 A の <c>order.status</c> を含めて上書きする）。</summary>
    [Fact]
    public void Join_OverlappingPaths_LaterBranchWins()
    {
        // Arrange
        var parent = CreateParentWithOrder();
        var branchA = CreateBranch(parent);
        var branchB = CreateBranch(parent);
        branchA.SetVar("$.vars.order.status", "charged");
        branchB.SetVar("$.vars.order", new Dictionary<string, object?> { ["total"] = 5 });

        // Act
        ApplyInCompletionOrder(parent, branchA, branchB);
        var root = parent.ToPathRoot();

        // Assert
        Assert.Equal("5", Text(SimpleJsonPathResolver.Resolve(root, "$.vars.order.total").Value));
        Assert.False(SimpleJsonPathResolver.Resolve(root, "$.vars.order.status").Found);
    }

    /// <summary>
    /// ネストした Fork の Join で取り込んだ値は、子が書いた値として外側の Join へ戻る。
    /// </summary>
    [Fact]
    public void ApplyVarWritesAndMergeStateEntries_RecordAsWrittenForOuterJoin()
    {
        // Arrange
        var child = CreateBranch(CreateParentWithOrder());

        // Act
        child.ApplyVarWrites([new ContextVarWrite(["order", "status"], "charged")]);
        child.MergeStateEntries(new Dictionary<string, object?>
        {
            ["Inner"] = new Dictionary<string, object?> { ["output"] = 1 }
        });
        var writes = WorkflowExecutionContext.ExportBranchWrites(child.ExportCheckpoint());

        // Assert
        Assert.Equal(["Inner"], writes.States!.Keys);
        var write = Assert.Single(writes.VarWrites!);
        Assert.Equal(["order", "status"], write.Path);
    }

    /// <summary>
    /// 解決した値を比較用の文字列にする。JSON を経由した値は <see cref="JsonElement"/> や数値型が変わるため、型ではなく値で比べる。
    /// </summary>
    private static string? Text(object? value) =>
        value switch
        {
            JsonElement element => element.ToString(),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
        };

    /// <summary><c>order</c> と完了済み状態 <c>Receive</c> を持つ親（論理実行 ID は <c>parent-1</c>）。</summary>
    private static WorkflowExecutionContext CreateParentWithOrder()
    {
        var parent = WorkflowExecutionContext.Create(null, "parent-1", "W");
        parent.SetVar("$.vars.order", new Dictionary<string, object?>
        {
            ["id"] = "o-1",
            ["status"] = "new",
            ["total"] = 0
        });
        parent.SetStateOutput("Receive", new Dictionary<string, object?> { ["orderId"] = "o-1" });
        return parent;
    }

    /// <summary>親の現時点の写しを引き継いだ枝の Context を作る。</summary>
    private static WorkflowExecutionContext CreateBranch(WorkflowExecutionContext parent) =>
        WorkflowExecutionContext.CreateForkBranch(new ForkBranchStart(null, parent.ExportForkParentContext()), "W");

    /// <summary>各枝の書いた値を、渡した順（枝の完了順）に親へ適用する（Hosted の Join と同じ経路）。</summary>
    private static void ApplyInCompletionOrder(WorkflowExecutionContext parent, params WorkflowExecutionContext[] branches)
    {
        foreach (var writes in branches.Select(branch => WorkflowExecutionContext.ExportBranchWrites(branch.ExportCheckpoint())))
        {
            parent.MergeStateEntries(writes.States!);
            parent.ApplyVarWrites(writes.VarWrites!);
        }
    }
}
