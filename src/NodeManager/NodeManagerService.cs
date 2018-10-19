using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Description;
using System.Fabric.Query;
using System.Threading;
using System.Threading.Tasks;

namespace NodeManager
{
    internal class NodeManagerService : ActorService
    {
        private static readonly TimeSpan ScanInterval;
        private static readonly TimeSpan ClientOperationTimeout;
        private static readonly TimeSpan DownNodeGraceInterval;
        private static readonly bool SkipNodesUnderFabricUpgrade;

        public NodeManagerService(
            StatefulServiceContext context,
            ActorTypeInformation actorTypeInfo,
            Func<ActorService, ActorId, ActorBase> actorFactory = null,
            Func<ActorBase, IActorStateProvider, IActorStateManager> stateManagerFactory = null,
            IActorStateProvider stateProvider = null, ActorServiceSettings settings = null)
            : base(context, actorTypeInfo, actorFactory, stateManagerFactory, stateProvider, settings)
        {
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            await base.RunAsync(cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await RemoveScaledInNodesAsync(cancellationToken);
                }
                catch (Exception e)
                {
                    ActorEventSource.Current.ServiceError(
                        this.Context,
                        "Failed to remove scaled-in nodes, Error = {0}",
                        e);
                }

                await Task.Delay(ScanInterval, cancellationToken);
            }
        }

        /// <summary>
        /// Remove the nodes that are scaled in but still managed by Service Fabric. These nodes show up as down nodes.
        /// </summary>
        private async Task RemoveScaledInNodesAsync(CancellationToken cancellationToken)
        {
            var client = new FabricClient();

            if (SkipNodesUnderFabricUpgrade)
            {
                var upgradeInProgress = await IsFabricUpgradeInProgressAsync(client, cancellationToken);


                if (upgradeInProgress)
                {
                    ActorEventSource.Current.ServiceMessage(
                        this.Context,
                        "Skipping removing scaled-in nodes as fabric upgrade is in progress.");
                    return;
                }
            }

            var nodesToRemove = await GetNodesToRemoveAsync(client, cancellationToken);
            await RemoveNodesAsync(client, nodesToRemove, cancellationToken);
        }

        private Task RemoveNodesAsync(
            FabricClient client,
            IList<Node> nodesToRemove,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        private async Task<IList<Node>> GetNodesToRemoveAsync(
            FabricClient client,
            CancellationToken cancellationToken)
        {
            var nodesToRemove = new List<Node>();

            var queryDescription = new NodeQueryDescription();
            queryDescription.ContinuationToken = null;

            do
            {
                var nodeList = await client.QueryManager.GetNodePagedListAsync(
                    queryDescription,
                    ClientOperationTimeout,
                    cancellationToken);
                foreach (var node in nodeList)
                {
                    if (IsMyType(node))
                    {
                        // do not remove the nodes where this service is running
                        continue;
                    }

                    if (node.NodeStatus == NodeStatus.Down)
                    {
                        // is down long enough
                        if (IsDownLongEnough(node))
                        {
                            nodesToRemove.Add(node);
                        }
                    }
                }

                queryDescription.ContinuationToken = nodeList.ContinuationToken;
            } while (queryDescription.ContinuationToken != null);

            return nodesToRemove;
        }

        private bool IsDownLongEnough(Node node)
        {
            var downInterval = DateTime.UtcNow.Subtract(node.NodeDownAt);

            ActorEventSource.Current.ServiceMessage(
                this.Context,
                "Node {0} is down for {1} time.",
                downInterval);

            return (downInterval.CompareTo(DownNodeGraceInterval) > 0);
        }


        private bool IsMyType(Node node)
        {
            return (string.Compare(
                this.Context.NodeContext.NodeType,
                node.NodeType,
                StringComparison.OrdinalIgnoreCase) == 0);
        }

        private async Task<bool> IsFabricUpgradeInProgressAsync(
            FabricClient client,
            CancellationToken cancellationToken)
        {
            ActorEventSource.Current.ServiceMessage(
                this.Context,
                "Checking if FabricUpgrade is in progress or not.");

            var upgradeProgress = await client.ClusterManager.GetFabricUpgradeProgressAsync(ClientOperationTimeout, cancellationToken);

            if ((upgradeProgress.UpgradeState == FabricUpgradeState.RollingBackInProgress) ||
                (upgradeProgress.UpgradeState == FabricUpgradeState.RollingForwardInProgress) ||
                (upgradeProgress.UpgradeState == FabricUpgradeState.RollingForwardPending))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        static NodeManagerService()
        {
            ScanInterval = TimeSpan.FromSeconds(60);
            ClientOperationTimeout = TimeSpan.FromSeconds(30);
            DownNodeGraceInterval = TimeSpan.FromSeconds(120);
            SkipNodesUnderFabricUpgrade = true;
        }
    }

}
