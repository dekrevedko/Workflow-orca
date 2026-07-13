using AwesomeAssertions;
using OrcaCore.Core.Definitions;
using Xunit;

namespace OrcaCore.Core.Tests.Building;

public sealed class PartitionerTests
{
    [Fact]
    public void ItemPartitioner_PreservesInputOrder()
    {
        var items = new[] { 3, 1, 2 };

        var partitions = WorkflowPartitioner<int>.Items().Partition(items);

        partitions.SelectMany(partition => partition.Items).Should().Equal(items);
        partitions.Select(partition => partition.Index).Should().Equal(0, 1, 2);
        partitions[0].Items.Should().Equal(3);
        partitions[1].Items.Should().Equal(1);
        partitions[2].Items.Should().Equal(2);
    }

    [Fact]
    public void FixedBatchPartitioner_CreatesStableBatches()
    {
        var items = new[] { 1, 2, 3, 4, 5 };

        var partitions = WorkflowPartitioner<int>.Batch(2).Partition(items);

        partitions.Select(partition => partition.Index).Should().Equal(0, 1, 2);
        partitions[0].Items.Should().Equal(1, 2);
        partitions[1].Items.Should().Equal(3, 4);
        partitions[2].Items.Should().Equal(5);
    }

    [Fact]
    public void SelectorBatchPartitioner_UsesDeterministicSelector()
    {
        var items = new[] { "b1", "a1", "b2", "a2" };

        var partitions = WorkflowPartitioner<string>.BatchBy(item => item[0]).Partition(items);

        partitions.Select(partition => partition.Index).Should().Equal(0, 1);
        partitions[0].Items.Should().Equal("b1", "b2");
        partitions[1].Items.Should().Equal("a1", "a2");
    }
}
